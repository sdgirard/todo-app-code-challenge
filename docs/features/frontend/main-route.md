# Feature Spec: Main Route — List + Add (`/`, `TodoListRoute`)

First frontend feature built, and the first time the generated API client is actually wired into the app. Follows [`../../architecture/frontend/overview.md`](../../architecture/frontend/overview.md); the backend contract this route consumes is already fully built (see [`../../architecture/backend/overview.md`](../../architecture/backend/overview.md)) and its `openapi.json` is already committed at `backend/src/TodoApi.Gateway/openapi.json`.

This is a **spec, not an implementation** — no frontend code is written yet. It locks in scope, the client-generation setup, the route's data shape, and the component breakdown so the implementation pass has a settled plan to follow, mirroring how the backend's [`add-todo/spec.md`](../backend/add-todo/spec.md) was written before `AddTodo` was coded.

## Scope

In scope:

- Generating the TypeScript API client (orval) from the committed backend `openapi.json`, wired as a frontend pre-build step.
- Installing and wiring React Router (`createBrowserRouter`, `RouterProvider`) — not present in the scaffold today.
- The `/` route (`TodoListRoute`): loader fetches the full todo list (`GET /todos`), page renders the list and an inline add-todo form (`POST /todos`).
- The shared `lib/problemDetails.ts` helper, since the add form's action needs it the first time a `400 ValidationProblem` can occur.
- Route-level and component-level tests for everything above.

Out of scope (future specs):

- `/todos/:id` (`TodoDetailRoute`) — View, Update, Complete/Incomplete, Delete. This spec's loader/action patterns are written to be reused there, but that route isn't built here.
- Any styling beyond plain Tailwind utility classes on semantic HTML — no visual design pass (per [`../../architecture/frontend/overview.md#styling`](../../architecture/frontend/overview.md#styling), polish isn't evaluated).
- Production/demo API base URL and CORS origin — still deferred per [`../../architecture/frontend/overview.md#api-base-url--local-dev-origin`](../../architecture/frontend/overview.md#api-base-url--local-dev-origin); this spec only needs the local dev value.

## API Client Generation (orval)

Not wired in yet — the frontend `package.json` has no `orval` dependency and no `generate:api` script today. This spec is what turns that on.

- **Dependency:** add `orval` as a frontend dev dependency.
- **Config:** `frontend/orval.config.ts`, pointing input at `../backend/src/TodoApi.Gateway/openapi.json` and output at `src/api/generated/`, using orval's **fetch client** mode (not React Query hooks — no query library in this app, per [`../../architecture/frontend/overview.md#approach`](../../architecture/frontend/overview.md#approach)).
- **Build wiring:** `npm run generate:api` runs orval; wired as `predev` and `prebuild` scripts in `package.json` so `npm run dev` / `npm run build` always regenerate first. Matches the backend's own build-time (not on-demand) OpenAPI generation.
- **Committed output:** `src/api/generated/` is committed, not gitignored — same rationale as the backend's `openapi.json` (drift is visible in review as a diff, and the frontend builds/tests without the backend running).
- **Expected generated surface**, based on the current committed spec's four `/todos*` operations relevant to this route:
  - `listTodos(): Promise<{ data: TodoResponse[] | void; status: number; ... }>` → `GET /todos`
  - `addTodo(body: AddTodoEndpointRequest): Promise<{ data: TodoResponse | void; status: number; ... }>` → `POST /todos`
  - Plus `getTodoById`, `updateTodo`, `updateCompletionStatus`, `deleteTodo` — generated now (they're in the same spec file) but not called until `TodoDetailRoute`'s spec.
  - Generated model types: `TodoResponse`, `AddTodoEndpointRequest` (matches the backend's actual DTOs — see [`AddTodoEndpoint.Request`/`Response` history in `add-todo/spec.md`](../backend/add-todo/spec.md#dto-contract), now unified into the shared `TodoResponse`).
- **As documented in the architecture doc, the generated client never throws** on a non-2xx response — it always resolves `{ data, status }`. This route's loader/action must check `status` explicitly (see [Loader](#loader-listtodos) and [Action](#action-addtodo) below); nothing here can rely on a caught exception.

## Directory/File Plan

New files this spec introduces (subset of the full tree in [`../../architecture/frontend/overview.md#directory-structure`](../../architecture/frontend/overview.md#directory-structure) — only what `/` needs):

```text
frontend/
├── orval.config.ts
├── src/
│   ├── router.tsx                        # createBrowserRouter with the "/" route only, for now
│   ├── api/
│   │   └── generated/                    # orval output
│   ├── routes/
│   │   ├── TodoListRoute.tsx
│   │   ├── TodoListRoute.loader.ts
│   │   ├── TodoListRoute.action.ts
│   ├── components/
│   │   ├── TodoList.tsx
│   │   └── TodoForm.tsx
│   └── lib/
│       └── problemDetails.ts
└── tests/
    ├── setup.ts                          # add MSW server lifecycle hooks to existing RTL setup
    ├── mocks/
    │   └── handlers.ts                   # MSW handlers for GET/POST /todos only, for now
    ├── routes/
    │   └── TodoListRoute.test.tsx
    ├── components/
    │   ├── TodoList.test.tsx
    │   └── TodoForm.test.tsx
    └── lib/
        └── problemDetails.test.ts
```

`main.tsx` changes from rendering `<App />` directly to `<RouterProvider router={router} />`; `App.tsx` (the Vite scaffold placeholder) is deleted — nothing in the real app needs it.

## Loader: `listTodos`

`TodoListRoute.loader.ts`:

- Calls the generated `listTodos()`.
- On `status === 200`: returns `data` (a `TodoResponse[]`) directly as loader data.
- On any other status: throws a `Response` (e.g. `throw new Response("Failed to load todos", { status })`), letting the route's `errorElement` handle it — per [`../../architecture/frontend/overview.md#error-handling`](../../architecture/frontend/overview.md#error-handling). In practice `GET /todos` has no documented non-200 response today (no path params to 404 on), so this is defensive rather than an expected path — still written explicitly rather than assumed away, matching this route's job of being the template the detail route's loader (which *does* have a real 404 case) will copy.
- No error boundary component is built in this spec (that's `ErrorBoundary.tsx`, deferred to whichever spec first has a real, reachable error case) — the loader still throws correctly, but the route is registered without an `errorElement` for now, so an unhandled loader error falls back to React Router's default error rendering. Revisit when `TodoDetailRoute`'s 404 case makes a real error boundary worth building.

## Action: `addTodo`

`TodoListRoute.action.ts`:

- Reads `FormData` from the request: `title`, `description`, `dueDate`.
- Builds the request body: `title` required as-is; `description` sent as `null` if the field was left empty (not an empty string — matches the backend's `string?`); `dueDate` sent as `null` if left empty, otherwise the raw value from the form's `<input type="datetime-local">` (e.g. `"2026-09-25T14:30"`) passed straight through with no reformatting — `System.Text.Json` parses it directly into the backend's `DateTime?` (per [`add-todo/spec.md#todomodel-domain-model`](../backend/add-todo/spec.md#todomodel-domain-model), `DueDate` was deliberately typed wider than the requirement's date-only minimum specifically to allow an optional time component).
- Calls the generated `addTodo(body)`.
- On `status === 201`: returns `redirect("/")`. Since the action and loader share the same route, React Router re-runs the loader automatically — no manual refetch, matching [`../../architecture/frontend/overview.md#approach`](../../architecture/frontend/overview.md#approach). (A redirect to the same path still triggers a loader re-run in React Router's data APIs, since it's a fresh navigation.)
- On `status === 400`: returns the parsed `ValidationProblemDetails` body (via `lib/problemDetails.ts`) as action data — **not thrown** — so `TodoListRoute` re-renders with the add form's input intact and field errors attached via `useActionData()`, per [`../../architecture/frontend/overview.md#error-handling`](../../architecture/frontend/overview.md#error-handling).
- Any other status: treated the same as the 400 path for now (returned, not thrown) — there's no other documented failure mode for `POST /todos` today, but throwing here would unmount the user's in-progress form input, which is the wrong UX regardless of the specific status code.

## `lib/problemDetails.ts`

Introduced here (first spec to need it) per [`../../architecture/frontend/overview.md#error-handling`](../../architecture/frontend/overview.md#error-handling):

- Narrows an unknown response body to the RFC 9457 shape structurally (checks for `title`/`status`/`errors` fields) rather than trusting the generated client's type — the generated client types error-status `data` as `void` even though the real body is present at runtime (documented gap in the architecture doc).
- Exports a function that extracts a per-field message dictionary from `ValidationProblemDetails.errors` (backend's actual failure shape for `AddTodo`, per [`add-todo/spec.md#dto-contract`](../backend/add-todo/spec.md#dto-contract)) for the add form to key off of (e.g. `errors.Title`).
- Exports a function that extracts a single top-level `detail`/`title` message for non-field-specific errors.

## Components

### `TodoList.tsx`

Presentational, takes `todos: TodoResponse[]` as a prop. Renders Title, Due Date, and Completion Status per item, per the requirement's List minimum (`../../requirements/requirements.md`, "List" row). Due Date renders both date and time (`toLocaleString()`, not `toLocaleDateString()`) — see [Due Date: Date + Time](#due-date-date--time) below — since a `dueDate` can now carry a meaningful time component, not just a day. Each row links to `/todos/:id` (`TodoDetailRoute`) for View — the link target exists in this spec even though the route it points to doesn't yet; React Router renders it as a plain anchor regardless of whether the destination route is registered.

An empty list renders a simple "No to-dos yet" message rather than an empty table — no todos exist until the add form is used at least once.

### `TodoForm.tsx`

Shared add/edit form per [`../../architecture/frontend/overview.md#directory-structure`](../../architecture/frontend/overview.md#directory-structure) (this spec only exercises its **add** mode; edit mode is exercised later by `TodoDetailRoute`). Fields: `title` (text, required), `description` (textarea, optional), `dueDate` (**`<input type="datetime-local">`**, optional — date *and* time, not date-only; see [Due Date: Date + Time](#due-date-date--time) below). A hidden `intent` field (value `"add"` for this route) is included now even though `TodoListRoute`'s action only ever handles one intent — matching the shared-form contract the detail route will need later (per [`../../architecture/frontend/overview.md#one-action-per-route-dispatched-by-an-intent-field`](../../architecture/frontend/overview.md#routes--backend-operations)), rather than adding it retroactively once the detail route needs multi-intent dispatch.

Client-side checks (required `title`, reasonable max lengths matching the backend's `200`/`2000` bounds from [`add-todo/spec.md#validation-rules-service-tier--requestvalidator`](../backend/add-todo/spec.md#validation-rules-service-tier--requestvalidator)) run before submit purely for fast feedback — not the source of truth, per [`../../architecture/frontend/overview.md#data-flow-write-path`](../../architecture/frontend/overview.md#data-flow-write-path). Renders field-level error text next to a field when a matching key is present in the error dictionary passed in as a prop (sourced from `useActionData()` in `TodoListRoute`).

Submits via React Router's `<Form method="post">`, not a manual `fetch`/`onSubmit` handler — so submission goes through the route's `action` automatically.

## Due Date: Date + Time

**`dueDate` is picked as a date *and* time, not date-only** — a deliberate divergence from the requirement's `YYYY-MM-DD`-is-sufficient minimum, made possible because the backend's `TodoModel.DueDate` was already typed `DateTime?` (not `DateOnly?`) specifically to leave room for this (per [`add-todo/spec.md#todomodel-domain-model`](../backend/add-todo/spec.md#todomodel-domain-model)). No backend change is needed to support it.

- **Input:** `<input type="datetime-local">` in `TodoForm`, not `type="date"`. Its value is a local-time string with no timezone offset (e.g. `"2026-09-25T14:30"`, seconds omitted when `:00`) — sent to the backend as-is in the request body, with no client-side reformatting or timezone conversion.
- **Wire format:** the backend accepts this directly — `System.Text.Json` parses any ISO 8601 `DateTime?`, and a bare `"2026-09-25T14:30"` (no `Z`/offset) deserializes to `DateTimeKind.Unspecified`, consistent with the round-trip behavior already documented in [`add-todo/spec.md#todomodel-domain-model`](../backend/add-todo/spec.md#todomodel-domain-model) ("Serialization detail"). No new backend behavior — the endpoint already accepted a full timestamp, this just means the frontend is now the first caller to actually send one.
- **Validation:** the backend's past-due check (`DueDate >= DateTime.UtcNow.Date`, per [`add-todo/spec.md#validation-rules-service-tier--requestvalidator`](../backend/add-todo/spec.md#validation-rules-service-tier--requestvalidator)) compares against **today's date**, not the current instant — so a `dueDate` with today's date and any time-of-day (including one earlier than the current time) still passes. This spec doesn't change that rule or add a client-side mirror of it beyond the existing required/max-length checks; a same-day-but-already-past-time value is accepted by design on the backend as-is.
- **Display:** `TodoList` renders `dueDate` with `Date.prototype.toLocaleString()` (date + time, viewer's local timezone) instead of `toLocaleDateString()` — since a `dueDate` now routinely carries a non-midnight time, showing only the date would silently drop information the user just entered.
- **No timezone handling beyond the browser's own default.** `datetime-local`'s value has no offset, and `toLocaleString()` renders in whatever timezone the browser is running in — the same "trust the value as given, no server-side timezone conversion" convention the backend already documents for `DueDate`. Multi-timezone correctness is out of scope for this single-user, no-auth phase (per [`../../architecture/authentication.md`](../../architecture/authentication.md)).

## `TodoListRoute.tsx`

- Reads loader data via `useLoaderData()` (the `TodoResponse[]`), passes it to `<TodoList />`.
- Reads action data via `useActionData()` (the parsed validation errors, if the last submit failed), passes relevant field errors to `<TodoForm />`.
- Renders `<TodoForm intent="add" />` above or below the list (no design decision beyond "both are visible on one page" — no separate add screen, per [`../../architecture/frontend/overview.md#no-standalone-add-or-edit-route`](../../architecture/frontend/overview.md#no-standalone-add-or-edit-route)).
- Uses `useNavigation()` to disable the submit button while the add action is in flight, giving basic feedback without introducing any extra state library.

## `router.tsx`

`createBrowserRouter([{ path: "/", element: <TodoListRoute />, loader: todoListLoader, action: todoListAction }])` — single entry for now. `/todos/:id` is added here in the detail route's own spec, not stubbed in this one.

## Testing

Per [`../../architecture/frontend/overview.md#testing`](../../architecture/frontend/overview.md#testing):

- **`tests/lib/problemDetails.test.ts`** — plain Vitest, no rendering: recognizes a well-formed `ValidationProblemDetails` body, extracts field errors, extracts a top-level `detail`, returns nothing usable for a non-Problem-Details body.
- **`tests/components/TodoForm.test.tsx`** — RTL, in isolation: renders its three fields plus the hidden `intent` field; shows a passed-in field error next to the matching field; client-side check blocks submit on empty `title` and shows a message without hitting the network.
- **`tests/components/TodoList.test.tsx`** — RTL: renders Title/Due Date/Completion Status for each item in a given array; renders the empty-state message for an empty array; each row links to the right `/todos/:id`.
- **`tests/routes/TodoListRoute.test.tsx`** — RTL + `createMemoryRouter` + MSW (`tests/mocks/handlers.ts` stubbing `GET /todos` and `POST /todos`): initial render shows the list from the mocked `GET /todos`; submitting the add form with valid input triggers `POST /todos`, and after redirect the list reflects the mocked post-add `GET /todos` response (loader re-run); submitting with an empty `title` against a mocked `400` response keeps the form visible and shows the returned field error via `useActionData()`.
- **`tests/mocks/handlers.ts`** — MSW handlers for `GET /todos` and `POST /todos` only; handlers for the other four operations are added in the detail route's spec, not pre-built here.

No Playwright/E2E — out of scope project-wide, per [`../../architecture/frontend/overview.md#testing`](../../architecture/frontend/overview.md#testing).

## Local Dev Origin / Base URL

Uses the values already settled in [`../../architecture/frontend/overview.md#api-base-url--local-dev-origin`](../../architecture/frontend/overview.md#api-base-url--local-dev-origin) and already assumed by the backend's CORS policy ([`add-todo/spec.md#cors`](../backend/add-todo/spec.md#cors)): `VITE_API_BASE_URL=https://localhost:7020` in `.env.development`, frontend dev server on `https://localhost:5173` (Vite `server.https` using the shared dev cert). This spec is the first to actually consume that value (baked into the generated client's base URL) rather than just declare it — confirms the earlier architecture-doc decision works end-to-end instead of being an assumption.

## Wiring Checklist

- [ ] `npm install` (frontend): add `orval`, `react-router` (or `react-router-dom`, whichever the current React Router major packages routing under), `msw`.
- [ ] `frontend/orval.config.ts`: input `../backend/src/TodoApi.Gateway/openapi.json`, output `src/api/generated/`, fetch client mode.
- [ ] `package.json`: add `generate:api` script; wire as `predev`/`prebuild`.
- [ ] `.env.development`: `VITE_API_BASE_URL=https://localhost:7020`.
- [ ] `vite.config.ts`: `server.https` using the shared dev cert, per [`../../architecture/frontend/overview.md#api-base-url--local-dev-origin`](../../architecture/frontend/overview.md#api-base-url--local-dev-origin).
- [ ] `src/router.tsx`, `src/main.tsx`: wire `createBrowserRouter`/`RouterProvider`, remove `App.tsx`.
- [ ] `tests/setup.ts`: add MSW server lifecycle (`beforeAll`/`afterEach`/`afterAll`) alongside existing RTL/jest-dom setup.
- [ ] Confirm backend is running locally (`dotnet run --project backend/src/TodoApi.Gateway/`) with an up-to-date `openapi.json` before running `generate:api` for the first time.
- [ ] Update `README.md` per [`CLAUDE.md`'s sync rule](../../../CLAUDE.md#keep-readmemd-in-sync) once this is implemented — build/test commands for the frontend become real, and "React Router and the generated client are not wired in" is no longer true.

## Open Questions

- ~~**`<input type="date">` value format vs. `dueDate`'s `DateTime?` wire type**~~ — Resolved: superseded by [Due Date: Date + Time](#due-date-date--time) — the field is `<input type="datetime-local">`, not `type="date"`, and its value passes straight through to the backend with no conversion, confirmed working end-to-end.
- **Exact React Router package name/version** — the architecture doc says "React Router (data APIs)" without pinning a version; implementation should install the current stable release and note it in `README.md`'s stack summary if it differs from what's implied there.

## Related Docs

- [`../../architecture/frontend/overview.md`](../../architecture/frontend/overview.md) — overall frontend design this spec implements a slice of
- [`../../architecture/overview-architecture.md`](../../architecture/overview-architecture.md#api-contract--client-generation) — system-level client-generation pipeline
- [`../add-todo/spec.md`](../backend/add-todo/spec.md) — backend `POST /todos` contract this route's action calls
- [`../list-todos/spec.md`](../backend/list-todos/spec.md) — backend `GET /todos` contract this route's loader calls
- [`../../requirements/requirements.md`](../../requirements/requirements.md) — source List/Add requirements
