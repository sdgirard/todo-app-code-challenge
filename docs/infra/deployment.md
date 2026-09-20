# Deployment

Status: draft, TLS strategy only for now. Full deployment details (cluster layout, ingress, how the app actually gets deployed) weren't a core requirement and didn't fit in this project's time budget — sections below are TODO placeholders rather than a shipped deployment.

Demo deployment target: home lab Kubernetes cluster, exposed publicly at **`foci-todo.thecameraeye.ca`** through a public, secure entry point. Image comes from the Harbor registry — see [`harbor-registry-setup.md`](./harbor-registry-setup.md).

## TLS

TLS is built in from the start, not bolted on later — the app should never run over plain HTTP, in any environment, **including cluster-internal traffic between the ingress and the pod.**

- **Termination: Kestrel terminates TLS directly, not the ingress.** The ingress/reverse proxy does TLS passthrough — encrypted bytes go straight to Kestrel in the pod, which holds the certificate and does the handshake itself. Decided against ingress termination (where the ingress→pod hop would be plaintext HTTP) for two reasons: no cleartext on the wire anywhere, and gRPC-readiness — gRPC needs a real end-to-end TLS handshake (ALPN → HTTP/2) that ingress-terminated setups tend to handle poorly. Full rationale and the cert-mount mechanics (PFX format, `/certs` mount point, Kestrel env config) are in [`container-image.md`](./container-image.md#tls-kestrel-terminates-it-directly).
- **Local development:** ASP.NET Core's development HTTPS certificate (`dotnet dev-certs https`), exported to `.pfx` and mounted into the container the same way the production cert is — same mechanism in every environment, just a different cert source.
- **Demo deployment (home lab):** [Let's Encrypt](https://letsencrypt.org/) issues the certificate for `foci-todo.thecameraeye.ca`, the public hostname for the demo deployment. The cert is delivered to the pod as a K8s `Secret` mounted at `/certs`, per `container-image.md`.

Not yet decided (part of the later deployment conversation): how the Let's Encrypt cert actually gets issued, renewed, and converted to `.pfx` in the home lab (e.g. cert-manager on the cluster vs. the ingress/reverse proxy handling ACME natively vs. certbot), and the exact rotation mechanics once mounted — that's an ingress/cluster-layout detail, not a TLS-strategy detail. The strategy itself (Kestrel-terminated TLS everywhere, PFX cert mounted into the pod) is decided regardless of that mechanism.

## Cluster / Ingress

TODO — home lab Kubernetes cluster details, ingress controller choice (must support TLS passthrough, per the TLS section above), how the Harbor image gets deployed (manifests vs. Helm chart, etc.).

Also needs: a PersistentVolume (PVC) mounted into the pod at `/data` for the SQLite database file — see [`../architecture/backend/overview.md#where-the-sqlite-file-lives`](../architecture/backend/overview.md#where-the-sqlite-file-lives) and [`container-image.md`](./container-image.md#persistence-sqlite-path-via-data). Without it, the to-do list resets on every pod restart/redeploy.

## Environments

TODO — what environments exist (local, demo?), how config/secrets differ between them.

## Related Docs

- [`container-image.md`](./container-image.md) — container image design: TLS/cert mounting, SQLite volume, build stages
- [`harbor-registry-setup.md`](./harbor-registry-setup.md) — where the Docker image comes from
- [`versioning.md`](./versioning.md) — how deployed images are versioned
- [`outstanding-items.md`](./outstanding-items.md) — deferred infra decisions (resource limits, image scanning)
- [`../architecture/overview-architecture.md`](../architecture/overview-architecture.md) — system-level architecture
- [`../architecture/backend/overview.md`](../architecture/backend/overview.md) — backend design
