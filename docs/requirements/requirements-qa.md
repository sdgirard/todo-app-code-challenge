# Requirements Q&A — Questions for Foci Solutions

Open questions on the take-home coding challenge, to send to Christine Pothier / Foci Technical Team before or during implementation. Grouped by topic, referencing the relevant section of `requirements.md`.

## Interface & Scope

1. Is there a preferred interface among CLI, Basic Web Interface, SPA, and RESTful API, or is the choice entirely at my discretion with no weighting in evaluation?
2. If I build a RESTful API, is an accompanying minimal UI/CLI expected as a client, or is the API itself (exercised via tests/curl/Postman) a complete submission on its own?

## Data Model

3. For `dueDate`, is a date-only value (`YYYY-MM-DD`, no time component) acceptable, or should it support a timestamp?
4. Should `createdAt` (and any `updatedAt`, if added) be stored in UTC, local time, or is the format left to my discretion as long as it's a timestamp?
5. Is an `id` field expected to be a specific format (e.g., auto-incrementing integer, UUID), or is that an implementation detail left to me?
6. Should an `updatedAt` field be added, or is tracking only `createdAt` sufficient per the stated data model?

## Functionality

7. For "Update," should partial updates be supported (e.g., PATCH-style, only sending changed fields), or is a full replacement of title/description/dueDate acceptable?
8. Are "Complete" and "Incomplete" expected to be two distinct actions/endpoints, or is a single toggle/status-update action acceptable as long as both states are reachable?
9. Is there a maximum expected list size to design around (e.g., should List support pagination), or can I assume a small dataset with no need for pagination?

## Persistence

10. Is there a preference between file-based storage (JSON/CSV) vs. an in-memory store with a swappable persistence interface, or are both considered equally acceptable given the time-box?
11. If file-based storage is used, is concurrent access (e.g., multiple simultaneous requests in a web/API interface) something I need to handle, or can I assume single-user, sequential access?

## Testing & Quality

12. Is there a minimum expected test coverage (unit vs. integration), or is "testable, well-structured code" left to my judgment on depth and breadth?
13. Should tests cover the persistence layer directly, or is mocking/stubbing storage in unit tests acceptable with persistence only verified via a smaller set of integration tests?

## Submission Logistics

14. Should the Git repository be public, or would Foci prefer a private repo with specific collaborators/reviewers added? If private, who should be granted access?
15. Is there a specific deadline for submission, or is the "4-6 hours of focused effort" the only time guidance (i.e., no hard due date)?
16. Should the submission be a single commit/PR, or is an incremental commit history (as mentioned in the requirements) preferred over squashing before submission?

## Optional Enhancements

17. Are any of the optional enhancements (filtering, sorting, validation, Docker) more highly weighted than others in evaluation, or are they all equally "nice to have" with no bearing on the core assessment?

---

**Status:** Not yet sent. Review and prune before emailing — most of these have reasonable default answers in the original requirements and may not all be worth asking.
