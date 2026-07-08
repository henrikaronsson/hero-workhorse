using System.ComponentModel;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.AI;

namespace HeroWorkhorse.SqlTools;

/// <summary>
/// Two agent tools for SQL Server: a schema explorer and a guarded SELECT-only query runner.
/// Call <see cref="AsAIFunctions"/> to get them as <see cref="AIFunction"/>s for a ChatOptions.Tools list.
/// </summary>
public sealed class SqlTools(SqlToolsOptions options)
{
    /// <summary>Both tools as AIFunctions, ready to drop into ChatOptions.Tools.</summary>
    public IList<AIFunction> AsAIFunctions() =>
    [
        AIFunctionFactory.Create(GetSchemaAsync, "get_schema",
            "Lists all tables in the database with their columns, data types and nullability. Call this before writing queries."),
        AIFunctionFactory.Create(QueryAsync, "run_query",
            "Runs a single read-only SELECT query against the database and returns the rows. Only SELECT is allowed."),
    ];

    [Description("Lists all tables with their columns, data types and nullability.")]
    public async Task<object> GetSchemaAsync(CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT c.TABLE_SCHEMA, c.TABLE_NAME, c.COLUMN_NAME, c.DATA_TYPE,
                   c.CHARACTER_MAXIMUM_LENGTH, c.IS_NULLABLE
            FROM INFORMATION_SCHEMA.COLUMNS c
            INNER JOIN INFORMATION_SCHEMA.TABLES t
                ON t.TABLE_SCHEMA = c.TABLE_SCHEMA AND t.TABLE_NAME = c.TABLE_NAME
            WHERE t.TABLE_TYPE = 'BASE TABLE'
            ORDER BY c.TABLE_SCHEMA, c.TABLE_NAME, c.ORDINAL_POSITION
            """;

        await using var connection = new SqlConnection(options.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection) { CommandTimeout = options.CommandTimeoutSeconds };
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var tables = new Dictionary<string, List<object>>();
        while (await reader.ReadAsync(cancellationToken))
        {
            var table = $"{reader.GetString(0)}.{reader.GetString(1)}";
            var maxLength = reader.IsDBNull(4) ? (int?)null : reader.GetInt32(4);
            var type = reader.GetString(3) + (maxLength is > 0 ? $"({maxLength})" : maxLength == -1 ? "(max)" : "");

            if (!tables.TryGetValue(table, out var columns))
            {
                tables[table] = columns = [];
            }
            columns.Add(new
            {
                name = reader.GetString(2),
                type,
                nullable = reader.GetString(5) == "YES",
            });
        }

        return tables.Select(t => new { table = t.Key, columns = t.Value });
    }

    [Description("Runs a single read-only SELECT query and returns the rows.")]
    public async Task<object> QueryAsync(
        [Description("A single T-SQL SELECT statement.")] string sql,
        CancellationToken cancellationToken = default)
    {
        if (SelectOnlyValidator.Validate(sql) is { } rejection)
        {
            return new { error = rejection };
        }

        await using var connection = new SqlConnection(options.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection) { CommandTimeout = options.CommandTimeoutSeconds };

        try
        {
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);

            var columns = Enumerable.Range(0, reader.FieldCount).Select(reader.GetName).ToList();
            var rows = new List<List<object?>>();
            var truncated = false;

            while (await reader.ReadAsync(cancellationToken))
            {
                if (rows.Count >= options.MaxRows)
                {
                    truncated = true;
                    break;
                }
                var row = new List<object?>(reader.FieldCount);
                for (var i = 0; i < reader.FieldCount; i++)
                {
                    row.Add(reader.IsDBNull(i) ? null : reader.GetValue(i));
                }
                rows.Add(row);
            }

            return new { columns, rows, rowCount = rows.Count, truncated };
        }
        catch (SqlException ex)
        {
            return new { error = $"Query failed: {ex.Message}" };
        }
    }
}
