namespace LogicaSharp.Dialects;

/// <summary>
/// ClickHouse dialect implementation.
/// </summary>
public class ClickHouseDialect : DialectBase
{
    public override string Name => "clickhouse";

    public override IReadOnlyDictionary<string, string> BuiltInFunctions => new Dictionary<string, string>(base.BuiltInFunctions)
    {
        ["Abs"] = "abs(%s)",
        ["Ceil"] = "ceil(%s)",
        ["Floor"] = "floor(%s)",
        ["Round"] = "round(%s)",
        ["Sqrt"] = "sqrt(%s)",
        ["Log"] = "log(%s)",
        ["Log2"] = "log2(%s)",
        ["Log10"] = "log10(%s)",
        ["Exp"] = "exp(%s)",
        ["Sin"] = "sin(%s)",
        ["Cos"] = "cos(%s)",
        ["Tan"] = "tan(%s)",
        ["Length"] = "length(%s)",
        ["Size"] = "length(%s)",
        ["ArrayLength"] = "length(%s)",
        ["Upper"] = "upper(%s)",
        ["Lower"] = "lower(%s)",
        ["Trim"] = "trimBoth(%s)",
        ["LTrim"] = "trimLeft(%s)",
        ["RTrim"] = "trimRight(%s)",
        ["Concat"] = "concat(%s)",
        ["Replace"] = "replaceAll(%s, %s, %s)",
        ["Coalesce"] = "coalesce(%s)",
        ["NullIf"] = "nullIf(%s, %s)",
        ["Sum"] = "sum(%s)",
        ["Avg"] = "avg(%s)",
        ["Min"] = "min(%s)",
        ["Max"] = "max(%s)",
        ["Count"] = "count(%s)",
        ["CountDistinct"] = "uniq(%s)",
        ["ApproxCountDistinct"] = "uniq(%s)",
        ["ToString"] = "toString(%s)",
        ["ToInt64"] = "toInt64(%s)",
        ["ToFloat64"] = "toFloat64(%s)",
        ["ToInt32"] = "toInt32(%s)",
        ["ToUInt64"] = "toUInt64(%s)",
        ["ToDate"] = "toDate(%s)",
        ["ToDateTime"] = "toDateTime(%s)",
        ["Range"] = "range(%s)",
        ["Element"] = "arrayElement(%s, %s)",
        ["ArrayElement"] = "arrayElement(%s, %s)",
        ["ArrayJoin"] = "arrayJoin(%s)",
        ["ArrayConcat"] = "arrayConcat(%s)",
        ["ArraySort"] = "arraySort(%s)",
        ["ArrayReverse"] = "arrayReverse(%s)",
        ["ArraySlice"] = "arraySlice(%s, %s, %s)",
        ["ArrayMap"] = "arrayMap(%s, %s)",
        ["ArrayFilter"] = "arrayFilter(%s, %s)",
        ["ArrayExists"] = "arrayExists(%s, %s)",
        ["ArrayAll"] = "arrayAll(%s, %s)",
        ["ArrayFirst"] = "arrayFirst(%s, %s)",
        ["ArrayReduce"] = "arrayReduce(%s, %s)",
        ["ArrayUniq"] = "arrayUniq(%s)",
        ["ArrayDistinct"] = "arrayDistinct(%s)",
        ["ArrayFlatten"] = "arrayFlatten(%s)",
        ["ArrayStringConcat"] = "arrayStringConcat(%s, %s)",
        ["Has"] = "has(%s, %s)",
        ["HasAll"] = "hasAll(%s, %s)",
        ["HasAny"] = "hasAny(%s, %s)",
        ["Empty"] = "empty(%s)",
        ["NotEmpty"] = "notEmpty(%s)",
        ["Split"] = "splitByChar(%s, %s)",
        ["SplitByString"] = "splitByString(%s, %s)",
        ["Greatest"] = "greatest(%s, %s)",
        ["Least"] = "least(%s, %s)",
        ["Left"] = "left(%s, %s)",
        ["Right"] = "right(%s, %s)",
        ["CharLength"] = "lengthUTF8(%s)",
        ["Reverse"] = "reverse(%s)",
        ["Year"] = "toYear(%s)",
        ["Month"] = "toMonth(%s)",
        ["Day"] = "toDayOfMonth(%s)",
        ["DayOfWeek"] = "toDayOfWeek(%s)",
        ["DayOfYear"] = "toDayOfYear(%s)",
        ["Hour"] = "toHour(%s)",
        ["Minute"] = "toMinute(%s)",
        ["Second"] = "toSecond(%s)",
        ["Now"] = "now()",
        ["Today"] = "today()",
        ["Yesterday"] = "yesterday()",
        ["AddDays"] = "addDays(%s, %s)",
        ["AddMonths"] = "addMonths(%s, %s)",
        ["AddYears"] = "addYears(%s, %s)",
        ["SubtractDays"] = "subtractDays(%s, %s)",
        ["DateDiff"] = "dateDiff(%s, %s, %s)",
        ["If"] = "if(%s, %s, %s)",
        ["MultiIf"] = "multiIf(%s)",
        ["CityHash64"] = "cityHash64(%s)",
        ["Fingerprint"] = "cityHash64(%s)",
        ["MD5"] = "MD5(%s)",
        ["SHA256"] = "SHA256(%s)",
        ["ArgMin"] = "argMin(%s, %s)",
        ["ArgMax"] = "argMax(%s, %s)",
        ["GroupArray"] = "groupArray(%s)",
        ["GroupArrayDistinct"] = "groupUniqArray(%s)",
        ["GroupArrayInsertAt"] = "groupArrayInsertAt(%s, %s)",
        ["Quantile"] = "quantile(%s)(%s)",
        ["Median"] = "median(%s)",
        ["Any"] = "any(%s)",
        ["AnyLast"] = "anyLast(%s)",
        ["TopK"] = "topK(%s)(%s)",
        ["Tuple"] = "tuple(%s)",
        ["TupleElement"] = "tupleElement(%s, %s)",
        ["Map"] = "map(%s)",
        ["MapKeys"] = "mapKeys(%s)",
        ["MapValues"] = "mapValues(%s)",
        ["MapContains"] = "mapContains(%s, %s)",
    };

    public override IReadOnlyDictionary<string, string> InfixOperators => new Dictionary<string, string>(base.InfixOperators)
    {
        ["++"] = "concat(%s, %s)",
        ["&&"] = "(%s) AND (%s)",
        ["||"] = "(%s) OR (%s)",
        ["%"] = "modulo(%s, %s)",
        ["^"] = "pow(%s, %s)",
    };

    public override IReadOnlyDictionary<string, string> TypeMappings => new Dictionary<string, string>
    {
        ["INT64"] = "Int64",
        ["INT32"] = "Int32",
        ["INT16"] = "Int16",
        ["INT8"] = "Int8",
        ["UINT64"] = "UInt64",
        ["UINT32"] = "UInt32",
        ["UINT16"] = "UInt16",
        ["UINT8"] = "UInt8",
        ["FLOAT64"] = "Float64",
        ["FLOAT32"] = "Float32",
        ["STRING"] = "String",
        ["BOOL"] = "UInt8",
        ["DATE"] = "Date",
        ["TIMESTAMP"] = "DateTime",
        ["DATETIME"] = "DateTime",
        ["BYTES"] = "String",
        ["UUID"] = "UUID",
        ["DECIMAL"] = "Decimal",
        ["ARRAY"] = "Array",
    };

    public override string QuoteIdentifier(string identifier)
    {
        // Handle backtick-quoted identifiers
        if (identifier.StartsWith('`') && identifier.EndsWith('`'))
        {
            identifier = identifier[1..^1];
        }

        // ClickHouse uses backticks or double quotes
        return $"`{identifier.Replace("`", "\\`")}`";
    }

    public override string UnnestPhrase(string arrayExpr, string alias)
    {
        // ClickHouse uses ARRAY JOIN
        return $"ARRAY JOIN {arrayExpr} AS {alias}";
    }

    public override string ArrayPhrase(string elementsExpr)
    {
        return $"[{elementsExpr}]";
    }

    public override string StringAggFunction(string expr, string separator)
    {
        return $"arrayStringConcat(groupArray({expr}), {separator})";
    }

    public override string ArrayAggFunction(string expr, bool distinct = false)
    {
        if (distinct)
        {
            return $"groupUniqArray({expr})";
        }
        return $"groupArray({expr})";
    }

    public override string SubscriptExpr(string record, string subscript)
    {
        // ClickHouse uses array indexing (1-based)
        return $"{record}[{subscript}]";
    }

    public override string SubstringFunction(string str, string start, string length)
    {
        // ClickHouse substring is 1-indexed
        return $"substring({str}, {start}, {length})";
    }

    public override string ModuloOperator => "modulo";

    public override string PowerFunction(string baseExpr, string exponent)
    {
        return $"pow({baseExpr}, {exponent})";
    }

    public override string BooleanLiteral(bool value)
    {
        // ClickHouse uses 1/0 for boolean
        return value ? "1" : "0";
    }

    public override string CurrentTimestamp => "now()";

    public override string CastExpr(string expr, string targetType)
    {
        var upperType = targetType.ToUpperInvariant();
        if (TypeMappings.TryGetValue(upperType, out var mappedType))
        {
            targetType = mappedType;
        }

        // ClickHouse prefers toType functions
        return targetType switch
        {
            "Int64" => $"toInt64({expr})",
            "Int32" => $"toInt32({expr})",
            "Float64" => $"toFloat64({expr})",
            "Float32" => $"toFloat32({expr})",
            "String" => $"toString({expr})",
            "Date" => $"toDate({expr})",
            "DateTime" => $"toDateTime({expr})",
            "UInt64" => $"toUInt64({expr})",
            "UInt32" => $"toUInt32({expr})",
            "UInt8" => $"toUInt8({expr})",
            _ => $"CAST({expr} AS {targetType})"
        };
    }

    public override string RecursiveCte(string cteName, string anchorQuery, string recursiveQuery, string selectQuery)
    {
        // ClickHouse supports recursive CTEs with RECURSIVE keyword (from version 21.8+)
        return $@"WITH RECURSIVE {cteName} AS (
    {anchorQuery}
    UNION ALL
    {recursiveQuery}
)
{selectQuery}";
    }
}
