using LogicaSharp.Dialects;

namespace LogicaSharp.Tests;

public class DialectTests
{
    [Fact]
    public void TSqlDialect_QuoteIdentifier_UsesSquareBrackets()
    {
        var dialect = new TSqlDialect();

        Assert.Equal("[table]", dialect.QuoteIdentifier("table"));
        Assert.Equal("[schema].[table]", dialect.QuoteIdentifier("schema.table"));
    }

    [Fact]
    public void TSqlDialect_QuoteIdentifier_HandlesBackticks()
    {
        var dialect = new TSqlDialect();

        Assert.Equal("[my table]", dialect.QuoteIdentifier("`my table`"));
    }

    [Fact]
    public void TSqlDialect_QuoteString_UsesSingleQuotes()
    {
        var dialect = new TSqlDialect();

        Assert.Equal("'hello'", dialect.QuoteString("hello"));
        Assert.Equal("'it''s'", dialect.QuoteString("it's"));
    }

    [Fact]
    public void TSqlDialect_BooleanLiteral_Uses1And0()
    {
        var dialect = new TSqlDialect();

        Assert.Equal("1", dialect.BooleanLiteral(true));
        Assert.Equal("0", dialect.BooleanLiteral(false));
    }

    [Fact]
    public void TSqlDialect_CastExpr_MapsTypes()
    {
        var dialect = new TSqlDialect();

        Assert.Equal("CAST(x AS BIGINT)", dialect.CastExpr("x", "INT64"));
        Assert.Equal("CAST(x AS FLOAT)", dialect.CastExpr("x", "FLOAT64"));
        Assert.Equal("CAST(x AS NVARCHAR(MAX))", dialect.CastExpr("x", "STRING"));
    }

    [Fact]
    public void TSqlDialect_StringAggFunction_UsesStringAgg()
    {
        var dialect = new TSqlDialect();

        Assert.Equal("STRING_AGG(x, ',')", dialect.StringAggFunction("x", "','"));
    }

    [Fact]
    public void TSqlDialect_UnnestPhrase_UsesOpenJson()
    {
        var dialect = new TSqlDialect();

        var result = dialect.UnnestPhrase("[1,2,3]", "arr");
        Assert.Contains("OPENJSON", result);
    }

    [Fact]
    public void TSqlDialect_RecursiveCte_OmitsRecursiveKeyword()
    {
        var dialect = new TSqlDialect();

        var result = dialect.RecursiveCte("cte", "SELECT 1", "SELECT 2", "SELECT * FROM cte");
        Assert.DoesNotContain("RECURSIVE", result);
        Assert.Contains("WITH cte AS", result);
    }

    [Fact]
    public void ClickHouseDialect_QuoteIdentifier_UsesBackticks()
    {
        var dialect = new ClickHouseDialect();

        Assert.Equal("`table`", dialect.QuoteIdentifier("table"));
    }

    [Fact]
    public void ClickHouseDialect_BooleanLiteral_Uses1And0()
    {
        var dialect = new ClickHouseDialect();

        Assert.Equal("1", dialect.BooleanLiteral(true));
        Assert.Equal("0", dialect.BooleanLiteral(false));
    }

    [Fact]
    public void ClickHouseDialect_CastExpr_UsesToFunctions()
    {
        var dialect = new ClickHouseDialect();

        Assert.Equal("toInt64(x)", dialect.CastExpr("x", "INT64"));
        Assert.Equal("toFloat64(x)", dialect.CastExpr("x", "FLOAT64"));
        Assert.Equal("toString(x)", dialect.CastExpr("x", "STRING"));
    }

    [Fact]
    public void ClickHouseDialect_ArrayAggFunction_UsesGroupArray()
    {
        var dialect = new ClickHouseDialect();

        Assert.Equal("groupArray(x)", dialect.ArrayAggFunction("x"));
        Assert.Equal("groupUniqArray(x)", dialect.ArrayAggFunction("x", distinct: true));
    }

    [Fact]
    public void ClickHouseDialect_StringAggFunction_UsesArrayStringConcat()
    {
        var dialect = new ClickHouseDialect();

        var result = dialect.StringAggFunction("x", "','");
        Assert.Contains("arrayStringConcat", result);
        Assert.Contains("groupArray", result);
    }

    [Fact]
    public void ClickHouseDialect_UnnestPhrase_UsesArrayJoin()
    {
        var dialect = new ClickHouseDialect();

        var result = dialect.UnnestPhrase("arr", "x");
        Assert.Equal("ARRAY JOIN arr AS x", result);
    }

    [Fact]
    public void ClickHouseDialect_PowerFunction_UsesPow()
    {
        var dialect = new ClickHouseDialect();

        Assert.Equal("pow(2, 3)", dialect.PowerFunction("2", "3"));
    }

    [Fact]
    public void ClickHouseDialect_RecursiveCte_IncludesRecursiveKeyword()
    {
        var dialect = new ClickHouseDialect();

        var result = dialect.RecursiveCte("cte", "SELECT 1", "SELECT 2", "SELECT * FROM cte");
        Assert.Contains("WITH RECURSIVE cte AS", result);
    }

    [Fact]
    public void DialectRegistry_Get_ReturnsTSql()
    {
        var dialect = DialectRegistry.Get("mssql");
        Assert.IsType<TSqlDialect>(dialect);

        dialect = DialectRegistry.Get("tsql");
        Assert.IsType<TSqlDialect>(dialect);

        dialect = DialectRegistry.Get("sqlserver");
        Assert.IsType<TSqlDialect>(dialect);
    }

    [Fact]
    public void DialectRegistry_Get_ReturnsClickHouse()
    {
        var dialect = DialectRegistry.Get("clickhouse");
        Assert.IsType<ClickHouseDialect>(dialect);
    }

    [Fact]
    public void DialectRegistry_Get_ThrowsForUnknown()
    {
        Assert.Throws<ArgumentException>(() => DialectRegistry.Get("unknown"));
    }

    [Fact]
    public void DialectRegistry_AvailableDialects_ListsAll()
    {
        var dialects = DialectRegistry.AvailableDialects.ToList();

        Assert.Contains("mssql", dialects);
        Assert.Contains("clickhouse", dialects);
    }

    [Fact]
    public void TSqlDialect_InfixOperators_HasPlusPlus()
    {
        var dialect = new TSqlDialect();

        Assert.True(dialect.InfixOperators.ContainsKey("++"));
        Assert.Contains("CONCAT", dialect.InfixOperators["++"]);
    }

    [Fact]
    public void ClickHouseDialect_InfixOperators_HasPlusPlus()
    {
        var dialect = new ClickHouseDialect();

        Assert.True(dialect.InfixOperators.ContainsKey("++"));
        Assert.Contains("concat", dialect.InfixOperators["++"]);
    }

    [Fact]
    public void TSqlDialect_TypeMappings_HasCommonTypes()
    {
        var dialect = new TSqlDialect();

        Assert.Equal("BIGINT", dialect.TypeMappings["INT64"]);
        Assert.Equal("FLOAT", dialect.TypeMappings["FLOAT64"]);
        Assert.Equal("NVARCHAR(MAX)", dialect.TypeMappings["STRING"]);
        Assert.Equal("BIT", dialect.TypeMappings["BOOL"]);
    }

    [Fact]
    public void ClickHouseDialect_TypeMappings_HasCommonTypes()
    {
        var dialect = new ClickHouseDialect();

        Assert.Equal("Int64", dialect.TypeMappings["INT64"]);
        Assert.Equal("Float64", dialect.TypeMappings["FLOAT64"]);
        Assert.Equal("String", dialect.TypeMappings["STRING"]);
        Assert.Equal("UInt8", dialect.TypeMappings["BOOL"]);
    }

    [Fact]
    public void TSqlDialect_BuiltInFunctions_HasCommonFunctions()
    {
        var dialect = new TSqlDialect();

        Assert.True(dialect.BuiltInFunctions.ContainsKey("Abs"));
        Assert.True(dialect.BuiltInFunctions.ContainsKey("Length"));
        Assert.True(dialect.BuiltInFunctions.ContainsKey("Upper"));
        Assert.True(dialect.BuiltInFunctions.ContainsKey("Sum"));
        Assert.True(dialect.BuiltInFunctions.ContainsKey("Fingerprint"));
    }

    [Fact]
    public void ClickHouseDialect_BuiltInFunctions_HasCommonFunctions()
    {
        var dialect = new ClickHouseDialect();

        Assert.True(dialect.BuiltInFunctions.ContainsKey("Abs"));
        Assert.True(dialect.BuiltInFunctions.ContainsKey("Length"));
        Assert.True(dialect.BuiltInFunctions.ContainsKey("Upper"));
        Assert.True(dialect.BuiltInFunctions.ContainsKey("Sum"));
        Assert.True(dialect.BuiltInFunctions.ContainsKey("Fingerprint"));
        Assert.True(dialect.BuiltInFunctions.ContainsKey("ArgMin"));
        Assert.True(dialect.BuiltInFunctions.ContainsKey("ArgMax"));
    }

    [Fact]
    public void TSqlDialect_Name_ReturnsMssql()
    {
        var dialect = new TSqlDialect();
        Assert.Equal("mssql", dialect.Name);
    }

    [Fact]
    public void ClickHouseDialect_Name_ReturnsClickhouse()
    {
        var dialect = new ClickHouseDialect();
        Assert.Equal("clickhouse", dialect.Name);
    }
}
