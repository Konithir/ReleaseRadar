# Integration configuration

## Argo CD

Create an Argo CD token with permission to read applications and invoke sync. Set `Integrations:ArgoCd:Enabled` to `true`, then supply the server URL and token. A rollback is implemented as a sync to the recorded previous revision, so no numeric Argo CD deployment history ID is required.

## Kubernetes

When running in-cluster, ReleaseRadar reads `/var/run/secrets/kubernetes.io/serviceaccount/token`. The Helm chart binds its service account to a read-only ClusterRole covering pods, namespaces and common workload types. A token can also be supplied explicitly for a remote cluster.

## Prometheus

ReleaseRadar executes instant queries against `/api/v1/query`. Workloads are selected by namespace and pod-name prefix. CPU is calculated from `container_cpu_usage_seconds_total`; memory is calculated from working set bytes relative to declared limits.

## GitHub

Repositories use the `owner/repository` form. If a sync request does not contain an explicit revision, the GitHub adapter resolves the current repository head and passes its short SHA to Argo CD.

## Secrets

The chart expects these keys in `secrets.existingSecret`:

- `connection-string`
- `argocd-token`
- `github-token`

`secrets.create=true` is available for short-lived test namespaces. Production environments should create the Secret through External Secrets, the Azure Key Vault CSI driver or another secret-management controller.
