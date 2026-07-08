using HeroWorkhorse.SqlTools;

namespace HeroWorkhorse.Tests;

public class SelectOnlyValidatorTests
{
    [Theory]
    [InlineData("SELECT 1")]
    [InlineData("SELECT * FROM Customers")]
    [InlineData("SELECT TOP 10 Name, Price FROM Products ORDER BY Price DESC")]
    [InlineData("SELECT c.Name, COUNT(*) FROM Customers c JOIN Orders o ON o.CustomerId = c.Id GROUP BY c.Name")]
    [InlineData("WITH Recent AS (SELECT * FROM Orders WHERE CreatedAt > '2026-01-01') SELECT COUNT(*) FROM Recent")]
    [InlineData("SELECT (SELECT MAX(Price) FROM Products) AS MaxPrice")]
    [InlineData("SELECT 1 UNION ALL SELECT 2")]
    [InlineData("-- a comment\nSELECT 1")]
    [InlineData("  \n\t SELECT 1 ; ")]
    public void Accepts_single_select(string sql)
    {
        Assert.Null(SelectOnlyValidator.Validate(sql));
    }

    [Theory]
    [InlineData("INSERT INTO Customers (Name) VALUES ('x')")]
    [InlineData("UPDATE Customers SET Name = 'x'")]
    [InlineData("DELETE FROM Customers")]
    [InlineData("DROP TABLE Customers")]
    [InlineData("TRUNCATE TABLE Customers")]
    [InlineData("CREATE TABLE T (Id INT)")]
    [InlineData("ALTER TABLE Customers ADD Age INT")]
    [InlineData("EXEC sp_who")]
    [InlineData("GRANT SELECT ON Customers TO someone")]
    [InlineData("MERGE Customers AS t USING Customers AS s ON t.Id = s.Id WHEN MATCHED THEN DELETE;")]
    public void Rejects_non_select_statements(string sql)
    {
        Assert.NotNull(SelectOnlyValidator.Validate(sql));
    }

    [Theory]
    [InlineData("SELECT 1; DELETE FROM Customers")]
    [InlineData("SELECT 1; SELECT 2")]
    [InlineData("SELECT 1\nGO\nDELETE FROM Customers")]
    [InlineData("/* hidden */ SELECT 1; DROP TABLE Customers; --")]
    public void Rejects_multiple_statements(string sql)
    {
        Assert.NotNull(SelectOnlyValidator.Validate(sql));
    }

    [Fact]
    public void Rejects_select_into()
    {
        var reason = SelectOnlyValidator.Validate("SELECT * INTO Archive FROM Customers");

        Assert.NotNull(reason);
        Assert.Contains("INTO", reason);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Rejects_empty_input(string? sql)
    {
        Assert.NotNull(SelectOnlyValidator.Validate(sql!));
    }

    [Theory]
    [InlineData("SELECT FROM")]
    public void Rejects_unparsable_sql(string sql)
    {
        var reason = SelectOnlyValidator.Validate(sql);

        Assert.NotNull(reason);
        Assert.Contains("parse", reason, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("SELEC 1")]     // ScriptDom parses a bare identifier as an implicit EXEC
    [InlineData("hello world")]
    public void Rejects_garbage_input(string sql)
    {
        Assert.NotNull(SelectOnlyValidator.Validate(sql));
    }
}
