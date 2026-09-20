# Feature Spec: Toggle Completion Status (`/todos/:id`, `TodoDetailRoute`)

Fourth frontend feature. This is the requirements' **Complete** and **Incomplete** operations — "mark a specific to-do item as completed/not completed by its ID" — collapsed into a single toggle button, matching the backend's own "one endpoint, not two" decision (see [`../backend/update-completion-status/spec.md#scope`](../backend/update-completion-status/spec.md#scope)). Adds a second mutation to `TodoDetailRoute`, alongside the Delete button [`delete-todo.md`](delete-todo.md) already added — this spec extends the same `TodoDetailRoute.action.ts` with a second `intent` branch rather than introducing a new action file, exactly as [`delete-todo.md`'s Scope](delete-todo.md#scope) anticipated. The backend contract this action calls is already fully built — [`../backend/update-completion-status/spec.md`](../backend/update-completion-status/spec.md) (`PATCH /todos/{id}`).

This is a **spec, not an implementation** — no frontend code is written yet, matching how the three prior specs in this directory were written before their code.

## Scope

In scope:

- A **toggle button** on `TodoDetailRoute`, visible on the existing detail view alongside Delete.
  - Label reads **"Complete"** when `todo.isCompleted` is `false`.
  - Label reads **"Incomplete"** when `todo.isCompleted` is `true`.
  - Clicking it submits the opposite boolean of the todo's current `isCompleted` and, on success, the page reflects the new state — button label flips, and `TodoDetail`'s existing Status field (see [`detail-route.md#tododetailtsx`](detail-route.md#tododetailtsx)) updates to match.
- A new `toggleComplete` (or equivalently named) `intent` branch in `TodoDetailRoute.action.ts`, alongside the existing `delete` branch.
- Handling the `404`-on-toggle race (todo deleted between page load and clicking the toggle) — see [Action: `updateCompletionStatus`](#action-updatecompletionstatus).
- Component and route-level tests for all of the above.

Out of scope (future specs):

- **Edit** (`PUT /todos/{id}`) — still not implemented; `TodoForm.tsx`'s edit mode remains unused.
- **No confirmation dialog for the toggle.** Unlike Delete, toggling completion is trivially reversible (click it again), so this spec doesn't reuse `ConfirmDialog.tsx` for it — clicking the button submits immediately.
- Any styling beyond plain Tailwind utility classes on semantic HTML — no visual design pass, same exception as every prior spec in this directory.
- Toggling from the list view (`TodoListRoute`/`TodoList.tsx`) — the requirements' Complete/Incomplete operations only require doing this "by its ID," which the detail route already addresses; a per-row toggle on the list is a plausible future enhancement but isn't required and isn't built here.

## API Client

No new generation step — `orval` already generated `updateCompletionStatus` in [`main-route.md`'s API Client Generation](main-route.md#api-client-generation-orval) pass, ahead of use. This spec is simply the first caller.

- `updateCompletionStatus(id: string, body: UpdateCompletionStatusEndpointRequest): Promise<{ data: TodoResponse | void; status: 200 | 404; ... }>` → `PATCH /todos/{id}`, from `src/api/generated/todos/todos.ts`.
- `UpdateCompletionStatusEndpointRequest` is `{ isCompleted: boolean }`, generated from the backend's `UpdateCompletionStatusEndpoint.Request` record (see [`../backend/update-completion-status/spec.md#dto-contract`](../backend/update-completion-status/spec.md#dto-contract)).
- Same non-throwing contract as every other generated call: a `404` resolves normally with `status: 404`, it does not reject/throw. This route's action must branch on `status` explicitly, same as the existing `delete` branch already does.
- **Success returns the full `TodoResponse`** (`200`, not `204`) — unlike `deleteTodo`, there's a body to use: the backend's response reflects the server-computed `updatedAt`, per [`../backend/update-completion-status/spec.md#dto-contract`](../backend/update-completion-status/spec.md#dto-contract). This spec doesn't read the response body directly in the action, though — see [Action](#action-updatecompletionstatus) for why a loader re-run is used instead.

## Directory/File Plan

No new files — every file this spec touches already exists per [`delete-todo.md`](delete-todo.md):

```text
frontend/
├── src/
│   ├── routes/
│   │   ├── TodoDetailRoute.tsx             # add toggle button + submit wiring
│   │   └── TodoDetailRoute.action.ts       # add a second intent branch
│   └── components/
│       └── TodoDetail.tsx                  # unchanged — already renders isCompleted-driven status text
└── tests/
    ├── mocks/
    │   └── handlers.ts                     # add a PATCH /todos/:id handler
    └── routes/
        └── TodoDetailRoute.test.tsx        # extend with toggle-flow cases
```

`router.tsx` is unchanged — no new route, and the existing `/todos/:id` entry already has an `action` (`todoDetailAction`) per [`delete-todo.md#routertsx`](delete-todo.md#routertsx).

## Toggle Button (`TodoDetailRoute.tsx`)

- A **toggle button**, rendered alongside the existing Delete button, reading `todo.isCompleted` from loader data (`useLoaderData()`) to decide its label — no separate local state for the label, since the loader's `todo` is already the single source of truth for the current status (same reasoning `TodoDetail`'s own Status field already relies on).
- Clicking it builds `FormData` with `intent: "toggleComplete"` and `isCompleted: String(!todo.isCompleted)`, then submits via [React Router's `useSubmit()`](https://reactrouter.com/en/main/hooks/use-submit) — same programmatic-submit pattern the Delete button's confirm handler already uses (per [`delete-todo.md#delete-button--dialog-state-tododetailroutetsx`](delete-todo.md#delete-button--dialog-state-tododetailroutetsx)), not a `<Form>`, since this is a single button with no surrounding form fields.
- Reuses the existing `useNavigation()` submitting-state check already in place for the Delete button (`navigation.state === "submitting"`) to disable **both** the toggle button and the Delete button while either mutation is in flight — the two buttons share one "something's submitting" guard rather than each tracking its own, since both submissions go through the same route `action` and only one can be in flight at a time.
- **No optimistic UI.** The button's label and `TodoDetail`'s Status field only change once the loader re-runs with fresh data (see [Action](#action-updatecompletionstatus)) — same non-optimistic stance [`delete-todo.md#delete-button--dialog-state-tododetailroutetsx`](delete-todo.md#delete-button--dialog-state-tododetailroutetsx) already took for Delete.

## Action: `updateCompletionStatus`

Extends the existing `TodoDetailRoute.action.ts` (see [`delete-todo.md#action-deletetodo`](delete-todo.md#action-deletetodo)) with a second `intent` branch:

- Reads `id` from `params`, same as the existing `delete` branch.
- Reads `intent` from `FormData`; branches to this path when `intent === "toggleComplete"`. The existing `delete` branch and the "unknown intent → `400`" fallback are otherwise unchanged.
- Reads `isCompleted` from `FormData` and parses it back to a `boolean` (`formData.get("isCompleted") === "true"`) — `FormData` values are always strings, so the button's `String(!todo.isCompleted)` (see [Toggle Button](#toggle-button-tododetailroutetsx)) round-trips through this parse.
- Calls the generated `updateCompletionStatus(id, { isCompleted })`.
- On `status === 200`: returns `null` (no redirect, unlike `delete`) — this is a **same-route** submission, so React Router's data APIs automatically re-run this route's `loader` after a successful `action` on the same route, which re-fetches the todo via `getTodoById` and gives `TodoDetailRoute` the updated `isCompleted`/`updatedAt` without this action needing to thread the `200` response's body through `useActionData()` itself. Matches the "loader re-run instead of manual refetch" convention already established by [`main-route.md`'s `addTodo` action](main-route.md#action-addtodo) and [`delete-todo.md`'s cross-route redirect](delete-todo.md#action-deletetodo) — here it's a same-route re-run rather than a redirect-triggered one, but the same underlying mechanism.
- On `status === 404`: **the toggle-after-delete race** — the todo existed when the page loaded but was deleted by something else before the toggle was clicked. Unlike `delete`'s `404` handling (which redirects to `/`, treating "already gone" as success), a toggle has no sensible "success" framing once the target is gone — there's nothing to have completed or reopened. Returns `redirect("/")` anyway, on the same reasoning [`delete-todo.md#action-deletetodo`](delete-todo.md#action-deletetodo) used: the detail page for a nonexistent todo can't usefully re-render, and redirecting to the now-accurate list is simpler than inventing an error-message path for a race the user did nothing wrong to trigger. No error message is shown for this case, consistent with the identical `404` case on `delete`.
- Any other status: throws a `Response` with that status, same "no other documented failure mode, but still explicit" posture as every prior action/loader branch in this directory — see [`delete-todo.md#action-deletetodo`](delete-todo.md#action-deletetodo)'s equivalent note.

```ts
// TodoDetailRoute.action.ts, extended
export const todoDetailAction = async ({ params, request }: ActionFunctionArgs) => {
  const id = params.id as string
  const formData = await request.formData()
  const intent = formData.get('intent')

  if (intent === 'delete') {
    // existing branch, unchanged — see delete-todo.md
  }

  if (intent === 'toggleComplete') {
    const isCompleted = formData.get('isCompleted') === 'true'
    const result = await updateCompletionStatus(id, { isCompleted })

    if (result.status === 200) {
      return null
    }

    if (result.status === 404) {
      return redirect('/')
    }

    throw new Response('Failed to update completion status', { status: result.status })
  }

  throw new Response('Unknown action', { status: 400 })
}
```

## `router.tsx`

No changes — the `/todos/:id` entry already has `action: todoDetailAction` wired from [`delete-todo.md#routertsx`](delete-todo.md#routertsx); this spec's new intent branch lives inside that same function.

## Testing

Per [`../../architecture/frontend/overview.md#testing`](../../architecture/frontend/overview.md#testing), same split prior specs in this directory established:

- **`tests/routes/TodoDetailRoute.test.tsx`** — extended with toggle-flow cases, RTL + `createMemoryRouter` + MSW:
  - An incomplete todo renders a button labeled **"Complete"**; a completed todo renders one labeled **"Incomplete"**.
  - Clicking the toggle on an incomplete todo issues `PATCH /todos/:id` with `{"isCompleted": true}`; after the loader re-runs, the button now reads **"Incomplete"** and the Status field reads **"Completed"**.
  - Clicking the toggle on a completed todo issues `PATCH /todos/:id` with `{"isCompleted": false}` and flips the other way.
  - Clicking the toggle while a mocked `PATCH` handler returns `404` navigates to `/`, same assertion pattern `delete-todo.md`'s equivalent race-case test uses.
  - The toggle button and the Delete button are both disabled while either submission is in flight (assert via `navigation.state`, or by checking the `disabled` attribute during a deliberately-delayed MSW response).
- **`tests/mocks/handlers.ts`** — add a `PATCH /todos/:id` handler: updates the matching in-memory todo's `isCompleted` (and `updatedAt`) and returns `200` with the updated `TodoResponse`, or returns `404` if the id isn't found — mirrors the existing `DELETE /todos/:id` handler's lookup logic added in [`delete-todo.md`](delete-todo.md#testing).

No new component test file — `TodoDetail.test.tsx` (from [`detail-route.md#testing`](detail-route.md#testing)) already covers the Status field's "Completed"/"Incomplete" text for both `isCompleted` values; this spec doesn't duplicate that coverage for the new button, since the button's label logic is simple enough to verify at the route level alongside the submit behavior it's paired with.

No Playwright/E2E — out of scope project-wide, same as prior specs.

## Wiring Checklist

- [ ] `src/routes/TodoDetailRoute.action.ts`: add the `toggleComplete` intent branch per [Action: `updateCompletionStatus`](#action-updatecompletionstatus).
- [ ] `src/routes/TodoDetailRoute.tsx`: add the toggle button, `useSubmit()` wiring, and shared submitting-state disable per [Toggle Button](#toggle-button-tododetailroutetsx).
- [ ] `tests/mocks/handlers.ts`: add a `PATCH /todos/:id` MSW handler.
- [ ] `tests/routes/TodoDetailRoute.test.tsx`: extend with the toggle-flow cases in [Testing](#testing).
- [ ] Update `README.md` per [`CLAUDE.md`'s sync rule](../../../CLAUDE.md#keep-readmemd-in-sync) once implemented — the Complete/Incomplete operations move from "not yet available" to done.

## Open Questions

- **Whether a per-row toggle should be added to `TodoList.tsx`.** Not required by `requirements.md` (both operations are phrased "by its ID," which the detail route satisfies) and not built here — see [Scope](#scope). Revisit only if Foci's review or a later requirement explicitly asks for list-level toggling.
- **Whether the two buttons (Delete, toggle) need distinct in-flight indicators** rather than sharing one `navigation.state === "submitting"` guard. Today's implementation can't distinguish "Delete is submitting" from "toggle is submitting" — both buttons simply disable together. Not addressed here since React Router's data APIs don't cheaply expose which submission is in flight without inspecting `navigation.formData`, and a shared guard is adequate UX for two buttons on one small page; revisit if a future spec adds a third concurrent mutation to this route where the ambiguity becomes more noticeable.

## Related Docs

- [`delete-todo.md`](delete-todo.md) — the first mutation added to `TodoDetailRoute`; this spec's `intent`-branching action, `useSubmit()` pattern, and same-race-becomes-redirect-to-list handling all extend what that spec established
- [`detail-route.md`](detail-route.md) — the read-only detail route both mutation specs build on; `TodoDetail`'s existing `isCompleted`-driven Status field this spec relies on updating automatically after the loader re-run
- [`main-route.md`](main-route.md) — the API client generation pass, and the "loader re-runs after a same-route action" convention this spec's `200` handling relies on
- [`../backend/update-completion-status/spec.md`](../backend/update-completion-status/spec.md) — backend `PATCH /todos/{id}` contract this action calls, including the "one endpoint, not two" decision this spec's single toggle button mirrors on the frontend, and the `200`/`404` shape this spec's action branches on
- [`../../architecture/frontend/overview.md`](../../architecture/frontend/overview.md#one-action-per-route-dispatched-by-an-intent-field) — the "one action per route, dispatched by an intent field" pattern this spec's second `intent` branch continues
- [`../../requirements/requirements.md`](../../requirements/requirements.md) — source **Complete**/**Incomplete** requirements
