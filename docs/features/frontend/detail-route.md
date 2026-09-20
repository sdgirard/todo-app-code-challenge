# Feature Spec: Detail Route — View Only (`/todos/:id`, `TodoDetailRoute`)

Second frontend feature. This is the requirements' **View** operation — "show details of a specific to-do item by its ID." Follows [`../../architecture/frontend/overview.md`](../../architecture/frontend/overview.md) and builds directly on [`main-route.md`](main-route.md), which already wired the generated API client, React Router, and the `TodoList` title link that points at this route (`Link to={\`/todos/${todo.id}\`}` in `TodoList.tsx`). The backend contract this route consumes is already fully built — [`../backend/get-todo-by-id/spec.md`](../backend/get-todo-by-id/spec.md) (`GET /todos/{id}`) — and its response is the same shared `TodoResponse` DTO `main-route.md` already generates a client type for.

This is a **spec, not an implementation** — no frontend code is written yet, matching how [`main-route.md`](main-route.md) and the backend's per-endpoint specs were written before their code.

## Scope

In scope:

- The `/todos/:id` route (`TodoDetailRoute`): loader fetches a single todo (`GET /todos/{id}`), page renders its full details **read-only**.
- Route registration in `router.tsx` alongside the existing `/` route.
- A route-level `errorElement` for the 404 case — the first real, reachable error case in the app (per [`main-route.md`'s note](main-route.md#loader-listtodos) that this was deferred until one existed).
- Route-level and component-level tests for everything above.

Out of scope (future specs):

- **Any mutation from this page** — no Edit, no Complete/Incomplete toggle, no Delete. The requirements' Update, Complete/Incomplete, and Delete operations are not implemented here even though their backend endpoints already exist ([`../backend/update-todo/spec.md`](../backend/update-todo/spec.md), [`../backend/update-completion-status/spec.md`](../backend/update-completion-status/spec.md), [`../backend/delete-todo/spec.md`](../backend/delete-todo/spec.md)). This route has no `action`, and `TodoForm.tsx`'s edit mode (already scaffolded per [`../../architecture/frontend/overview.md#directory-structure`](../../architecture/frontend/overview.md#directory-structure)) is not used here.
- A "back to list" navigation affordance is included as a plain `Link` (see [`TodoDetailRoute.tsx`](#tododetailroutetsx)) but no broader navigation/layout chrome — no shared header/nav component is introduced by this spec.
- Any styling beyond plain Tailwind utility classes on semantic HTML — no visual design pass, same exception as `main-route.md`.

## API Client

No new generation step — `orval` already generated `getTodoById` and the shared `TodoResponse` model in [`main-route.md`'s API Client Generation](main-route.md#api-client-generation-orval) pass, ahead of use. This spec is simply the first caller.

- `getTodoById(id: string): Promise<{ data: TodoResponse | void; status: 200 | 404; ... }>` → `GET /todos/{id}`, from `src/api/generated/todos/todos.ts`.
- Same non-throwing contract as every other generated call ([`main-route.md`](main-route.md#api-client-generation-orval)): a `404` resolves normally with `status: 404` and `data: void`, it does not reject/throw. This route's loader must branch on `status` explicitly, same as `TodoListRoute.loader.ts` already does.

## Directory/File Plan

New files this spec introduces (subset of the full tree in [`../../architecture/frontend/overview.md#directory-structure`](../../architecture/frontend/overview.md#directory-structure) — only what `/todos/:id` needs; everything else already exists per [`main-route.md`](main-route.md)):

```text
frontend/
├── src/
│   ├── router.tsx                        # add the "/todos/:id" route
│   ├── routes/
│   │   ├── TodoDetailRoute.tsx
│   │   ├── TodoDetailRoute.loader.ts
│   │   └── ErrorBoundary.tsx             # first real use — see Error Handling
│   └── components/
│       └── TodoDetail.tsx
└── tests/
    ├── mocks/
    │   └── handlers.ts                   # add a GET /todos/:id handler alongside the existing ones
    ├── routes/
    │   └── TodoDetailRoute.test.tsx
    └── components/
        └── TodoDetail.test.tsx
```

No `TodoDetailRoute.action.ts` — this route has no `action`, per [Scope](#scope).

## Loader: `getTodoById`

`TodoDetailRoute.loader.ts`:

- Reads `id` from `params` (`LoaderFunctionArgs`), matching the `:id` route parameter.
- Calls the generated `getTodoById(id)`.
- On `status === 200`: returns `data` (a `TodoResponse`) directly as loader data.
- On `status === 404`: throws `new Response("Todo not found", { status: 404 })`, letting the route's `errorElement` handle it — this is the real, reachable 404 case `main-route.md`'s loader note anticipated when it deferred building an error boundary.
- Any other status: throws a `Response` with that status, same defensive-but-explicit pattern as `TodoListRoute.loader.ts` — no other documented failure mode exists for `GET /todos/{id}` today ([`../backend/get-todo-by-id/spec.md#dto-contract`](../backend/get-todo-by-id/spec.md#dto-contract) only documents `200`/`404`), but the loader still branches on status rather than assuming only those two occur.

No dependency on the malformed-id case backend-side — `../backend/get-todo-by-id/spec.md`'s `{id:guid}` route constraint means a non-`Guid` path segment never reaches the backend's `GetTodoByIdEndpoint` at all and comes back as its own `404`, which this loader's `status === 404` branch already covers without special-casing.

## Error Handling

`ErrorBoundary.tsx` is introduced here — the first spec with a real, reachable error to render (per [`main-route.md`'s deferral note](main-route.md#loader-listtodos)):

- Uses React Router's `useRouteError()` + `isRouteErrorResponse()` to distinguish a thrown `Response` (this route's 404) from an unexpected JS error.
- For a `404` `Response`: renders a simple "Todo not found" message plus a `Link` back to `/`.
- For anything else: renders a generic "Something went wrong" message — no attempt at fine-grained handling of every possible status here, since none of this route's other statuses are expected in practice (see [Loader](#loader-gettodobyid)).
- Registered as this route's `errorElement` in `router.tsx`. The `/` route is **not** retrofitted with this same `errorElement` in this spec — `main-route.md`'s loader still has no real reachable error case, so giving it one now would be speculative; revisit only if `TodoListRoute` gets one.

## `TodoDetail.tsx`

Presentational, takes `todo: TodoResponse` as a prop. Renders all fields relevant to the requirements' View minimum plus what's already useful from the shared DTO:

- **Title** — heading-level text.
- **Description** — body text, or an "No description" placeholder when `null`.
- **Due Date** — `toLocaleString()` (date + time), or "No due date" when `null`, same formatting convention `TodoList.tsx` already uses (see [`main-route.md#due-date-date--time`](main-route.md#due-date-date--time)) — this spec doesn't introduce a second date-formatting convention, it reuses the existing one (extracted to a shared helper if the duplication between `TodoList.tsx` and this component becomes worth removing at implementation time; not mandated by this spec either way).
- **Completion Status** — same "Completed"/"Incomplete" text `TodoList.tsx` already renders per row.
- **Created At** — new to this page (not shown in the list view); `toLocaleString()`.
- **Updated At** — new to this page; `toLocaleString()` when non-null, "Never updated" when `null`.

Purely presentational — no buttons, no form, no interactive elements. Read-only per [Scope](#scope).

## `TodoDetailRoute.tsx`

- Reads loader data via `useLoaderData()` (the `TodoResponse`), passes it to `<TodoDetail />`.
- Renders a `Link to="/"` back to the list — the only navigation affordance this spec adds.
- No `useActionData()`, no `useNavigation()` submitting-state handling — nothing on this page submits anything.

## `router.tsx`

Extends the array from [`main-route.md#routertsx`](main-route.md#routertsx):

```ts
createBrowserRouter([
  { path: "/", element: <TodoListRoute />, loader: todoListLoader, action: todoListAction },
  { path: "/todos/:id", element: <TodoDetailRoute />, loader: todoDetailLoader, errorElement: <ErrorBoundary /> },
])
```

## Testing

Per [`../../architecture/frontend/overview.md#testing`](../../architecture/frontend/overview.md#testing), same split `main-route.md` established:

- **`tests/components/TodoDetail.test.tsx`** — RTL, in isolation: renders all six fields from a given `TodoResponse`; renders "No description" / "No due date" / "Never updated" placeholders when those fields are `null`.
- **`tests/routes/TodoDetailRoute.test.tsx`** — RTL + `createMemoryRouter` + MSW (`tests/mocks/handlers.ts`, extended with a `GET /todos/:id` handler): rendering with a valid id shows the mocked todo's details; rendering with an id the mock resolves to `404` renders the error boundary's "Todo not found" message instead of the detail view; the back-to-list `Link` points at `/`.
- **`tests/mocks/handlers.ts`** — add the `GET /todos/:id` handler this spec's tests need; the other three not-yet-consumed operations (`PUT`, `PATCH`, `DELETE`) are still deferred to whichever future spec first calls them.

No Playwright/E2E — out of scope project-wide, same as `main-route.md`.

## Wiring Checklist

- [ ] `src/routes/TodoDetailRoute.loader.ts`: new file — loader per [Loader](#loader-gettodobyid).
- [ ] `src/routes/TodoDetailRoute.tsx`: new file — reads loader data, renders `<TodoDetail />` and a back-to-list link.
- [ ] `src/routes/ErrorBoundary.tsx`: new file — per [Error Handling](#error-handling).
- [ ] `src/components/TodoDetail.tsx`: new file — presentational, per [`TodoDetail.tsx`](#tododetailtsx).
- [ ] `src/router.tsx`: add the `/todos/:id` route entry with its `loader` and `errorElement`.
- [ ] `tests/mocks/handlers.ts`: add a `GET /todos/:id` MSW handler.
- [ ] `tests/components/TodoDetail.test.tsx`, `tests/routes/TodoDetailRoute.test.tsx`: new test files per [Testing](#testing).
- [ ] Update `README.md` per [`CLAUDE.md`'s sync rule](../../../CLAUDE.md#keep-readmemd-in-sync) once implemented — the View operation moves from "not yet available" to done.

## Open Questions

- **Whether `TodoDetail.tsx`'s date formatting should be extracted into a shared helper with `TodoList.tsx`.** Both components independently call `toLocaleString()` on `dueDate` today; this spec doesn't mandate extraction, since two call sites doing the same one-line formatting isn't yet enough duplication to justify a shared module (per `CLAUDE.md`'s "three similar lines is better than a premature abstraction" guidance) — revisit if a third call site appears (e.g. `TodoForm`'s edit mode) or if `createdAt`/`updatedAt` formatting needs the same treatment.

## Related Docs

- [`main-route.md`](main-route.md) — the list route this one is linked from; API client generation, error-handling deferral note, and due-date formatting convention this spec builds on
- [`../backend/get-todo-by-id/spec.md`](../backend/get-todo-by-id/spec.md) — backend `GET /todos/{id}` contract this route's loader calls, including the shared `TodoResponse` DTO and the `{id:guid}` route-constraint behavior this loader relies on for its 404 handling
- [`../../architecture/frontend/overview.md`](../../architecture/frontend/overview.md) — overall frontend design this spec implements a slice of
- [`../../requirements/requirements.md`](../../requirements/requirements.md) — source **View** requirement
