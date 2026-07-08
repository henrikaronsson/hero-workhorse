# dotnet/sql-tools

## What

Two agent tools for SQL Server:

- `get_schema` — lists all tables with columns, types and nullability so the model can orient itself.
- `run_query` — executes exactly one **SELECT** statement, with a row limit and a command timeout.

SELECT-only is enforced by actually parsing the SQL with `Microsoft.SqlServer.TransactSql.ScriptDom` (statement-type whitelist, `SELECT ... INTO` rejected) — not by string matching. On top of that, use a **read-only login** in the connection string as defense in depth; validation alone should never be the only barrier.

Decision on the spec's [OPEN] point: SQL Server only for now. Postgres could reuse `SqlToolsOptions`/the same shape with Npgsql plus a Postgres parser (or `EXPLAIN`-based validation); Cosmos would be a separate optional tool. Neither was cheap enough to justify in v1.

## Files

- `SqlTools.cs` — the two tools + `AsAIFunctions()` factory.
- `SelectOnlyValidator.cs` — ScriptDom-based SELECT-only validation.
- `SqlToolsOptions.cs` — connection string, max rows, timeout.

## Dependencies

- `Microsoft.Data.SqlClient`
- `Microsoft.SqlServer.TransactSql.ScriptDom`
- `Microsoft.Extensions.AI.Abstractions`

## Usage

```csharp
var sqlTools = new SqlTools(new SqlToolsOptions
{
    ConnectionString = configuration.GetConnectionString("ReadOnly")!, // dedicated read-only login!
    MaxRows = 100,
    CommandTimeoutSeconds = 15,
});

var chatOptions = new ChatOptions { Tools = [.. sqlTools.AsAIFunctions()] };
```

Errors (validation rejections, SQL errors) are returned as `{ "error": "..." }` tool results instead of thrown, so the model can read the message and correct its query.

Creating a read-only login (run once against your DB):

```sql
CREATE LOGIN agent_reader WITH PASSWORD = '...';
CREATE USER agent_reader FOR LOGIN agent_reader;
ALTER ROLE db_datareader ADD MEMBER agent_reader;
```

## Pairs with

- `dotnet/agent-loop` — drop `AsAIFunctions()` into the loop's `ChatOptions.Tools`.
- `prompts/sql-agent.md` — a system prompt tuned for these two tools.
- `samples/sql-agent` — end-to-end usage against a seeded demo database.
