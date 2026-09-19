# To-Do List Application — Coding Challenge Requirements

Source: "Foci Solutions: Invitation to Take Home Coding Challenge" (email thread, sent by Christine Pothier / Foci Technical Team, 2026-09-15).

## Overview

- **Scenario:** Build a to-do list application that allows users to manage their tasks.
- **Time expectation:** ~4-6 hours of focused effort. Christine verbally noted during the interview that candidates have up to a month to complete the challenge given other commitments — no hard deadline.
- **Evaluation focus:** Architecture, testing, and code quality — not interface polish or sophistication. If time runs short, reduce feature scope rather than quality.
- **AI tools:** Use of generative AI tools (e.g., GitHub Copilot, ChatGPT) is explicitly permitted and encouraged. The final submission must still be consistent, coherent, and reflect the candidate's own understanding and ownership of the design and implementation.

## Core Requirements

### 1. Programming Language

Implement the solution in one of:
- JavaScript/TypeScript (Node.js)
- Python
- C#
- Java

### 2. Interface (choose one)

- **Command-Line Interface (CLI):** Accept commands and arguments to perform actions.
- **Basic Web Interface:** Simple web pages/forms (minimal frontend JS/styling is fine) interacting with backend logic. Functionality over visual polish. Frameworks like Next.js, Flask/Django, ASP.NET Core MVC/Razor Pages, or others are acceptable.
- **SPA:** React, Vue, Angular, or another modern SPA framework.
- **RESTful API:** API endpoints (preferably JSON) allowing clients to manage to-do items.

> Regardless of interface choice, evaluation centers on architecture, testing, and code quality — not interface sophistication.

### 3. Data Model

Each to-do item must include:

| Field | Required | Type | Notes |
|---|---|---|---|
| `title` | Required | string | Short description of the task |
| `description` | Optional | string | Longer explanation |
| `dueDate` | Optional | date | `YYYY-MM-DD` format is fine |
| `isCompleted` | — | boolean | Defaults to `false` |
| `createdAt` | — | timestamp | Set when the item is created |

### 4. Functionality (CRUD + Status)

Provide the following capabilities through the chosen interface:

- **Add** — Create a new to-do item.
- **List** — Display all to-do items (include Title, Due Date, Completion Status at minimum).
- **View** — Show details of a specific to-do item by its ID.
- **Update** — Modify the title, description, or due date of an existing item by its ID.
- **Complete** — Mark a specific to-do item as completed by its ID.
- **Incomplete** — Mark a specific to-do item as not completed by its ID.
- **Delete** — Remove a to-do item by its ID.

#### API specifics (if REST API is chosen)

- Define clear endpoints using standard REST conventions, e.g.:
  - `POST /todos`
  - `GET /todos`
  - `GET /todos/{id}`
  - `PUT /todos/{id}`
  - `DELETE /todos/{id}`
  - Potentially `PATCH` or dedicated actions for complete/incomplete.
- Use standard HTTP methods and status codes.
- JSON request/response bodies are preferred.

### 5. Persistence

- Data must persist between application runs.
- Simple file-based storage (e.g., JSON, CSV) is sufficient — a full database is not required (but is acceptable if preferred).
- An in-memory store with clear separation that would allow swapping in persistent storage later is also acceptable, especially if file I/O would push significantly over the time estimate.

## Optional Enhancements (not required)

- Filtering the list (e.g., completed, incomplete, overdue items).
- Sorting the list (e.g., by due date, creation date, title).
- Input validation (especially important for APIs/Web UIs).
- Containerization (e.g., Docker).

## Submission Requirements

- Provide a link to a Git repository (e.g., GitHub, GitLab).
- Include a `README.md` in the repository root with:
  - Clear instructions on how to build/run the application.
  - Instructions on how to run the tests.
  - A brief explanation of design choices, particularly backend architecture and testing strategy.
  - Any assumptions made.
  - (Optional) Notes on trade-offs made due to time constraints.
- Ensure commit history is reasonably clean, reflecting the development process.

## Decisions Made

- **Commit history:** Will follow personal practice of squashing before submission rather than keeping partial/WIP check-ins — no intermediate commits expected.
- **Persistence:** EF Core + SQLite, going beyond the "file-based or in-memory is sufficient" minimum to demonstrate real ORM usage (migrations, change tracking, LINQ). SQLite keeps it compatible with the spirit of file-based storage — one file, no separate DB server. See [`../architecture/backend/overview.md#persistence`](../architecture/backend/overview.md#persistence).

## Open Questions / Follow-ups

None raised yet. The invitation email states: "If anything is unclear or you have questions, please don't hesitate to reach out."
