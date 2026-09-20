# Feature Spec: Delete Todo — Confirmation Dialog (`/todos/:id`, `TodoDetailRoute`)

Third frontend feature. This is the requirements' **Delete** operation — "remove a to-do item by its ID." Adds a Delete button to [`detail-route.md`](detail-route.md)'s previously read-only `TodoDetailRoute`, which is the first mutation this route performs — everything up to now on that route was `View` only, per [`detail-route.md`'s Scope](detail-route.md#scope). The backend contract this action calls is already fully built — [`../backend/delete-todo/spec.md`](../backend/delete-todo/spec.md) (`DELETE /todos/{id}`) — and matches the "one action per route, dispatched by an intent field" shape already anticipated (but not yet built) in [`../../architecture/frontend/overview.md#one-action-per-route-dispatched-by-an-intent-field`](../../architecture/frontend/overview.md#routes--backend-operations).

This is a **spec, not an implementation** — no frontend code is written yet, matching how [`main-route.md`](main-route.md) and [`detail-route.md`](detail-route.md) were written before their code.

## Scope

In scope:

- A **Delete** button on `TodoDetailRoute`, visible on the existing read-only detail view.
- A **confirmation dialog** shown when Delete is clicked, with **Yes** / **No** choices:
  - **Yes** performs the deletion (`DELETE /todos/{id}`) and navigates back to `/` on success.
  - **No** simply closes the dialog — no request is made, the detail view is unchanged.
- `TodoDetailRoute.action.ts` — new file, this route's first `action`. Handles the `delete` intent only (see [Action: `deleteTodo`](#action-deletetodo)); `update`/`complete`/`incomplete` intents mentioned in the architecture doc's route table are **not** added by this spec.
- Handling the `404`-on-delete race (todo already gone by the time Yes is clicked) — see [Action: `deleteTodo`](#action-deletetodo).
- Component and route-level tests for all of the above.

Out of scope (future specs):

- **Edit** (`PUT /todos/{id}`) and **Complete/Incomplete** (`PATCH /todos/{id}`) — still not implemented; `TodoForm.tsx`'s edit mode remains unused. When one of those specs lands, it extends this same `TodoDetailRoute.action.ts` with another `intent` branch rather than introducing a second action file — see [`../../architecture/frontend/overview.md#one-action-per-route-dispatched-by-an-intent-field`](../../architecture/frontend/overview.md#routes--backend-operations).
- Any styling beyond plain Tailwind utility classes on semantic HTML — no visual design pass, same exception as `main-route.md`/`detail-route.md`.
- Undo / soft delete — matches the backend's own stance, see [`../backend/delete-todo/spec.md#scope`](../backend/delete-todo/spec.md#scope) ("no mention of recovery").

## API Client

No new generation step — `orval` already generated `deleteTodo` in [`main-route.md`'s API Client Generation](main-route.md#api-client-generation-orval) pass, ahead of use. This spec is simply the first caller.

- `deleteTodo(id: string): Promise<{ data: void; status: 204 | 404; ... }>` → `DELETE /todos/{id}`, from `src/api/generated/todos/todos.ts`.
- Same non-throwing contract as every other generated call: a `404` resolves normally with `status: 404`, it does not reject/throw. This route's action must branch on `status` explicitly, same as `TodoListRoute.action.ts` already does for `addTodo`.
- **No response body on success** (`204`), matching [`../backend/delete-todo/spec.md#dto-contract`](../backend/delete-todo/spec.md#dto-contract) — there's nothing to read out of `data` on the success path, unlike `addTodo`/`getTodoById`.

## Directory/File Plan

New files this spec introduces (subset of the full tree in [`../../architecture/frontend/overview.md#directory-structure`](../../architecture/frontend/overview.md#directory-structure) — only what Delete needs; everything else already exists per [`main-route.md`](main-route.md)/[`detail-route.md`](detail-route.md)):

```text
frontend/
├── src/
│   ├── routes/
│   │   ├── TodoDetailRoute.tsx            # add Delete button + dialog wiring
│   │   └── TodoDetailRoute.action.ts      # new — handles the "delete" intent
│   └── components/
│       └── ConfirmDialog.tsx              # new — generic yes/no confirmation dialog
└── tests/
    ├── mocks/
    │   └── handlers.ts                    # add a DELETE /todos/:id handler
    ├── routes/
    │   └── TodoDetailRoute.test.tsx        # extend with delete-flow cases
    └── components/
        └── ConfirmDialog.test.tsx         # new
```

`router.tsx` gains one line — `action: todoDetailAction` on the existing `/todos/:id` entry (see [`router.tsx`](#routertsx) below); no new route is added.

## `ConfirmDialog.tsx`

Presentational, generic (not delete-specific) — reusable if a future spec needs a second confirm-before-mutating flow (e.g. a future "clear completed" bulk action), though none exists yet and this spec doesn't build for that hypothetically beyond making the component not hard-code "delete" in its copy.

Props: `open: boolean`, `message: string`, `onConfirm: () => void`, `onCancel: () => void`.

- Renders **nothing** when `open` is `false` — a conditionally-rendered dialog, not a CSS-hidden one, per [`../../architecture/frontend/overview.md#styling`](../../architecture/frontend/overview.md#styling)'s "plain Tailwind utility classes on semantic HTML, no component library" convention. Built as a plain positioned `<div>` overlay + panel, not the native `<dialog>` element — kept simple and dependency-free for a two-button yes/no prompt; `<dialog>`'s extra semantics (top-layer stacking, `::backdrop`, focus-trapping) aren't needed for this app's scope and would be the first non-trivial native-API integration in a codebase that has otherwise stayed to plain controlled components (per [`../../architecture/frontend/overview.md`](../../architecture/frontend/overview.md#open-questions)'s "no form library" precedent — same reasoning applies here).
- Renders `message` as body text, plus **Yes** and **No** buttons wired to `onConfirm`/`onCancel` respectively.
- No focus-trap, no `Escape`-to-close, no portal — matches the "no visual/interaction-polish pass" exception already carried by every prior spec in this directory. Revisit only if a future spec's requirements explicitly call for it.

## Delete Button + Dialog State (`TodoDetailRoute.tsx`)

- Adds a `showConfirm` boolean piece of local component state (`useState`), owned by `TodoDetailRoute` — not global, not in the loader/action, since it's pure UI state with no server round-trip of its own.
- A **Delete** button, visible alongside the existing back-to-list link, sets `showConfirm` to `true` when clicked. It does **not** submit anything by itself.
- Renders `<ConfirmDialog open={showConfirm} message="Delete this to-do?" onConfirm={...} onCancel={() => setShowConfirm(false)} />`.
- `onConfirm` submits the actual delete: per [React Router's `useSubmit()`](https://reactrouter.com/en/main/hooks/use-submit) (a programmatic submit, not a `<Form>` the Yes button renders inside of — the button lives inside `ConfirmDialog`, which is generic and shouldn't know about this route's `intent` field or FormData shape), building `FormData` with `intent: "delete"` and calling `submit(formData, { method: "post" })`. This triggers `TodoDetailRoute.action.ts`.
- Uses `useNavigation()` to disable both the Delete button and the dialog's Yes/No buttons while the delete submission is in flight (`navigation.state === "submitting"`), same UX-feedback pattern `TodoListRoute.tsx` already uses for the add form.
- **No optimistic UI.** The detail view stays exactly as-is (dialog included, if the delete somehow doesn't immediately navigate) until the action's `redirect("/")` actually completes the navigation — nothing renders a spinner or fades the page out beyond the disabled-buttons treatment above. Matches the plain, non-optimistic style `TodoListRoute` already uses for its add flow.

## Action: `deleteTodo`

`TodoDetailRoute.action.ts` — this route's first action, and (per [Scope](#scope)) the only intent it handles for now:

- Reads `id` from `params` (`ActionFunctionArgs`), same as `TodoDetailRoute.loader.ts` already does.
- Reads `FormData` from the request; reads `intent`. Since `delete` is the only branch that exists yet, an unrecognized/missing `intent` throws a `Response("Unknown action", { status: 400 })` — defensive, not a real reachable case today, matching the same "explicit rather than assumed away" posture [`detail-route.md`'s loader](detail-route.md#loader-gettodobyid) took for its own "any other status" branch.
- Calls the generated `deleteTodo(id)`.
- On `status === 204`: returns `redirect("/")`. This is a **cross-route** redirect (detail → list), unlike `TodoListRoute.action.ts`'s same-route redirect — React Router's data APIs handle both identically; the list route's loader still re-runs as a fresh navigation, so the list reflects the deletion with no manual refetch, per [`../../architecture/frontend/overview.md#data-layer`](../../architecture/frontend/overview.md).
- On `status === 404`: **the confirm-then-delete race** — the todo existed when the page loaded (or the user has had the confirm dialog open a while) but was deleted by something else before Yes was clicked. Treated as success from the user's perspective: also returns `redirect("/")`, on the reasoning that the user's intent ("this todo should not exist") is already satisfied — same idempotency stance [`../backend/delete-todo/spec.md#dto-contract`](../backend/delete-todo/spec.md#dto-contract) documents for the backend's own repeated-delete case, just re-applied one layer up at the UI. No error message is shown for this case; redirecting straight to the now-accurate list is simpler and no less correct than surfacing a "this was already deleted" toast for a race the user did nothing wrong to trigger.
- Any other status: throws a `Response` with that status — no other documented failure mode exists for `DELETE /todos/{id}` today ([`../backend/delete-todo/spec.md#dto-contract`](../backend/delete-todo/spec.md#dto-contract) only documents `204`/`404`), but the action still branches explicitly rather than assuming only those two occur. Unlike `TodoListRoute.action.ts`'s equivalent branch (which *returns* rather than throws, to preserve in-progress form input), throwing here is fine — there's no form input on this page to preserve, and an unexpected failure deleting a todo is exactly the kind of "can't render this screen's intended outcome" case `errorElement` exists for.

## `router.tsx`

Extends the existing `/todos/:id` entry from [`detail-route.md#routertsx`](detail-route.md#routertsx) with an `action`:

```ts
createBrowserRouter([
  { path: "/", element: <TodoListRoute />, loader: todoListLoader, action: todoListAction },
  {
    path: "/todos/:id",
    element: <TodoDetailRoute />,
    loader: todoDetailLoader,
    action: todoDetailAction,
    errorElement: <ErrorBoundary />,
  },
])
```

## Testing

Per [`../../architecture/frontend/overview.md#testing`](../../architecture/frontend/overview.md#testing), same split prior specs in this directory established:

- **`tests/components/ConfirmDialog.test.tsx`** — RTL, in isolation: renders nothing when `open` is `false`; renders `message` and both buttons when `open` is `true`; clicking **Yes** calls `onConfirm`; clicking **No** calls `onCancel` and not `onConfirm`.
- **`tests/routes/TodoDetailRoute.test.tsx`** — extended with delete-flow cases, RTL + `createMemoryRouter` + MSW:
  - Clicking Delete shows the confirmation dialog; the detail view is still present underneath (dialog, not full replacement).
  - Clicking **No** closes the dialog without any `DELETE` request being made (assert via an MSW handler override that fails the test if called, or a call-count spy).
  - Clicking Delete then **Yes** issues `DELETE /todos/:id` and navigates to `/` — assert via the memory router's resulting location, same pattern `TodoListRoute.test.tsx` uses for its post-redirect assertion.
  - Clicking Delete then **Yes** against a mocked `404` (already-deleted race) still navigates to `/` without rendering an error.
- **`tests/mocks/handlers.ts`** — add a `DELETE /todos/:id` handler: removes the matching entry from the in-memory `todos` array and returns `204`, or returns `404` if the id isn't found — mirrors the existing `GET /todos/:id` handler's lookup logic added in [`detail-route.md`](detail-route.md).

No Playwright/E2E — out of scope project-wide, same as prior specs.

## Wiring Checklist

- [ ] `src/components/ConfirmDialog.tsx`: new file — generic yes/no dialog per [`ConfirmDialog.tsx`](#confirmdialogtsx).
- [ ] `src/routes/TodoDetailRoute.action.ts`: new file — action per [Action: `deleteTodo`](#action-deletetodo).
- [ ] `src/routes/TodoDetailRoute.tsx`: add Delete button, `showConfirm` state, `useSubmit()`/`useNavigation()` wiring per [Delete Button + Dialog State](#delete-button--dialog-state-tododetailroutetsx).
- [ ] `src/router.tsx`: add `action: todoDetailAction` to the existing `/todos/:id` entry.
- [ ] `tests/mocks/handlers.ts`: add a `DELETE /todos/:id` MSW handler.
- [ ] `tests/components/ConfirmDialog.test.tsx`: new test file.
- [ ] `tests/routes/TodoDetailRoute.test.tsx`: extend with the delete-flow cases in [Testing](#testing).
- [ ] Update `README.md` per [`CLAUDE.md`'s sync rule](../../../CLAUDE.md#keep-readmemd-in-sync) once implemented — the Delete operation moves from "not yet available" to done.

## Open Questions

- **Whether `ConfirmDialog` should be reused for a future Edit/Complete confirmation.** Not needed today — Edit and Complete/Incomplete have no confirm-before-submit requirement in `requirements.md`. `ConfirmDialog` is written generically (see [`ConfirmDialog.tsx`](#confirmdialogtsx)) so it *can* be reused later without rework, but this spec doesn't presume a second caller will ever exist.
- **Keyboard/accessibility polish for the dialog** (focus trap, `Escape` to cancel, `aria-modal`). Deliberately deferred — see [`ConfirmDialog.tsx`](#confirmdialogtsx)'s note — consistent with this project's stated "no visual/interaction design pass" scope exception, but flagged here in case Foci's review explicitly weighs accessibility; revisit if so.

## Related Docs

- [`detail-route.md`](detail-route.md) — the view-only detail route this spec adds its first mutation to; loader, `TodoResponse` shape, and error-boundary pattern this spec builds on
- [`main-route.md`](main-route.md) — the API client generation pass, and the `TodoListRoute.action.ts` intent-branching/redirect/status-checking conventions this spec's action mirrors
- [`../backend/delete-todo/spec.md`](../backend/delete-todo/spec.md) — backend `DELETE /todos/{id}` contract this action calls, including the `204`/`404` shape and the repeated-delete idempotency stance this spec's `404` handling re-applies client-side
- [`../../architecture/frontend/overview.md`](../../architecture/frontend/overview.md#one-action-per-route-dispatched-by-an-intent-field) — the "one action per route, dispatched by an intent field" pattern this spec is the first to actually implement
- [`../../requirements/requirements.md`](../../requirements/requirements.md) — source **Delete** requirement
