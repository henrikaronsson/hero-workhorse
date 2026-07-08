# prompts/

Reusable system prompts as plain markdown. Copy the text into your agent (or load the file at startup) and fill any `{{placeholders}}`.

- `sql-agent.md` — data analyst over a read-only SQL database, tuned for the `get_schema` / `run_query` tools from `dotnet/sql-tools`. Includes an optional schema-injection variant that saves one tool round-trip.

This folder grows organically — add prompts here once they have proven themselves in a real project.
