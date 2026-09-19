# Deployment

Status: draft, TLS strategy only for now. Full deployment details (cluster layout, ingress, how the app actually gets deployed) are a separate, later conversation — sections below are TODO placeholders until then.

Demo deployment target: home lab Kubernetes cluster, exposed publicly at **`foci-todo.thecameraeye.ca`** through a public, secure entry point. Image comes from the Harbor registry — see [`harbor-registry-setup.md`](./harbor-registry-setup.md).

## TLS

TLS is built in from the start, not bolted on later — the app should never run over plain HTTP, in any environment.

- **Local development:** ASP.NET Core's development HTTPS certificate (`dotnet dev-certs https --trust`), same as the framework default. No real cert authority involved; this is purely so local dev runs over HTTPS and matches how the app behaves in every other environment.
- **Demo deployment (home lab):** [Let's Encrypt](https://letsencrypt.org/) issues the certificate for `foci-todo.thecameraeye.ca`, the public hostname for the demo deployment, since the home lab needs a publicly-trusted cert for Foci reviewers to hit the demo URL without browser warnings.

Not yet decided (part of the later deployment conversation): how the Let's Encrypt cert actually gets issued and renewed in the home lab (e.g. cert-manager on the cluster vs. the ingress/reverse proxy handling ACME natively vs. certbot) — that's an ingress/cluster-layout detail, not a TLS-strategy detail. The strategy itself (dev cert locally, Let's Encrypt for the demo) is decided regardless of that mechanism.

## Cluster / Ingress

TODO — home lab Kubernetes cluster details, ingress controller choice, how the Harbor image gets deployed (manifests vs. Helm chart, etc.).

Also needs: a PersistentVolume (PVC) mounted into the pod for the SQLite database file — see [`../architecture/backend/overview.md#where-the-sqlite-file-lives`](../architecture/backend/overview.md#where-the-sqlite-file-lives). Without it, the to-do list resets on every pod restart/redeploy.

## Environments

TODO — what environments exist (local, demo?), how config/secrets differ between them.

## Related Docs

- [`harbor-registry-setup.md`](./harbor-registry-setup.md) — where the Docker image comes from
- [`versioning.md`](./versioning.md) — how deployed images are versioned
- [`../architecture/overview-architecture.md`](../architecture/overview-architecture.md) — system-level architecture
- [`../architecture/backend/overview.md`](../architecture/backend/overview.md) — backend design
