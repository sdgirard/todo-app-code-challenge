# Authentication — Rough Notes

Status: not built yet. Phase 1 is single-user, no auth — matches the baseline in [`overview-architecture.md`](./overview-architecture.md) and [`backend/overview.md`](./backend/overview.md). This doc is for thinking through what multi-user/auth would look like *if* time allows (see [`../requirements/requirements-qa.md`](../requirements/requirements-qa.md) for the related question sent to Foci).

Two options under consideration: self-hosted Authentik, or Clerk (hosted).

## Option A: Self-hosted Authentik

Open-source identity provider, run it myself (e.g. on my own infra/cluster, could tie into the [Harbor](../infra/harbor-registry-setup.md) box or a separate container).

**Pros**
- Full control — no third-party dependency, no vendor lock-in.
- No per-user/per-MAU cost, no account limits.
- Already comfortable self-hosting infra (same pattern as Harbor).
- Good excuse to show off infra/devops skills as part of the submission, if that's valued.

**Cons**
- I have to stand it up, secure it, keep it running for however long Foci wants to evaluate — that's real infra ops for a take-home.
- Foci reviewers would need network access to my self-hosted instance to actually log in and test it — extra friction, possible firewall/DNS issues on their end.
- More time investment than the challenge probably calls for (challenge scope is 4-6 hrs baseline, this eats into that or the "extra time" buffer).
- If my server goes down after submission, the multi-user demo breaks for anyone reviewing later.

## Option B: Clerk (hosted)

Hosted auth-as-a-service, integrate via SDK.

**Pros**
- Nothing to host/maintain — Clerk's uptime, not mine.
- Fast to integrate (prebuilt sign-in/sign-up UI components, SDKs for React + ASP.NET/backend token verification).
- Foci reviewers can just sign up themselves with a normal email, no access-granting needed on my end.
- Reflects realistic real-world choice — most teams don't roll their own IdP for a CRUD app.

**Cons**
- Third-party dependency — if Clerk has an outage during review, demo breaks (lower risk than self-host, but not zero).
- Free tier limits (MAU cap) — shouldn't matter for a handful of reviewers, but worth checking current limits before committing.
- Less "look what I can run myself" impressiveness than self-hosting, if that's a thing Foci would care about (probably isn't, given the evaluation focus is architecture/testing/code quality, not infra ops).

## Access for Foci reviewers

Whichever option is chosen, reviewers need a way in:
- **Clerk:** they self-register with their own email — no action needed from me beyond having sign-up open.
- **Authentik:** I'd need to either pre-create accounts for them or open self-registration, plus give them the URL and make sure it's reachable.

## Fake / seed accounts

Would be good to have 1-2 pre-seeded demo accounts (fake users with sample to-do data) so reviewers can log in immediately without creating an account first, if they'd rather not. Applies to either option — need seed data + known test credentials documented in the README if this ships.

## Leaning

Clerk is the more sensible choice for a time-boxed take-home: less ops overhead, no availability risk from my own infra, and reviewers get in with zero friction. Self-hosting Authentik is more impressive as an infra flex but is probably the wrong trade-off here given the evaluation criteria and the time budget. Not a final decision — revisit if/when there's time to build multi-user at all.
