namespace HeroWorkhorse.SqlTools;

public sealed class SqlToolsOptions
{
    /// <summary>
    /// Connection string for a dedicated read-only login. Even though queries are validated to be
    /// SELECT-only, defense in depth says the login should not be able to write anyway.
    /// </summary>
    public required string ConnectionString { get; set; }

    /// <summary>Max rows returned per query; results beyond this are cut off and flagged.</summary>
    public int MaxRows { get; set; } = 100;

    /// <summary>Per-query command timeout in seconds.</summary>
    public int CommandTimeoutSeconds { get; set; } = 15;
}
