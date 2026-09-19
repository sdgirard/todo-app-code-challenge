# Requirements Q&A — Internal Working Doc (Not for Foci)

**Internal/development use only.** This is the full brainstormed list of every ambiguity found in the requirements, used to think through the problem space. Most of these have reasonable, defensible defaults and are better handled as documented assumptions in the README than as questions sent to Foci.

The actual, trimmed list of questions to send is in [`requirements-qa.md`](./requirements-qa.md) — that is the public/shareable version. Keep this doc and that one in sync manually if new ambiguities come up: think through them here first, then decide whether they're worth promoting to the public list.

Grouped by topic, referencing the relevant section of `requirements.md`.

## Interface & Scope

1. Is there a preferred interface among CLI, Basic Web Interface, SPA, and RESTful API, or is the choice entirely at my discretion with no weighting in evaluation?
2. If I build a RESTful API, is an accompanying minimal UI/CLI expected as a client, or is the API itself (exercised via tests/curl/Postman) a complete submission on its own?

## Data Model

3. For `dueDate`, is a date-only value (`YYYY-MM-DD`, no time component) acceptable, or should it support a timestamp?
4. Should `createdAt` (and any `updatedAt`, if added) be stored in UTC, local time, or is the format left to my discretion as long as it's a timestamp?
5. Is an `id` field expected to be a specific format (e.g., auto-incrementing integer, UUID), or is that an implementation detail left to me?
6. Should an `updatedAt` field be added, or is tracking only `createdAt` sufficient per the stated data model?
7. Is there a maximum length for `title` and `description`, or is any reasonable length acceptable?

## Functionality

8. For "Update," should partial updates be supported (e.g., PATCH-style, only sending changed fields), or is a full replacement of title/description/dueDate acceptable?
9. Are "Complete" and "Incomplete" expected to be two distinct actions/endpoints, or is a single toggle/status-update action acceptable as long as both states are reachable?
10. Is there a maximum expected list size to design around (e.g., should List support pagination), or can I assume a small dataset with no need for pagination?

## Error Handling & API Contract

The requirements specify "standard HTTP methods and status codes" but don't define the error response shape or edge-case behavior.

11. Is there a preferred error response body shape (e.g., `{ "error": "message" }`, RFC 7807 Problem Details, or something else), or is any consistent, documented shape acceptable?
12. For invalid input (e.g., missing required `title`, malformed `dueDate`), is `400 Bad Request` with a descriptive message sufficient, or is there an expectation of field-level validation errors?
13. For operations on a non-existent ID (View/Update/Complete/Incomplete/Delete), is `404 Not Found` the expected response, or is there a different convention preferred?
14. Should validation errors distinguish between client mistakes (400-level) and unexpected server failures (500-level), or is a simpler error-handling approach acceptable given the time-box?

## Users & Authentication

The requirements don't mention users, accounts, or authentication anywhere — worth confirming explicitly since it materially affects the data model and architecture.

15. Is this a single-user application (one implicit "owner" of all to-do items, no login), or should it support multiple distinct users, each with their own list of to-do items?
16. If multi-user support is expected, is authentication (login/session/tokens) in scope, or can users be identified some simpler way (e.g., a `userId` passed in requests, no real auth)?
17. If auth is in scope, is there a preferred approach/library, or is a minimal scheme (e.g., basic auth, a simple token) acceptable given the time-box?
18. Should to-do items be scoped/isolated per user (i.e., user A can never see or modify user B's items), or is a shared list across all users acceptable?

## Persistence

19. Is there a preference between file-based storage (JSON/CSV) vs. an in-memory store with a swappable persistence interface, or are both considered equally acceptable given the time-box?
20. If file-based storage is used, is concurrent access (e.g., multiple simultaneous requests in a web/API interface) something I need to handle, or can I assume single-user, sequential access?

## Testing & Quality

21. Is there a minimum expected test coverage (unit vs. integration), or is "testable, well-structured code" left to my judgment on depth and breadth?
22. Should tests cover the persistence layer directly, or is mocking/stubbing storage in unit tests acceptable with persistence only verified via a smaller set of integration tests?

## Submission Logistics

23. Should the Git repository be public, or would Foci prefer a private repo with specific collaborators/reviewers added? If private, who should be granted access?

## Optional Enhancements

24. Are any of the optional enhancements (filtering, sorting, validation, Docker) more highly weighted than others in evaluation, or are they all equally "nice to have" with no bearing on the core assessment?

---

**Status:** Internal reference only — never sent to Foci as-is. See `requirements-qa.md` for the trimmed public list.
