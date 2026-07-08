using Microsoft.SqlServer.TransactSql.ScriptDom;

namespace HeroWorkhorse.SqlTools;

/// <summary>
/// Validates that a T-SQL string is exactly one SELECT statement, using a real parser
/// (ScriptDom) rather than string matching. Rejects SELECT ... INTO since it writes.
/// </summary>
public static class SelectOnlyValidator
{
    /// <summary>Returns null if valid, otherwise a human-readable rejection reason.</summary>
    public static string? Validate(string sql)
    {
        if (string.IsNullOrWhiteSpace(sql))
        {
            return "Query is empty.";
        }

        var parser = new TSql160Parser(initialQuotedIdentifiers: true);
        var fragment = parser.Parse(new StringReader(sql), out var errors);
        if (errors.Count > 0)
        {
            var first = errors[0];
            return $"SQL parse error at line {first.Line}: {first.Message}";
        }

        if (fragment is not TSqlScript script)
        {
            return "Could not parse query.";
        }

        var statements = script.Batches.SelectMany(b => b.Statements).ToList();
        if (statements.Count != 1)
        {
            return $"Exactly one statement is allowed, got {statements.Count}.";
        }

        if (statements[0] is not SelectStatement select)
        {
            return $"Only SELECT statements are allowed, got {statements[0].GetType().Name.Replace("Statement", "").ToUpperInvariant()}.";
        }

        if (select.Into is not null)
        {
            return "SELECT ... INTO is not allowed (it writes to a table).";
        }

        return null;
    }
}
