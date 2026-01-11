namespace LogicaSharp.Dialects;

/// <summary>
/// Base class for SQL dialect implementations with common functionality.
/// </summary>
public abstract class DialectBase : IDialect
{
    public abstract string Name { get; }

    public virtual IReadOnlyDictionary<string, string> BuiltInFunctions => new Dictionary<string, string>
    {
        ["Abs"] = "ABS(%s)",
        ["Ceil"] = "CEIL(%s)",
        ["Floor"] = "FLOOR(%s)",
        ["Round"] = "ROUND(%s)",
        ["Sqrt"] = "SQRT(%s)",
        ["Log"] = "LOG(%s)",
        ["Exp"] = "EXP(%s)",
        ["Sin"] = "SIN(%s)",
        ["Cos"] = "COS(%s)",
        ["Tan"] = "TAN(%s)",
        ["Length"] = "LENGTH(%s)",
        ["Upper"] = "UPPER(%s)",
        ["Lower"] = "LOWER(%s)",
        ["Trim"] = "TRIM(%s)",
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
    };

    public virtual IReadOnlyDictionary<string, string> InfixOperators => new Dictionary<string, string>
    {
        ["+"] = "(%s) + (%s)",
        ["-"] = "(%s) - (%s)",
        ["*"] = "(%s) * (%s)",
        ["/"] = "(%s) / (%s)",
        ["%"] = "(%s) %% (%s)",
        ["=="] = "(%s) = (%s)",
        ["!="] = "(%s) <> (%s)",
        ["<"] = "(%s) < (%s)",
        ["<="] = "(%s) <= (%s)",
        [">"] = "(%s) > (%s)",
        [">="] = "(%s) >= (%s)",
        ["&&"] = "(%s) AND (%s)",
        ["||"] = "(%s) OR (%s)",
        ["++"] = "CONCAT(%s, %s)",
    };

    public virtual IReadOnlyDictionary<string, string> TypeMappings => new Dictionary<string, string>
    {
        ["INT64"] = "BIGINT",
        ["FLOAT64"] = "FLOAT",
        ["STRING"] = "VARCHAR",
        ["BOOL"] = "BOOLEAN",
        ["DATE"] = "DATE",
        ["TIMESTAMP"] = "TIMESTAMP",
        ["BYTES"] = "VARBINARY",
    };

    public virtual string QuoteIdentifier(string identifier)
    {
        // Handle backtick-quoted identifiers
        if (identifier.StartsWith('`') && identifier.EndsWith('`'))
        {
            identifier = identifier[1..^1];
        }

        // Default: double quotes
        return $"\"{identifier.Replace("\"", "\"\"")}\"";
    }

    public virtual string QuoteString(string value)
    {
        return $"'{value.Replace("'", "''")}'";
    }

    public virtual string UnnestPhrase(string arrayExpr, string alias)
    {
        return $"UNNEST({arrayExpr}) AS {alias}";
    }

    public virtual string ArrayPhrase(string elementsExpr)
    {
        return $"ARRAY[{elementsExpr}]";
    }

    public virtual string GroupBySpec => "GROUP BY";

    public virtual string CoalesceFunction => "COALESCE";

    public virtual string StringAggFunction(string expr, string separator)
    {
        return $"STRING_AGG({expr}, {separator})";
    }

    public virtual string ArrayAggFunction(string expr, bool distinct = false)
    {
        var distinctClause = distinct ? "DISTINCT " : "";
        return $"ARRAY_AGG({distinctClause}{expr})";
    }

    public virtual string CastExpr(string expr, string targetType)
    {
        if (TypeMappings.TryGetValue(targetType.ToUpperInvariant(), out var mappedType))
        {
            targetType = mappedType;
        }
        return $"CAST({expr} AS {targetType})";
    }

    public virtual string SubscriptExpr(string record, string subscript)
    {
        return $"{record}[{subscript}]";
    }

    public virtual string SubstringFunction(string str, string start, string length)
    {
        return $"SUBSTRING({str}, {start}, {length})";
    }

    public virtual string ModuloOperator => "%";

    public virtual string PowerFunction(string baseExpr, string exponent)
    {
        return $"POWER({baseExpr}, {exponent})";
    }

    public virtual string BooleanLiteral(bool value)
    {
        return value ? "TRUE" : "FALSE";
    }

    public virtual string NullLiteral => "NULL";

    public virtual string CurrentTimestamp => "CURRENT_TIMESTAMP";

    public virtual string RecursiveCte(string cteName, string anchorQuery, string recursiveQuery, string selectQuery)
    {
        return $@"WITH RECURSIVE {cteName} AS (
    {anchorQuery}
    UNION ALL
    {recursiveQuery}
)
{selectQuery}";
    }
}
