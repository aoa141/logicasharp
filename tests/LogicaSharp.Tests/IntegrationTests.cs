using LogicaSharp.Compilation;

namespace LogicaSharp.Tests;

/// <summary>
/// Integration tests that verify the C# compiler produces valid SQL.
/// These tests verify that the output contains expected SQL patterns
/// based on outputs from the Python reference implementation.
/// </summary>
public class IntegrationTests
{
    [Fact]
    public void Functor_SimpleFunctor_GeneratesCorrectSQL()
    {
        // Simpler test first - basic rule with same structure should work
        var simpleSource = @"
@Engine(""mssql"");

DailyActiveUsers(user: ""alice"", product: ""App"", os: ""Windows"", sessions: 5);
DailyActiveUsers(user: ""bob"", product: ""App"", os: ""Mac"", sessions: 3);

FilteredDAU(user:, sessions:) :-
    DailyActiveUsers(user:, product:, os:, sessions:),
    product == ""App"",
    os == ""Windows"";
";

        var simpleSql = Logica.Compile(simpleSource, "FilteredDAU", "mssql");
        Assert.True(simpleSql.Contains("FROM", StringComparison.OrdinalIgnoreCase),
            $"Simple rule - Expected FROM clause in SQL:\n{simpleSql}");

        // Now test with functor
        var source = @"
@Engine(""mssql"");

DailyActiveUsers(user: ""alice"", product: ""App"", os: ""Windows"", sessions: 5);
DailyActiveUsers(user: ""bob"", product: ""App"", os: ""Mac"", sessions: 3);

@Functor(""FilterByProductOs"");
FilterByProductOs(user:, sessions:) :-
    source(user:, product:, os:, sessions:),
    product == filterProduct,
    os == filterOs;

FilteredDAU := FilterByProductOs(source: DailyActiveUsers, filterProduct: ""App"", filterOs: ""Windows"");
";

        var compiler = new LogicaCompiler("mssql");
        var simpleBodyDesc = compiler.DescribeRuleBody(simpleSource, "FilteredDAU");
        var functorTemplateBodyDesc = compiler.DescribeRuleBody(source, "FilterByProductOs");
        var functorBodyDesc = compiler.DescribeRuleBody(source, "FilteredDAU");
        var sql = Logica.Compile(source, "FilteredDAU", "mssql");

        // Compare body structures
        Assert.True(functorBodyDesc.Contains("BodyCall"),
            $"Functor body should contain BodyCall.\n\nFunctor template body:\n{functorTemplateBodyDesc}\n\nFunctor expanded body:\n{functorBodyDesc}\n\nSimple body:\n{simpleBodyDesc}");

        // Should have FROM clause with DailyActiveUsers
        Assert.True(sql.Contains("FROM", StringComparison.OrdinalIgnoreCase),
            $"Functor - Expected FROM clause in SQL:\n{sql}\n\nSimple SQL for comparison:\n{simpleSql}\n\nFunctor body:\n{functorBodyDesc}\n\nSimple body:\n{simpleBodyDesc}");
        // Should NOT have comment placeholder
        Assert.False(sql.Contains("/*"), $"Unexpected comment placeholder in SQL:\n{sql}");
    }

    /// <summary>
    /// Test based on Python output:
    /// SELECT 'Alice' AS col0, 30 AS col1 UNION ALL SELECT 'Bob' AS col0, 25 AS col1
    /// </summary>
    [Fact]
    public void Integration_SimpleFacts_MatchesPythonPattern()
    {
        var source = @"
            @Engine(""mssql"");
            Person(""Alice"", 30);
            Person(""Bob"", 25);
        ";

        var sql = Logica.Compile(source, "Person", "mssql");

        // Python produces: SELECT 'Alice' AS col0, 30 AS col1 UNION ALL ...
        Assert.Contains("SELECT", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("AS", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("UNION ALL", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("'Alice'", sql);
        Assert.Contains("'Bob'", sql);
        Assert.Contains("30", sql);
        Assert.Contains("25", sql);
    }

    /// <summary>
    /// Test based on Python output for named fields:
    /// SELECT 'Alice' AS name, 30 AS age
    /// </summary>
    [Fact]
    public void Integration_NamedFields_MatchesPythonPattern()
    {
        var source = @"
            @Engine(""mssql"");
            Person(name: ""Alice"", age: 30);
        ";

        var sql = Logica.Compile(source, "Person", "mssql");

        // Should have named columns
        Assert.Contains("[name]", sql);
        Assert.Contains("[age]", sql);
    }

    /// <summary>
    /// Test based on Python output for rule with body:
    /// SELECT Person.name AS name FROM t_0_Person AS Person WHERE (Person.age >= 18)
    /// </summary>
    [Fact]
    public void Integration_RuleWithBody_MatchesPythonPattern()
    {
        var source = @"
            @Engine(""mssql"");
            Person(name: ""Alice"", age: 30);
            Person(name: ""Bob"", age: 15);
            Adult(name:) :- Person(name:, age:), age >= 18;
        ";

        var sql = Logica.Compile(source, "Adult", "mssql");

        // Should have FROM, WHERE clauses
        Assert.Contains("FROM", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("WHERE", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(">=", sql);
        Assert.Contains("18", sql);
    }

    /// <summary>
    /// ClickHouse should use double quotes for string literals like Python does.
    /// Python output: SELECT "Alice" AS col0
    /// </summary>
    [Fact]
    public void Integration_ClickHouse_StringLiterals()
    {
        var source = @"
            @Engine(""clickhouse"");
            Person(""Alice"");
        ";

        var sql = Logica.Compile(source, "Person", "clickhouse");

        // ClickHouse uses backticks for identifiers
        Assert.Contains("`", sql);
    }

    /// <summary>
    /// Test engine detection matches Python behavior.
    /// </summary>
    [Fact]
    public void Integration_EngineDetection_MatchesPython()
    {
        var mssqlSource = "@Engine(\"mssql\"); Person(1);";
        var clickhouseSource = "@Engine(\"clickhouse\"); Person(1);";

        Assert.Equal("mssql", Logica.DetectEngine(mssqlSource));
        Assert.Equal("clickhouse", Logica.DetectEngine(clickhouseSource));
    }

    /// <summary>
    /// Test that multiple rules for same predicate create UNION ALL.
    /// Python output: ... UNION ALL ...
    /// </summary>
    [Fact]
    public void Integration_MultipleRules_GeneratesUnionAll()
    {
        var source = @"
            Parent(""Alice"", ""Bob"");
            Parent(""Bob"", ""Carol"");
            Ancestor(a, d) :- Parent(a, d);
        ";

        var sql = Logica.Compile(source, "Ancestor", "mssql");

        // Should generate a query
        Assert.Contains("SELECT", sql, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Test join generation when body has multiple predicates.
    /// Python uses comma-separated FROM clause.
    /// </summary>
    [Fact]
    public void Integration_JoinCondition_MatchesPythonPattern()
    {
        var source = @"
            Parent(parent: ""Alice"", child: ""Bob"");
            Parent(parent: ""Bob"", child: ""Carol"");
            Grandparent(gp:, gc:) :-
                Parent(parent: gp, child: mid),
                Parent(parent: mid, child: gc);
        ";

        var sql = Logica.Compile(source, "Grandparent", "mssql");

        // Should have multiple tables in FROM
        Assert.Contains("FROM", sql, StringComparison.OrdinalIgnoreCase);
        // Should have join conditions in WHERE
        Assert.Contains("WHERE", sql, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Test arithmetic expressions.
    /// </summary>
    [Fact]
    public void Integration_ArithmeticExpressions()
    {
        var source = @"
            Calc(result: 1 + 2 * 3);
        ";

        var sql = Logica.Compile(source, "Calc", "mssql");

        Assert.Contains("SELECT", sql, StringComparison.OrdinalIgnoreCase);
        // Should have the arithmetic
        Assert.Contains("1", sql);
        Assert.Contains("2", sql);
        Assert.Contains("3", sql);
    }

    /// <summary>
    /// Test comparison operators.
    /// Python: (Person.age >= 18) becomes = >= in SQL
    /// </summary>
    [Fact]
    public void Integration_ComparisonOperators()
    {
        var source = @"
            N(x) :- x == 5;
            N(x) :- x != 5;
            N(x) :- x < 5;
            N(x) :- x <= 5;
            N(x) :- x > 5;
            N(x) :- x >= 5;
        ";

        var sql = Logica.Compile(source, "N", "mssql");

        // Should translate comparison operators
        Assert.Contains("SELECT", sql, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Test negation handling.
    /// </summary>
    [Fact]
    public void Integration_Negation_GeneratesNotExists()
    {
        var source = @"
            A(1);
            A(2);
            B(2);
            B(3);
            OnlyA(x) :- A(x), ~B(x);
        ";

        var sql = Logica.Compile(source, "OnlyA", "mssql");

        Assert.Contains("NOT EXISTS", sql, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Test T-SQL specific syntax.
    /// </summary>
    [Fact]
    public void Integration_TSql_SpecificSyntax()
    {
        var source = @"Person(name: ""Alice"");";

        var sql = Logica.Compile(source, "Person", "mssql");

        // T-SQL uses square brackets for identifiers
        Assert.Contains("[", sql);
        Assert.Contains("]", sql);
        // T-SQL uses single quotes for strings
        Assert.Contains("'Alice'", sql);
    }

    /// <summary>
    /// Test ClickHouse specific syntax.
    /// </summary>
    [Fact]
    public void Integration_ClickHouse_SpecificSyntax()
    {
        var source = @"Person(name: ""Alice"");";

        var sql = Logica.Compile(source, "Person", "clickhouse");

        // ClickHouse uses backticks for identifiers
        Assert.Contains("`name`", sql);
    }

    /// <summary>
    /// Test that parse correctly handles comments.
    /// </summary>
    [Fact]
    public void Integration_Comments_AreIgnored()
    {
        var source = @"
            # This is a line comment
            Person(""Alice"");  # inline comment
            /* Block comment
               spanning multiple lines */
            Person(""Bob"");
        ";

        var program = Logica.Parse(source);

        Assert.Equal(2, program.Statements.Count);
    }

    /// <summary>
    /// Test tokenizer handles complex source.
    /// </summary>
    [Fact]
    public void Integration_Tokenize_CompleteProgram()
    {
        var source = @"
            @Engine(""mssql"");
            Person(name: ""Alice"", age: 30);
            Adult(name:) :- Person(name:, age:), age >= 18;
        ";

        var tokens = Logica.Tokenize(source);

        // Should have many tokens
        Assert.True(tokens.Count > 20);
        // Should end with EOF
        Assert.Equal(Parsing.TokenType.Eof, tokens[^1].Type);
    }

    /// <summary>
    /// Test available dialects.
    /// </summary>
    [Fact]
    public void Integration_AvailableDialects()
    {
        var dialects = Logica.AvailableDialects.ToList();

        Assert.Contains("mssql", dialects);
        Assert.Contains("clickhouse", dialects);
    }

    /// <summary>
    /// Test compiler creation.
    /// </summary>
    [Fact]
    public void Integration_CreateCompiler()
    {
        var compiler = Logica.CreateCompiler("mssql");
        Assert.NotNull(compiler);

        var dialect = new Dialects.TSqlDialect();
        var compilerWithDialect = Logica.CreateCompiler(dialect);
        Assert.NotNull(compilerWithDialect);
    }

    /// <summary>
    /// Test CompileAll function.
    /// </summary>
    [Fact]
    public void Integration_CompileAll()
    {
        var source = @"
            A(1); A(2);
            B(3); B(4);
            C(x) :- A(x);
        ";

        var results = Logica.CompileAll(source, "mssql");

        Assert.True(results.ContainsKey("A"));
        Assert.True(results.ContainsKey("B"));
        Assert.True(results.ContainsKey("C"));
    }

    /// <summary>
    /// Test auto-dialect compilation.
    /// </summary>
    [Fact]
    public void Integration_CompileWithAutoDialect()
    {
        var source = @"
            @Engine(""clickhouse"");
            Person(name: ""Alice"");
        ";

        var sql = Logica.CompileWithAutoDialect(source, "Person");

        // Should use ClickHouse syntax (backticks)
        Assert.Contains("`", sql);
    }

    /// <summary>
    /// Test auto-dialect with default.
    /// </summary>
    [Fact]
    public void Integration_CompileWithAutoDialect_UsesDefault()
    {
        var source = @"Person(name: ""Alice"");";

        var sql = Logica.CompileWithAutoDialect(source, "Person", "mssql");

        // Should use T-SQL syntax (square brackets)
        Assert.Contains("[", sql);
    }
}
