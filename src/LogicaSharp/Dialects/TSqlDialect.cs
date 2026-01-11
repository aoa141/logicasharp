namespace LogicaSharp.Dialects;

/// <summary>
/// T-SQL (Microsoft SQL Server) dialect implementation.
/// </summary>
public class TSqlDialect : DialectBase
{
    public override string Name => "mssql";

    public override IReadOnlyDictionary<string, string> BuiltInFunctions => new Dictionary<string, string>(base.BuiltInFunctions)
    {
        ["Abs"] = "ABS(%s)",
        ["Ceil"] = "CEILING(%s)",
        ["Floor"] = "FLOOR(%s)",
        ["Round"] = "ROUND(%s, 0)",
        ["Sqrt"] = "SQRT(%s)",
        ["Log"] = "LOG(%s)",
        ["Log10"] = "LOG10(%s)",
        ["Exp"] = "EXP(%s)",
        ["Sin"] = "SIN(%s)",
        ["Cos"] = "COS(%s)",
        ["Tan"] = "TAN(%s)",
        ["Length"] = "LEN(%s)",
        ["Size"] = "LEN(%s)",
        ["Upper"] = "UPPER(%s)",
        ["Lower"] = "LOWER(%s)",
        ["Trim"] = "LTRIM(RTRIM(%s))",
        ["LTrim"] = "LTRIM(%s)",
        ["RTrim"] = "RTRIM(%s)",
        ["Concat"] = "CONCAT(%s)",
        ["Replace"] = "REPLACE(%s, %s, %s)",
        ["Coalesce"] = "COALESCE(%s)",
        ["NullIf"] = "NULLIF(%s, %s)",
        ["Sum"] = "SUM(%s)",
        ["Avg"] = "AVG(%s)",
        ["Min"] = "MIN(%s)",
        ["Max"] = "MAX(%s)",
        ["Count"] = "COUNT(%s)",
        ["CountDistinct"] = "COUNT(DISTINCT %s)",
        ["ToString"] = "CAST(%s AS NVARCHAR(MAX))",
        ["ToInt64"] = "CAST(%s AS BIGINT)",
        ["ToFloat64"] = "CAST(%s AS FLOAT)",
        ["Range"] = "dbo.GenerateRange(%s)",
        ["Element"] = "JSON_VALUE(%s, %s)",
        ["ArrayLength"] = "JSON_QUERY(%s).value('count(/array/item)', 'int')",
        ["JsonExtract"] = "JSON_VALUE(%s, %s)",
        ["JsonExtractScalar"] = "JSON_VALUE(%s, %s)",
        ["Split"] = "STRING_SPLIT(%s, %s)",
        ["Greatest"] = "IIF(%s > %s, %s, %s)",
        ["Least"] = "IIF(%s < %s, %s, %s)",
        ["Left"] = "LEFT(%s, %s)",
        ["Right"] = "RIGHT(%s, %s)",
        ["CharLength"] = "LEN(%s)",
        ["Reverse"] = "REVERSE(%s)",
        ["Year"] = "YEAR(%s)",
        ["Month"] = "MONTH(%s)",
        ["Day"] = "DAY(%s)",
        ["Hour"] = "DATEPART(HOUR, %s)",
        ["Minute"] = "DATEPART(MINUTE, %s)",
        ["Second"] = "DATEPART(SECOND, %s)",
        ["DateAdd"] = "DATEADD(%s, %s, %s)",
        ["DateDiff"] = "DATEDIFF(%s, %s, %s)",
        ["GetDate"] = "GETDATE()",
        ["GetUtcDate"] = "GETUTCDATE()",
        ["NewId"] = "NEWID()",
        ["IsNull"] = "ISNULL(%s, %s)",
        ["IIF"] = "IIF(%s, %s, %s)",
        ["Fingerprint"] = "CAST(HASHBYTES('MD5', CAST(%s AS NVARCHAR(MAX))) AS BIGINT)",
    };

    public override IReadOnlyDictionary<string, string> InfixOperators => new Dictionary<string, string>(base.InfixOperators)
    {
        ["++"] = "CONCAT(%s, %s)",
        ["&&"] = "(%s) AND (%s)",
        ["||"] = "(%s) OR (%s)",
        ["%"] = "(%s) %% (%s)",
    };

    public override IReadOnlyDictionary<string, string> TypeMappings => new Dictionary<string, string>
    {
        ["INT64"] = "BIGINT",
        ["FLOAT64"] = "FLOAT",
        ["STRING"] = "NVARCHAR(MAX)",
        ["BOOL"] = "BIT",
        ["DATE"] = "DATE",
        ["TIMESTAMP"] = "DATETIME2",
        ["DATETIME"] = "DATETIME2",
        ["BYTES"] = "VARBINARY(MAX)",
        ["INT"] = "INT",
        ["SMALLINT"] = "SMALLINT",
        ["TINYINT"] = "TINYINT",
        ["DECIMAL"] = "DECIMAL",
        ["NUMERIC"] = "NUMERIC",
        ["MONEY"] = "MONEY",
        ["REAL"] = "REAL",
        ["TEXT"] = "NVARCHAR(MAX)",
        ["NTEXT"] = "NVARCHAR(MAX)",
        ["VARCHAR"] = "VARCHAR",
        ["NVARCHAR"] = "NVARCHAR",
        ["CHAR"] = "CHAR",
        ["NCHAR"] = "NCHAR",
    };

    public override string QuoteIdentifier(string identifier)
    {
        // Handle backtick-quoted identifiers - convert to brackets
        if (identifier.StartsWith('`') && identifier.EndsWith('`'))
        {
            identifier = identifier[1..^1];
        }

        // Handle dotted identifiers (schema.table)
        if (identifier.Contains('.'))
        {
            var parts = identifier.Split('.');
            return string.Join(".", parts.Select(p => $"[{p.Replace("]", "]]")}]"));
        }

        // T-SQL uses square brackets
        return $"[{identifier.Replace("]", "]]")}]";
    }

    public override string UnnestPhrase(string arrayExpr, string alias)
    {
        // T-SQL uses OPENJSON for array expansion
        return $"OPENJSON({arrayExpr}) WITH (value NVARCHAR(MAX) '$') AS {alias}";
    }

    public override string ArrayPhrase(string elementsExpr)
    {
        // T-SQL doesn't have native arrays, use JSON
        return $"(SELECT value FROM (VALUES {elementsExpr}) AS t(value) FOR JSON PATH)";
    }

    public override string StringAggFunction(string expr, string separator)
    {
        return $"STRING_AGG({expr}, {separator})";
    }

    public override string ArrayAggFunction(string expr, bool distinct = false)
    {
        // T-SQL uses STRING_AGG with FOR JSON for array aggregation
        if (distinct)
        {
            return $"(SELECT DISTINCT {expr} AS item FOR JSON PATH)";
        }
        return $"(SELECT {expr} AS item FOR JSON PATH)";
    }

    public override string SubscriptExpr(string record, string subscript)
    {
        // T-SQL uses JSON_VALUE for subscripting
        return $"JSON_VALUE({record}, {subscript})";
    }

    public override string SubstringFunction(string str, string start, string length)
    {
        // T-SQL SUBSTRING is 1-indexed
        return $"SUBSTRING({str}, {start}, {length})";
    }

    public override string BooleanLiteral(bool value)
    {
        // T-SQL uses 1/0 for BIT type
        return value ? "1" : "0";
    }

    public override string CurrentTimestamp => "GETDATE()";

    public override string RecursiveCte(string cteName, string anchorQuery, string recursiveQuery, string selectQuery)
    {
        // T-SQL doesn't use RECURSIVE keyword
        return $@"WITH {cteName} AS (
    {anchorQuery}
    UNION ALL
    {recursiveQuery}
)
{selectQuery}";
    }

    public override string CastExpr(string expr, string targetType)
    {
        var upperType = targetType.ToUpperInvariant();
        if (TypeMappings.TryGetValue(upperType, out var mappedType))
        {
            targetType = mappedType;
        }

        // Handle special cases
        return targetType.ToUpperInvariant() switch
        {
            "STRING" => $"CAST({expr} AS NVARCHAR(MAX))",
            "INT64" => $"CAST({expr} AS BIGINT)",
            "FLOAT64" => $"CAST({expr} AS FLOAT)",
            "BOOL" => $"CAST({expr} AS BIT)",
            _ => $"CAST({expr} AS {targetType})"
        };
    }

    public override string PowerFunction(string baseExpr, string exponent)
    {
        return $"POWER({baseExpr}, {exponent})";
    }
}
