# Feature Spec: Edit Todo — Title, Description, Due Date (`/todos/:id`, `TodoDetailRoute`)

Fifth frontend feature. This is the requirements' **Update** operation — "modify the title, description, or due date of an existing item by its ID." Adds `TodoForm.tsx`'s previously-unused **edit** mode ([`main-route.md`](main-route.md) built it in add-only mode; its edit mode has sat unexercised through every prior spec in this directory, most recently reaffirmed out of scope by [`detail-route.md#scope`](detail-route.md#scope) and [`delete-todo.md#scope`](delete-todo.md#scope)) to `TodoDetailRoute`, and extends `TodoDetailRoute.action.ts` with a third `intent` branch alongside the existing `delete` ([`delete-todo.md`](delete-todo.md)) and `toggleComplete` ([`toggle-completion.md`](toggle-completion.md)) branches. The backend contract this action calls is already fully built — [`../backend/update-todo/spec.md`](../backend/update-todo/spec.md) (`PUT /todos/{id}`).

This is a **spec, not an implementation** — no frontend code is written yet, matching how the four prior specs in this directory were written before their code.

## Scope

In scope:

- An **Edit** button on `TodoDetailRoute`, visible on the existing detail view alongside Delete and the completion toggle. Clicking it switches the page into edit mode, rendering `<TodoForm intent="edit" />` pre-filled with the current `title`/`description`/`dueDate` in place of the read-only `<TodoDetail />` view.
- Editable fields: **title**, **description**, **due date** — the requirement's own field list for Update. `isCompleted` is **not** a field this form edits (see [`isCompleted` on a Full-Replacement `PUT`](#iscompleted-on-a-full-replacement-put)); it continues to be owned exclusively by the toggle button from [`toggle-completion.md`](toggle-completion.md).
- Client-side validation on all three editable fields, reusing `TodoForm`'s existing add-mode checks (required `title`, max lengths) per [`main-route.md#todoformtsx`](main-route.md#todoformtsx) — same rules, now exercised in edit mode too.
- **A Save button that starts disabled and becomes enabled only once the form's current values differ from the todo's last-loaded values in one or more of the three editable fields** — the dirty-tracking behavior this spec exists to define. See [Dirty Tracking](#dirty-tracking-savebutton-enablement).
- A **Cancel** affordance that discards in-progress edits and returns to the read-only view without submitting anything.
- A new `edit` `intent` branch in `TodoDetailRoute.action.ts`, calling `updateTodo` (`PUT /todos/{id}`).
- Handling the `404`-on-save race (todo deleted between page load and clicking Save) and the `400` validation-failure response from the backend.
- Component and route-level tests for all of the above.

Out of scope (future specs):

- Editing `isCompleted` from this form — stays exclusively on the toggle button; see [`isCompleted` on a Full-Replacement `PUT`](#iscompleted-on-a-full-replacement-put).
- Editing from the list view (`TodoListRoute`/`TodoList.tsx`) — same "detail route only" scope every prior mutation spec in this directory has kept (see [`toggle-completion.md#scope`](toggle-completion.md#scope)'s equivalent exclusion).
- A confirmation dialog before Save or before discarding via Cancel — Update is reversible (the todo can just be edited back), matching the reasoning [`toggle-completion.md#scope`](toggle-completion.md#scope) used to skip `ConfirmDialog` for the completion toggle. Cancel with unsaved changes does not prompt "are you sure?" either — see [Cancel](#cancel).
- Any styling beyond plain Tailwind utility classes on semantic HTML — no visual design pass, same exception as every prior spec in this directory.
- Autosave / save-on-blur — Save is the only way changes are persisted; navigating away (including via Cancel) without clicking Save discards them.

## `isCompleted` on a Full-Replacement `PUT`

[`../backend/update-todo/spec.md#full-replacement-not-partial-service-tier-fields`](../backend/update-todo/spec.md#full-replacement-not-partial-service-tier-fields) is explicit that `PUT /todos/{id}` is a true full-resource replacement: the request body must include `isCompleted`, and the server overwrites it unconditionally along with `title`/`description`/`dueDate`. That is a backend-contract fact, not a UI requirement — this spec's edit form still only exposes title/description/due-date as user-editable fields, per the user's own request that drove this spec. The `edit` action branch closes that gap by echoing back the todo's **current** `isCompleted` (read from loader data, not from any form field) when it builds the `PUT` body — see [Action: `updateTodo`](#action-updatetodo). This mirrors exactly the round-trip [`../backend/update-todo/spec.md#full-replacement-not-partial-service-tier-fields`](../backend/update-todo/spec.md#full-replacement-not-partial-service-tier-fields) itself describes as the equivalence Complete/Incomplete is convenience over: "a client doing a `GetTodoById` read, flipping `isCompleted`, and re-`PUT`ting the rest unchanged" — here it's the reverse, re-`PUT`ting `isCompleted` unchanged while the rest changes. `isCompleted` is therefore **not** part of [Dirty Tracking](#dirty-tracking-savebutton-enablement)'s comparison — the form has no control for it, so it can never be the reason Save becomes enabled.

## API Client

No new generation step — `orval` already generated `updateTodo` in [`main-route.md`'s API Client Generation](main-route.md#api-client-generation-orval) pass, ahead of use. This spec is simply the first caller.

- `updateTodo(id: string, body: UpdateTodoEndpointRequest): Promise<{ data: TodoResponse | void; status: 200 | 400 | 404; ... }>` → `PUT /todos/{id}`, from `src/api/generated/todos/todos.ts`.
- `UpdateTodoEndpointRequest` is `{ title: string, description: string | null, dueDate: string | null, isCompleted: boolean }`, generated from the backend's `UpdateTodoEndpoint.Request` record (see [`../backend/update-todo/spec.md#endpoint`](../backend/update-todo/spec.md#endpoint)).
- Same non-throwing contract as every other generated call: `400`/`404` resolve normally with that `status`, neither rejects/throws. This route's action must branch on `status` explicitly, same as the existing `delete`/`toggleComplete` branches already do.
- **Success returns the full `TodoResponse`** (`200`) — same "loader re-run instead of manually threading the response body through" convention [`toggle-completion.md#api-client`](toggle-completion.md#api-client) already established for this route's other same-route mutation.

## Directory/File Plan

No new route files — every route file this spec touches already exists per [`toggle-completion.md`](toggle-completion.md). `TodoForm.tsx` already exists per [`main-route.md`](main-route.md) but has only ever been exercised in add mode until now:

```text
frontend/
├── src/
│   ├── routes/
│   │   ├── TodoDetailRoute.tsx             # add Edit/Cancel/Save UI + edit-mode state
│   │   └── TodoDetailRoute.action.ts       # add a third intent branch ("edit")
│   └── components/
│       └── TodoForm.tsx                    # exercise existing edit mode; add dirty-tracking support
└── tests/
    ├── mocks/
    │   └── handlers.ts                     # add a PUT /todos/:id handler
    ├── routes/
    │   └── TodoDetailRoute.test.tsx        # extend with edit-flow cases
    └── components/
        └── TodoForm.test.tsx               # extend with edit-mode + dirty-tracking cases
```

`router.tsx` is unchanged — no new route, and the existing `/todos/:id` entry already has `action: todoDetailAction` per [`delete-todo.md#routertsx`](delete-todo.md#routertsx).

## Edit Mode Toggle (`TodoDetailRoute.tsx`)

- Adds an `isEditing` boolean piece of local component state (`useState`), owned by `TodoDetailRoute` — same "pure UI state, not in the loader/action" reasoning [`delete-todo.md#delete-button--dialog-state-tododetailroutetsx`](delete-todo.md#delete-button--dialog-state-tododetailroutetsx) used for `showConfirm`.
- An **Edit** button, visible alongside Delete and the completion toggle when `isEditing` is `false`, sets `isEditing` to `true` when clicked. Hidden while `isEditing` is `true`.
- While `isEditing` is `true`: renders `<TodoForm intent="edit" todo={todo} onCancel={...} />` in place of `<TodoDetail todo={todo} />`. Delete and the completion toggle are **not** rendered while editing — editing one field of a todo while also being able to delete it or flip its completion status mid-edit is avoidable UI complexity this spec doesn't take on; hiding those buttons is simpler than deciding what should happen if they're clicked mid-edit.
- Reuses the existing `useNavigation()` submitting-state check already in place for Delete/toggle (`navigation.state === "submitting"`) to disable form fields and the Save button while the edit submission is in flight — same shared guard [`toggle-completion.md#toggle-button-tododetailroutetsx`](toggle-completion.md#toggle-button-tododetailroutetsx) already established across this route's mutations.
- **No optimistic UI.** The view only leaves edit mode once the action's `null` return triggers the loader re-run (see [Action](#action-updatetodo)), matching the non-optimistic stance every prior mutation spec in this directory has taken.

## Cancel

- A **Cancel** button, rendered by `TodoForm` in edit mode (not `TodoDetailRoute` directly — see [`TodoForm.tsx`](#todoformtsx-changes)), calls the `onCancel` callback `TodoDetailRoute` passes in.
- `onCancel` sets `isEditing` back to `false`. No `FormData` is built, nothing is submitted — the form's local field state is simply discarded when `TodoForm` unmounts (edit mode conditionally renders `TodoForm`, per [Edit Mode Toggle](#edit-mode-toggle-tododetailroutetsx)), and `<TodoDetail />` re-renders from the loader's still-unchanged `todo`.
- **No "discard unsaved changes?" confirmation** — per [Scope](#scope), Cancel is a plain, un-guarded discard. A future spec can add a confirm-before-cancel if dropped edits turn out to be a real usability complaint; not assumed here.

## Dirty Tracking (Save button enablement)

The behavior this spec exists to define, per the user's own requirement: **Save starts disabled and becomes enabled only when the form's current title, description, or due-date value differs from the todo's originally-loaded value for that same field.**

- `TodoForm` (edit mode) holds each field's current value as local controlled-input state (`useState`), initialized from the `todo` prop's `title`/`description`/`dueDate` when edit mode is entered. This is the same controlled-input shape `TodoForm`'s add mode already uses for its three fields — edit mode doesn't introduce a second input pattern, it reuses the existing one and adds a comparison layer on top.
- On every render, each current field value is compared against the corresponding **original** value — the `todo` prop's `title`/`description`/`dueDate` as loaded, captured once when edit mode is entered and not updated again until the next loader run (i.e., not re-captured on every keystroke). String fields (`title`, `description`) compare by simple equality, with the same `""` ↔ `null` normalization `TodoListRoute.action.ts`'s existing `addTodo` action already applies when building a request body (per [`main-route.md#action-addtodo`](main-route.md#action-addtodo)) — an empty `description` field and a `null` original `description` are **not** considered a difference, since they represent the same "no description" state. `dueDate` compares the form's raw `datetime-local` string against the original value formatted into that same `datetime-local` shape (so a value that round-trips through the input unchanged doesn't register as dirty due to a formatting mismatch alone — see [Due Date Comparison](#due-date-comparison) below).
- **Save is enabled (`disabled={false}`) when at least one of the three fields differs from its original value, using the rules above; disabled otherwise** — including immediately after entering edit mode (no field has changed yet) and immediately after a field is edited back to match its original value (e.g. typing a character into `title` then deleting it).
- This comparison is purely client-side, computed on every render from current vs. original field state — no network round-trip, no debounce; the fields are small enough (per the backend's own `200`/`2000` character max-length bounds) that a straightforward per-render string comparison is not a performance concern.
- **Not the same thing as client-side validation.** A field can be dirty (differs from original) and simultaneously invalid (e.g. `title` edited down to an empty string) — see [`TodoForm.tsx`](#todoformtsx-changes) for how the two checks combine to gate Save.

### Due Date Comparison

`todo.dueDate` (from `TodoResponse`, an ISO 8601 string or `null`) and the `<input type="datetime-local">`'s value (a local-time string with no offset, e.g. `"2026-09-26T14:30"`, per [`main-route.md#due-date-date--time`](main-route.md#due-date-date--time)) are not the same string shape, so a direct string comparison between them would always register as "changed" even when the user hasn't touched the field. To compare correctly:

- On entering edit mode, `todo.dueDate` (if non-`null`) is formatted into the same `datetime-local`-compatible local-time string shape the input itself produces/accepts — this becomes both the input's initial value **and** the "original" value dirty-tracking compares against. A `null` `todo.dueDate` becomes an empty string as the original value, matching the input's own empty-value representation.
- From that point on, dirty-tracking for `dueDate` is a plain string comparison between the input's current value and this formatted original — same treatment as `title`/`description`, no special-casing needed once the initial format-in step is done.
- This formatting step is a small, local piece of logic in `TodoForm` (not a new shared date-formatting helper) — it's the inverse direction of the existing `toLocaleString()` display formatting `TodoDetail`/`TodoList` already do, but produces a machine-format (`datetime-local`-compatible) string rather than a human-readable one, so it isn't the same function and doesn't create the "should this be extracted" question [`detail-route.md`'s Open Questions](detail-route.md#open-questions) raised for `toLocaleString()` display calls.

## `TodoForm.tsx` Changes

`TodoForm` already exists (add mode only, exercised) per [`main-route.md#todoformtsx`](main-route.md#todoformtsx). This spec exercises and extends its previously-unused edit mode:

- **New props for edit mode:** `todo: TodoResponse` (the current values to pre-fill and diff against) and `onCancel: () => void`. Both are `undefined`/unused in add mode, same as `intent="add"` today doesn't pass them.
- **Pre-fills fields from `todo`** when `intent === "edit"`, instead of the empty defaults add mode uses.
- **Renders a Cancel button** alongside Save, only in edit mode — add mode has no equivalent "discard and go back" affordance today (it just stays on `/` regardless), so this is edit-mode-only UI, not a change to add mode's rendered buttons.
- **Save button (`type="submit"`) is `disabled` when either:**
  1. No field differs from its original value (per [Dirty Tracking](#dirty-tracking-savebutton-enablement)), **or**
  2. The form fails its existing client-side checks (required `title`, max lengths — the same checks add mode already runs, per [`main-route.md#todoformtsx`](main-route.md#todoformtsx)).

  Both conditions are evaluated on every render from current field state; either one alone is enough to keep Save disabled. In add mode, only condition 2 applies (there's no "original" to diff against when creating) — dirty-tracking is edit-mode-only behavior, add mode's existing disable-while-invalid behavior is unchanged.
- A hidden `intent` field's value is `"edit"` in edit mode (`"add"` in add mode, unchanged) — continuing the existing per-render intent-field convention [`main-route.md#todoformtsx`](main-route.md#todoformtsx) established, now with a second real value instead of the one hardcoded value every prior spec has shipped.
- Still submits via React Router's `<Form method="post">`, matching add mode — no `useSubmit()` needed here despite this route's other two mutations using it, since (unlike Delete/toggle) this is a real form with real fields, exactly the case `<Form>` fits and `useSubmit()` was chosen to avoid for those simpler single-button actions (per [`delete-todo.md#delete-button--dialog-state-tododetailroutetsx`](delete-todo.md#delete-button--dialog-state-tododetailroutetsx)'s reasoning for why it picked `useSubmit()` in the first place — a form with fields is the case that reasoning carved out as the exception).
- Renders field-level error text next to a field when a matching key is present in the error dictionary passed in as a prop — same mechanism add mode already uses via `useActionData()`, per [`main-route.md#todoformtsx`](main-route.md#todoformtsx); `TodoDetailRoute` sources this from its own `useActionData()` the same way `TodoListRoute` already does for add mode.

## Action: `updateTodo`

Extends the existing `TodoDetailRoute.action.ts` (see [`delete-todo.md#action-deletetodo`](delete-todo.md#action-deletetodo), [`toggle-completion.md#action-updatecompletionstatus`](toggle-completion.md#action-updatecompletionstatus)) with a third `intent` branch:

- Reads `id` from `params`, same as the existing branches.
- Reads `intent` from `FormData`; branches to this path when `intent === "edit"`. The existing `delete`/`toggleComplete` branches and the "unknown intent → `400`" fallback are otherwise unchanged.
- Reads `title`, `description`, `dueDate` from `FormData` — same empty-string-to-`null` normalization for `description`/`dueDate` that `TodoListRoute.action.ts`'s `addTodo` branch already applies (per [`main-route.md#action-addtodo`](main-route.md#action-addtodo)), reused as-is rather than reinvented for this branch.
- **Reads `isCompleted` from `params`/loader data, not from `FormData`** — the form has no `isCompleted` field (per [`isCompleted` on a Full-Replacement `PUT`](#iscompleted-on-a-full-replacement-put)), so the action loads the todo's current `isCompleted` the same way the rest of this route already has it available. Since `TodoDetailRoute.action.ts` doesn't itself call the loader, this value is threaded through as a hidden `currentIsCompleted` field in the submitted `FormData` (set from `todo.isCompleted` when `TodoForm` renders in edit mode) rather than the action independently re-fetching the todo — avoids an extra `getTodoById` round-trip inside the action purely to recover one boolean the page already has loaded.
- Calls the generated `updateTodo(id, { title, description, dueDate, isCompleted: currentIsCompleted })`.
- On `status === 200`: returns `null` — same same-route "loader re-run reflects the change automatically" pattern [`toggle-completion.md#action-updatecompletionstatus`](toggle-completion.md#action-updatecompletionstatus) already established for this route's other in-place mutation. `TodoDetailRoute` reads the loader re-run (fresh `todo`) and flips `isEditing` back to `false` — see [Returning to Read-Only After Save](#returning-to-read-only-after-save).
- On `status === 400`: returns the parsed `ValidationProblemDetails` body (via the existing `lib/problemDetails.ts` from [`main-route.md#libproblemdetailsts`](main-route.md#libproblemdetailsts)) as action data — **not thrown** — so the form stays open with the user's in-progress edits intact and field errors attached via `useActionData()`, same "don't unmount the user's input on a validation failure" reasoning [`main-route.md#action-addtodo`](main-route.md#action-addtodo) already applied to the add form. `isEditing` stays `true` in this case — a `400` does not flip the route back to read-only.
- On `status === 404`: **the edit-after-delete race** — the todo existed when the page loaded (or when edit mode was entered) but was deleted by something else before Save was clicked. Same reasoning as [`delete-todo.md#action-deletetodo`](delete-todo.md#action-deletetodo)'s and [`toggle-completion.md#action-updatecompletionstatus`](toggle-completion.md#action-updatecompletionstatus)'s identical `404` cases: the detail page for a nonexistent todo can't usefully re-render, so this returns `redirect("/")` rather than surfacing an error. No error message is shown for this case, consistent with both prior mutations' identical `404` handling.
- Any other status: throws a `Response` with that status, same "no other documented failure mode, but still explicit" posture as every prior action branch in this directory.

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
    // existing branch, unchanged — see toggle-completion.md
  }

  if (intent === 'edit') {
    const title = formData.get('title') as string
    const description = (formData.get('description') as string) || null
    const dueDate = (formData.get('dueDate') as string) || null
    const isCompleted = formData.get('currentIsCompleted') === 'true'

    const result = await updateTodo(id, { title, description, dueDate, isCompleted })

    if (result.status === 200) {
      return null
    }

    if (result.status === 400) {
      return parseValidationProblem(result.data)
    }

    if (result.status === 404) {
      return redirect('/')
    }

    throw new Response('Failed to update todo', { status: result.status })
  }

  throw new Response('Unknown action', { status: 400 })
}
```

## Returning to Read-Only After Save

Unlike `toggleComplete` (which never leaves the read-only view), a successful `edit` submission needs `isEditing` to flip back to `false` so the page returns to `<TodoDetail />`. Since `isEditing` is local component state and the action only returns `null`/validation errors (no explicit "it worked" signal beyond "no action-data errors present"), `TodoDetailRoute` derives this from `useNavigation()`: when a `submit` with `intent: "edit"` transitions from `navigation.state === "submitting"` back to `"idle"` **and** `useActionData()` holds no validation errors (i.e., the `400` branch didn't just run), `isEditing` is set back to `false` in a `useEffect` keyed on `navigation.state`. This is the one piece of this spec that isn't a straightforward reuse of an existing pattern from the prior three specs in this directory — flagged in [Open Questions](#open-questions) as the detail most worth revisiting during implementation if a cleaner React Router idiom for "was this particular submission's specific intent the one that just succeeded" turns up.

## `router.tsx`

No changes — the `/todos/:id` entry already has `action: todoDetailAction` wired from [`delete-todo.md#routertsx`](delete-todo.md#routertsx); this spec's new intent branch lives inside that same function.

## Testing

Per [`../../architecture/frontend/overview.md#testing`](../../architecture/frontend/overview.md#testing), same split prior specs in this directory established:

- **`tests/components/TodoForm.test.tsx`** — extended with edit-mode cases, RTL, in isolation:
  - Rendering with `intent="edit"` and a `todo` prop pre-fills all three fields with that todo's current values.
  - Save is disabled immediately after render (no field changed yet).
  - Editing `title` (a single keystroke change from the original) enables Save; editing it back to exactly the original value disables Save again.
  - Editing `description` from a non-empty original to empty, and separately from a `null` original to an empty string, are each tested for their expected dirty state (the latter case must **not** enable Save, per [Dirty Tracking](#dirty-tracking-savebutton-enablement)'s `""`/`null` normalization).
  - Editing `dueDate` to a different value enables Save; leaving it untouched keeps Save disabled (covers [Due Date Comparison](#due-date-comparison)'s format-matching requirement — this is the case most likely to break if the round-trip formatting is wrong).
  - A dirty `title` field edited to empty (invalid) keeps Save disabled despite being dirty — covers condition 2 of [`TodoForm.tsx`](#todoformtsx-changes)'s Save-disable logic.
  - Clicking Cancel (edit mode only) calls `onCancel` and submits nothing.
  - A passed-in field error (edit mode) renders next to the matching field, same assertion pattern add mode's existing test already uses.
- **`tests/routes/TodoDetailRoute.test.tsx`** — extended with edit-flow cases, RTL + `createMemoryRouter` + MSW:
  - Clicking Edit hides `TodoDetail`/Delete/toggle and shows the pre-filled form; Delete and the toggle are not present while editing.
  - Clicking Cancel returns to the read-only view with the original values still displayed (no `PUT` request made — same "assert via a handler override that fails the test if called" pattern [`delete-todo.md#testing`](delete-todo.md#testing) uses for its equivalent Cancel-analog, the No button).
  - Changing `title` and clicking Save issues `PUT /todos/:id` with the updated `title` and the todo's unchanged `description`/`dueDate`/`isCompleted`; after the loader re-runs, the read-only view shows the new `title` and `isEditing` has returned to `false`.
  - Saving with an empty `title` against a mocked `400` response keeps the form open and shows the returned field error via `useActionData()`.
  - Clicking Save while a mocked `PUT` handler returns `404` navigates to `/`, same assertion pattern the equivalent race-case tests in `delete-todo.md`/`toggle-completion.md` use.
  - The form's fields and Save/Cancel buttons are disabled while a submission is in flight (same `navigation.state`-based assertion approach as prior specs).
- **`tests/mocks/handlers.ts`** — add a `PUT /todos/:id` handler: updates the matching in-memory todo's `title`/`description`/`dueDate`/`isCompleted` and `updatedAt`, and returns `200` with the updated `TodoResponse`; returns `400` with a `ValidationProblemDetails` body when the request's `title` is empty (mirrors the existing `POST /todos` handler's validation-failure case from [`main-route.md#testing`](main-route.md#testing)); returns `404` if the id isn't found — mirrors the existing `DELETE`/`PATCH /todos/:id` handlers' lookup logic added in [`delete-todo.md`](delete-todo.md#testing)/[`toggle-completion.md`](toggle-completion.md#testing).

No Playwright/E2E — out of scope project-wide, same as every prior spec.

## Wiring Checklist

- [ ] `src/components/TodoForm.tsx`: add `todo`/`onCancel` props, pre-fill logic, dirty-tracking state/comparison per [Dirty Tracking](#dirty-tracking-savebutton-enablement) and [Due Date Comparison](#due-date-comparison), Cancel button, Save `disabled` logic per [`TodoForm.tsx` Changes](#todoformtsx-changes).
- [ ] `src/routes/TodoDetailRoute.tsx`: add Edit button, `isEditing` state, conditional rendering of `TodoForm`/`TodoDetail`+Delete+toggle, the `useNavigation()`-driven return-to-read-only effect per [Returning to Read-Only After Save](#returning-to-read-only-after-save).
- [ ] `src/routes/TodoDetailRoute.action.ts`: add the `edit` intent branch per [Action: `updateTodo`](#action-updatetodo).
- [ ] `tests/mocks/handlers.ts`: add a `PUT /todos/:id` MSW handler.
- [ ] `tests/components/TodoForm.test.tsx`, `tests/routes/TodoDetailRoute.test.tsx`: extend with the cases in [Testing](#testing).
- [ ] Update `README.md` per [`CLAUDE.md`'s sync rule](../../../CLAUDE.md#keep-readmemd-in-sync) once implemented — the Update operation moves from "not yet available" to done.

## Open Questions

- **The `useNavigation()`-based "did this specific edit submission just succeed" detection in [Returning to Read-Only After Save](#returning-to-read-only-after-save).** This is the one mechanism in this spec without a direct precedent in the three prior mutation specs (each of which either always redirects away, per Delete, or never needs to leave its own view, per the completion toggle). Worth revisiting at implementation time against whatever React Router version actually lands (per [`main-route.md`'s own open question](main-route.md#open-questions) about the exact package/version) in case a newer idiom (e.g. inspecting `navigation.formData` directly, or a `fetcher`-based submission instead of the route's primary `useNavigation()`) is cleaner than the effect described here.
- **Whether the hidden `currentIsCompleted` field in `FormData` (see [Action: `updateTodo`](#action-updatetodo)) is the right mechanism**, versus the action independently calling `getTodoById` before building the `PUT` body. The hidden-field approach avoids an extra network round-trip but does mean the submitted `isCompleted` reflects whatever `isCompleted` value the page had loaded at the time edit mode was entered — if the completion status changed through some other path between then and Save (not currently possible in this single-tab, single-user app, but noted for completeness), the hidden field would be stale. Not a real risk given [`../../architecture/authentication.md`](../../architecture/authentication.md)'s single-user, no-concurrent-client phase 1 scope, but flagged here rather than silently assumed away.
- **Whether `title`/`description`/`dueDate` max-length client-side checks should visually distinguish "invalid" from "unchanged" as reasons Save is disabled.** This spec's [`TodoForm.tsx` Changes](#todoformtsx-changes) OR's the two conditions into one `disabled` boolean with no UI distinguishing which reason applies — a user with a dirty-but-invalid field sees the same disabled Save button as a user with no changes at all. Not addressed here, consistent with this project's "no visual/interaction polish pass" scope exception carried by every prior spec in this directory; revisit if Foci's review flags it as confusing.

## Related Docs

- [`main-route.md`](main-route.md) — `TodoForm.tsx`'s original add-mode build, its existing client-side validation checks and error-display mechanism this spec's edit mode reuses, and the `addTodo` action's `""`/`null` normalization this spec's `edit` branch also applies
- [`detail-route.md`](detail-route.md) — the read-only detail route this spec adds edit-mode UI to; `TodoDetail`'s field rendering this spec's edit mode temporarily replaces
- [`delete-todo.md`](delete-todo.md) — the first mutation added to `TodoDetailRoute`; the `intent`-branching action and per-route action-file convention this spec's third branch continues
- [`toggle-completion.md`](toggle-completion.md) — the second mutation added to `TodoDetailRoute`; the same-route loader-re-run-after-`200`, shared `navigation.state` submitting guard, and `404`-race-becomes-redirect conventions this spec's `edit` branch reuses directly
- [`../backend/update-todo/spec.md`](../backend/update-todo/spec.md) — backend `PUT /todos/{id}` contract this action calls, including the full-replacement `isCompleted`-required contract [`isCompleted` on a Full-Replacement `PUT`](#iscompleted-on-a-full-replacement-put) reconciles with this spec's three-field-only form, and the `200`/`400`/`404` shape this spec's action branches on
- [`../../architecture/frontend/overview.md`](../../architecture/frontend/overview.md#one-action-per-route-dispatched-by-an-intent-field) — the "one action per route, dispatched by an intent field" pattern this spec's third `intent` branch continues
- [`../../requirements/requirements.md`](../../requirements/requirements.md) — source **Update** requirement
