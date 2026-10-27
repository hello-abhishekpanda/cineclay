# Deployment preparation

The checked-in deployment files are deliberately non-publishing. CI builds API and web OCI archives and stores them as short-lived workflow artifacts. The manual CD preparation workflow only renders and validates these manifests.

Before enabling Azure delivery:

1. Provision Azure Container Registry, AKS and Azure Database for PostgreSQL.
2. Replace the image registry and repository placeholders in `k8s/overlays/azure/kustomization.yaml` and `argocd/application.yaml`.
3. Create the `cineclay-runtime` secret through Azure Key Vault/Secrets Store CSI or another secret manager. Never commit it.
4. Add Azure workload identity/OIDC and a reviewed ACR push job to CI.
5. Configure Argo CD to watch this repository. Delivery should update the image tags in Git; Argo CD should pull and reconcile them into AKS.
6. Protect the production GitHub environment and require approval before any tag update.

The application expects these secret keys: `postgres-connection-string`, `admin-api-key`, `admin-username`, and `admin-password`.
