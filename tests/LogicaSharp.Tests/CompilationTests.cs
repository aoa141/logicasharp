using LogicaSharp.Compilation;
using LogicaSharp.Dialects;

namespace LogicaSharp.Tests;

public class CompilationTests
{
    [Fact]
    public void Compile_SimpleFacts_GeneratesUnionAll()
    {
        var source = @"
            Person(""Alice"", 30);
            Person(""Bob"", 25);
            Person(""Carol"", 35);
        ";

        var sql = Logica.Compile(source, "Person", "mssql");

        // Should generate UNION ALL of all facts
        Assert.Contains("SELECT", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("'Alice'", sql);
        Assert.Contains("'Bob'", sql);
        Assert.Contains("'Carol'", sql);
        Assert.Contains("UNION ALL", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Compile_FactsWithNamedFields_GeneratesCorrectColumnNames()
    {
        var source = @"
            Person(name: ""Alice"", age: 30);
            Person(name: ""Bob"", age: 25);
        ";

        var sql = Logica.Compile(source, "Person", "mssql");

        Assert.Contains("[name]", sql);
        Assert.Contains("[age]", sql);
    }

    [Fact]
    public void Compile_RuleWithCondition_GeneratesWhereClause()
    {
        var source = @"
            Person(name: ""Alice"", age: 30);
            Person(name: ""Bob"", age: 15);
            Adult(name:) :- Person(name:, age:), age >= 18;
        ";

        var sql = Logica.Compile(source, "Adult", "mssql");

        Assert.Contains("WHERE", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(">=", sql);
        Assert.Contains("18", sql);
    }

    [Fact]
    public void Compile_MSSQL_UsesSquareBrackets()
    {
        var source = @"Person(name: ""Test"");";

        var sql = Logica.Compile(source, "Person", "mssql");

        Assert.Contains("[name]", sql);
    }

    [Fact]
    public void Compile_ClickHouse_UsesBackticks()
    {
        var source = @"Person(name: ""Test"");";

        var sql = Logica.Compile(source, "Person", "clickhouse");

        Assert.Contains("`name`", sql);
    }

    [Fact]
    public void Compile_DetectsEngine_FromAnnotation()
    {
        var source = @"
            @Engine(""mssql"");
            Person(""Alice"");
        ";

        var engine = Logica.DetectEngine(source);

        Assert.Equal("mssql", engine);
    }

    [Fact]
    public void Compile_AutoDialect_UsesAnnotation()
    {
        var source = @"
            @Engine(""clickhouse"");
            Person(name: ""Alice"");
        ";

        var sql = Logica.CompileWithAutoDialect(source, "Person");

        // ClickHouse uses backticks
        Assert.Contains("`name`", sql);
    }

    [Fact]
    public void Compile_MultiplePredicates_CompilesSeparately()
    {
        var source = @"
            A(1);
            A(2);
            B(3);
            B(4);
        ";

        var all = Logica.CompileAll(source, "mssql");

        Assert.True(all.ContainsKey("A"));
        Assert.True(all.ContainsKey("B"));
        Assert.Contains("1", all["A"]);
        Assert.Contains("3", all["B"]);
    }

    [Fact]
    public void Compile_Join_GeneratesFromClause()
    {
        var source = @"
            Parent(parent: ""Alice"", child: ""Bob"");
            Parent(parent: ""Bob"", child: ""Carol"");
            Grandparent(gp:, gc:) :- Parent(parent: gp, child: p), Parent(parent: p, child: gc);
        ";

        var sql = Logica.Compile(source, "Grandparent", "mssql");

        // Should have FROM clause with join condition
        Assert.Contains("FROM", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Compile_Expression_TranslatesOperators()
    {
        var source = @"
            N(x) :- x == 1 + 2;
        ";

        var sql = Logica.Compile(source, "N", "mssql");

        // == should become = and arithmetic should be translated
        Assert.Contains("=", sql);
        Assert.Contains("1", sql);
        Assert.Contains("2", sql);
        Assert.Contains("+", sql);
    }
}
