# Requirements Q&A — Questions for Foci Solutions

Questions to send to Christine Pothier / Foci Technical Team before or during implementation. This is the trimmed, public version — only questions where a wrong guess would mean meaningful rework, not just a judgment call. Everything else is treated as a documented assumption in the README instead.

The full brainstormed list (including everything cut from here) lives in [`requirements-qa-internal.md`](./requirements-qa-internal.md) for reference — that doc is internal only and not meant to be shared.

1. Is there a preferred interface among CLI, Basic Web Interface, SPA, and RESTful API, or is the choice entirely at my discretion with no weighting in evaluation?
2. The requirements don't mention users, accounts, or authentication. Is this meant to be a single-user application (no login), or should it support multiple distinct users with their own isolated lists? This affects the data model and architecture significantly, so I want to confirm before building rather than assume.
3. Is there a preferred error response shape for the API (e.g., a consistent JSON error body, standard HTTP status codes like 400/404), or is any consistent, documented convention acceptable?

---

**Status:** Ready to send.
