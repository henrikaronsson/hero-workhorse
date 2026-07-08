# infra/

Copy-paste infrastructure pieces for hosting the harness.

- `bicep/container-app.bicep` — Azure Container App + Key Vault + user-assigned Managed Identity for hosting the sample API. Secrets (e.g. an Azure OpenAI key) live in Key Vault; the app reads them via identity, never from appsettings.
- `docker-compose/` — composable service snippets for local dev:
  - `mssql.yml` — SQL Server 2022 with a healthcheck.
  - `ollama.yml` — Ollama in a container (alternative to a host install; note that host installs usually have better GPU access on Windows).

Use compose snippets by merging files:

```bash
docker compose -f mssql.yml -f ollama.yml up -d
```

The sample keeps its own self-contained `samples/sql-agent/docker-compose.yml` (SQL Server + seed); these pieces are for wiring your own projects.
