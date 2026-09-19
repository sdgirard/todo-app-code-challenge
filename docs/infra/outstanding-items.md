# Infra Outstanding Items

Running backlog of infra-adjacent decisions that came up during design but were deliberately deferred — not forgotten, not urgent enough to block the current doc/spec, but worth tracking so they don't silently fall off. Distinct from the "Open Questions / TODO" sections in individual docs, which track questions specific to that doc's scope; this doc is for items that don't have an obvious home yet or span multiple docs.

Each item should say where it came from and why it was deferred, so revisiting it later doesn't require reconstructing the context.

## Container resource limits (CPU/memory)

**Came up in:** [`container-image.md`](./container-image.md) design review.

Nothing yet specifies CPU/memory `requests`/`limits` for the K8s pod, or whether .NET's GC needs tuning for a small home-lab node (e.g. `DOTNET_gcServer=0` for workstation GC on a memory-constrained node, or `DOTNET_GCHeapHardLimit` to cap heap growth). This isn't a Dockerfile concern — it belongs in the pod spec — but the pod spec itself is still TODO in [`deployment.md`](./deployment.md)'s Cluster/Ingress section.

**Deferred because:** the home-lab node's actual available resources aren't known yet, and setting limits before knowing the target hardware would be guessing. Revisit once `deployment.md`'s Cluster/Ingress section is worked out.

## ~~Image vulnerability scanning~~ — Resolved

**Resolved by:** [`harbor-registry-setup.md#5-vulnerability-scanning`](./harbor-registry-setup.md#5-vulnerability-scanning). Decision: Harbor's built-in (Trivy) scanner, enabled on the `todo-app` project, scan-on-push, results reviewed via the Harbor UI/API. Not currently gating the CI pipeline (informational only) — see that section's Policy note for why.

## K8s: cert password → Kestrel config wiring

**Came up in:** [`container-image.md`](./container-image.md#cert-format-pfxpkcs12) design review.

The `.pfx` cert password is decided to be a mounted-file secret, not a literal env var value (per [`../standards/aspnet-web-api-guidelines.md#secrets-management`](../standards/aspnet-web-api-guidelines.md#secrets-management)), but the exact mechanism for getting that mounted file's contents into Kestrel's `Certificates:Default:Password` config value isn't wired up yet. ASP.NET Core's `AddKeyPerFile` configuration provider (pointed at the same mounted `Secret` volume as the `.pfx`) is the likely choice, but this needs to actually land in `ConfigureServices`/`Program.cs`.

**Deferred because:** it's application config wiring, not image design — natural to build once there's a real Dockerfile/pod to test the mount against, rather than speculatively coding it now.

## K8s: Let's Encrypt cert → mounted `.pfx` conversion and rotation

**Came up in:** [`container-image.md`](./container-image.md#cert-mounting) and [`deployment.md`](./deployment.md#tls) design review.

The cert format decision (PFX, mounted from a K8s `Secret`) is settled, but *how* the Let's Encrypt cert for `foci-todo.thecameraeye.ca` actually gets issued, renewed, and converted into `.pfx` form inside a K8s `Secret` is not — depends on the still-undecided ACME mechanism (cert-manager on the cluster vs. the ingress/reverse proxy handling ACME natively vs. certbot). Rotation mechanics once mounted (does the pod need a restart to pick up a renewed cert, or can Kestrel hot-reload it) are also undecided.

**Deferred because:** this is squarely a cluster/ingress-layout decision, which `deployment.md` already flags as a separate, later conversation. Revisit alongside the rest of the Cluster/Ingress section.

## K8s: PVC / StorageClass for the SQLite `/data` mount

**Came up in:** [`container-image.md`](./container-image.md#persistence-sqlite-path-via-data) and [`deployment.md`](./deployment.md#cluster--ingress) design review.

The container-side convention (`/data` mount point, `ConnectionStrings__TodoDb=Data Source=/data/todo.db`) is decided, but the actual PVC/StorageClass definition that backs `/data` in the home-lab cluster — which StorageClass, provisioner, access mode, size — doesn't exist yet.

**Deferred because:** depends on the home-lab cluster's actual storage setup, which is part of the still-TODO Cluster/Ingress section in `deployment.md`. Without it, the to-do list resets on every pod restart/redeploy — a real gap, but not one that blocks writing the Dockerfile itself.

## Related Docs

- [`dockerfile-organization.md`](./dockerfile-organization.md) — multi-image Dockerfile layout and naming
- [`container-image.md`](./container-image.md) — backend container image design
- [`deployment.md`](./deployment.md) — deployment target, cluster/ingress (still TODO)
- [`harbor-registry-setup.md`](./harbor-registry-setup.md) — registry setup, including vulnerability scanning (resolves the item above)
