using System.Globalization;
using System.Text;
using LogicaSharp.Ast;
using LogicaSharp.Dialects;

namespace LogicaSharp.Compilation;

/// <summary>
/// Translates Logica expressions to SQL.
/// </summary>
public class ExpressionTranslator
{
    private readonly IDialect _dialect;
    private readonly Dictionary<string, string> _variableBindings;
    private readonly CompilationContext _context;

    public ExpressionTranslator(IDialect dialect, CompilationContext context)
    {
        _dialect = dialect;
        _context = context;
        _variableBindings = new Dictionary<string, string>();
    }

    /// <summary>
    /// Binds a variable name to a SQL expression.
    /// </summary>
    public void BindVariable(string variable, string sqlExpr)
    {
        _variableBindings[variable] = sqlExpr;
    }

    /// <summary>
    /// Translates an expression to SQL.
    /// </summary>
    public string Translate(IExpression expr)
    {
        return expr switch
        {
            Variable v => TranslateVariable(v),
            NumberLiteral n => TranslateNumber(n),
            StringLiteral s => TranslateString(s),
            BooleanLiteral b => TranslateBoolean(b),
            NullLiteral => _dialect.NullLiteral,
            BinaryOp bin => TranslateBinaryOp(bin),
            UnaryOp un => TranslateUnaryOp(un),
            PredicateCall call => TranslateCall(call),
            ListLiteral list => TranslateList(list),
            Record rec => TranslateRecord(rec),
            Subscript sub => TranslateSubscript(sub),
            InExpression inExpr => TranslateIn(inExpr),
            CastExpr cast => TranslateCast(cast),
            ConditionalExpr cond => TranslateConditional(cond),
            Aggregation agg => TranslateAggregation(agg),
            SqlExpr sql => TranslateSqlExpr(sql),
            Lambda lambda => TranslateLambda(lambda),
            _ => throw new CompilationException($"Unsupported expression type: {expr.GetType().Name}")
        };
    }

    private string TranslateVariable(Variable v)
    {
        if (_variableBindings.TryGetValue(v.Name, out var binding))
        {
            return binding;
        }

        // Return as-is if not bound (will be resolved later or is a column reference)
        return _dialect.QuoteIdentifier(v.Name);
    }

    private string TranslateNumber(NumberLiteral n)
    {
        // Format with invariant culture to avoid locale issues
        if (n.Value == Math.Floor(n.Value) && Math.Abs(n.Value) < long.MaxValue)
        {
            return ((long)n.Value).ToString(CultureInfo.InvariantCulture);
        }
        return n.Value.ToString("G17", CultureInfo.InvariantCulture);
    }

    private string TranslateString(StringLiteral s)
    {
        return _dialect.QuoteString(s.Value);
    }

    private string TranslateBoolean(BooleanLiteral b)
    {
        return _dialect.BooleanLiteral(b.Value);
    }

    private string TranslateBinaryOp(BinaryOp bin)
    {
        var left = Translate(bin.Left);
        var right = Translate(bin.Right);

        // Handle arrow operator for map creation
        if (bin.Operator == "->")
        {
            return TranslateMapEntry(left, right);
        }

        // Handle power operator specially
        if (bin.Operator == "^")
        {
            return _dialect.PowerFunction(left, right);
        }

        // Look up operator template
        if (_dialect.InfixOperators.TryGetValue(bin.Operator, out var template))
        {
            // Template uses %s placeholders, replace them in order
            var result = template;
            result = ReplaceFirst(result, "%s", left);
            result = ReplaceFirst(result, "%s", right);
            return result;
        }

        // Default: use standard SQL operators
        var sqlOp = bin.Operator switch
        {
            "==" => "=",
            "!=" => "<>",
            "&&" => "AND",
            "||" => "OR",
            _ => bin.Operator
        };

        return $"({left}) {sqlOp} ({right})";
    }

    private string TranslateMapEntry(string key, string value)
    {
        // This is used within list literals for map-like structures
        return $"({key}, {value})";
    }

    private string TranslateUnaryOp(UnaryOp un)
    {
        var operand = Translate(un.Operand);

        return un.Operator switch
        {
            "-" => $"-({operand})",
            "!" or "~" => $"NOT ({operand})",
            _ => throw new CompilationException($"Unknown unary operator: {un.Operator}")
        };
    }

    private string TranslateCall(PredicateCall call)
    {
        // Check if it's a built-in function
        if (_dialect.BuiltInFunctions.TryGetValue(call.PredicateName, out var template))
        {
            return TranslateBuiltInFunction(call, template);
        }

        // Check for special functions
        return call.PredicateName switch
        {
            "Cast" => TranslateCastFunction(call),
            "TryCast" => TranslateTryCastFunction(call),
            "SqlExpr" => TranslateSqlExprFunction(call),
            "If" => TranslateIfFunction(call),
            "Coalesce" => TranslateCoalesceFunction(call),
            "Range" => TranslateRangeFunction(call),
            "List" => TranslateListFunction(call),
            "Array" => TranslateArrayFunction(call),
            "Join" => TranslateJoinFunction(call),
            _ => TranslatePredicateReference(call)
        };
    }

    private string TranslateBuiltInFunction(PredicateCall call, string template)
    {
        var args = call.Arguments.Fields
            .Where(f => f.Value != null)
            .Select(f => Translate(f.Value!))
            .ToList();

        // Count %s placeholders
        var placeholderCount = template.Split("%s").Length - 1;

        // If template has more placeholders than args, some args might need to be repeated
        // (for functions like Greatest/Least that compare same values)
        while (args.Count < placeholderCount && args.Count > 0)
        {
            args.Add(args[0]);
        }

        // Replace %s placeholders with arguments
        var result = template;
        foreach (var arg in args)
        {
            var idx = result.IndexOf("%s", StringComparison.Ordinal);
            if (idx >= 0)
            {
                result = result[..idx] + arg + result[(idx + 2)..];
            }
        }

        return result;
    }

    private string TranslateCastFunction(PredicateCall call)
    {
        var args = call.Arguments.Fields;
        if (args.Count < 2)
        {
            throw new CompilationException("Cast requires value and type arguments");
        }

        var value = Translate(args[0].Value!);
        var targetType = args[1].Value switch
        {
            StringLiteral s => s.Value,
            Variable v => v.Name,
            _ => throw new CompilationException("Cast type must be a string or identifier")
        };

        return _dialect.CastExpr(value, targetType);
    }

    private string TranslateTryCastFunction(PredicateCall call)
    {
        // TryCast is like Cast but returns NULL on failure
        // Not all dialects support this, so we implement it as a best effort
        return TranslateCastFunction(call);
    }

    private string TranslateSqlExprFunction(PredicateCall call)
    {
        var args = call.Arguments.Fields;
        if (args.Count < 1)
        {
            throw new CompilationException("SqlExpr requires a template argument");
        }

        var template = args[0].Value switch
        {
            StringLiteral s => s.Value,
            _ => throw new CompilationException("SqlExpr template must be a string")
        };

        // If there are parameter bindings, substitute them
        if (args.Count > 1 && args[1].Value is Record paramRecord)
        {
            foreach (var field in paramRecord.Fields)
            {
                if (field.Value != null)
                {
                    var paramValue = Translate(field.Value);
                    template = template.Replace($"{{{field.Field}}}", paramValue);
                }
            }
        }

        return template;
    }

    private string TranslateIfFunction(PredicateCall call)
    {
        var args = call.Arguments.Fields;
        if (args.Count < 3)
        {
            throw new CompilationException("If requires condition, then, and else arguments");
        }

        var condition = Translate(args[0].Value!);
        var thenExpr = Translate(args[1].Value!);
        var elseExpr = Translate(args[2].Value!);

        return $"CASE WHEN {condition} THEN {thenExpr} ELSE {elseExpr} END";
    }

    private string TranslateCoalesceFunction(PredicateCall call)
    {
        var args = call.Arguments.Fields
            .Where(f => f.Value != null)
            .Select(f => Translate(f.Value!));

        return $"{_dialect.CoalesceFunction}({string.Join(", ", args)})";
    }

    private string TranslateRangeFunction(PredicateCall call)
    {
        var args = call.Arguments.Fields;
        if (args.Count < 1)
        {
            throw new CompilationException("Range requires at least one argument");
        }

        var limit = Translate(args[0].Value!);

        // This depends heavily on the dialect
        if (_dialect is TSqlDialect)
        {
            // T-SQL doesn't have a built-in range function, use a numbers table or recursive CTE
            return $"(SELECT TOP ({limit}) ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) - 1 AS n FROM sys.objects)";
        }
        else if (_dialect is ClickHouseDialect)
        {
            return $"range({limit})";
        }

        // Default: try to use generate_series or similar
        return $"generate_series(0, {limit} - 1)";
    }

    private string TranslateListFunction(PredicateCall call)
    {
        var args = call.Arguments.Fields
            .Where(f => f.Value != null)
            .Select(f => Translate(f.Value!));

        return _dialect.ArrayPhrase(string.Join(", ", args));
    }

    private string TranslateArrayFunction(PredicateCall call)
    {
        return TranslateListFunction(call);
    }

    private string TranslateJoinFunction(PredicateCall call)
    {
        var args = call.Arguments.Fields;
        if (args.Count < 2)
        {
            throw new CompilationException("Join requires array and separator arguments");
        }

        var array = Translate(args[0].Value!);
        var separator = Translate(args[1].Value!);

        return _dialect.StringAggFunction(array, separator);
    }

    private string TranslatePredicateReference(PredicateCall call)
    {
        // This is a reference to another predicate - it will be resolved during rule translation
        // For now, return a placeholder that will be replaced
        var args = call.Arguments.Fields
            .Where(f => f.Value != null)
            .Select(f =>
            {
                var value = Translate(f.Value!);
                return string.IsNullOrEmpty(f.Field) || f.Field.StartsWith("col")
                    ? value
                    : $"{_dialect.QuoteIdentifier(f.Field)} AS {value}";
            });

        return $"/* {call.PredicateName}({string.Join(", ", args)}) */";
    }

    private string TranslateList(ListLiteral list)
    {
        var elements = list.Elements.Select(Translate);

        // Check if this is a map (list of arrow expressions)
        if (list.Elements.All(e => e is BinaryOp { Operator: "->" }))
        {
            // This is a map/dictionary
            var pairs = list.Elements.Cast<BinaryOp>()
                .Select(b => $"({Translate(b.Left)}, {Translate(b.Right)})");

            if (_dialect is ClickHouseDialect)
            {
                return $"map({string.Join(", ", pairs)})";
            }

            // For T-SQL, represent as JSON
            if (_dialect is TSqlDialect)
            {
                var jsonPairs = list.Elements.Cast<BinaryOp>()
                    .Select(b =>
                    {
                        var key = b.Left is StringLiteral s ? s.Value : Translate(b.Left);
                        var value = Translate(b.Right);
                        return $"'\"{key}\": ' + {value}";
                    });
                return $"'{{' + {string.Join(" + ', ' + ", jsonPairs)} + '}}'";
            }
        }

        return _dialect.ArrayPhrase(string.Join(", ", elements));
    }

    private string TranslateRecord(Record rec)
    {
        // Records are typically translated as row constructors or JSON objects
        var fields = rec.Fields
            .Where(f => f.Value != null)
            .Select(f => $"({_dialect.QuoteString(f.Field)}, {Translate(f.Value!)})");

        if (_dialect is ClickHouseDialect)
        {
            return $"tuple({string.Join(", ", fields)})";
        }

        if (_dialect is TSqlDialect)
        {
            // Build JSON object
            var jsonFields = rec.Fields
                .Where(f => f.Value != null)
                .Select(f => $"'{f.Field}', {Translate(f.Value!)}");
            return $"JSON_OBJECT({string.Join(", ", jsonFields)})";
        }

        return $"ROW({string.Join(", ", rec.Fields.Where(f => f.Value != null).Select(f => Translate(f.Value!)))})";
    }

    private string TranslateSubscript(Subscript sub)
    {
        var target = Translate(sub.Target);
        var index = Translate(sub.Index);

        // If index is a string literal (field access), use appropriate syntax
        if (sub.Index is StringLiteral fieldName)
        {
            if (_dialect is TSqlDialect)
            {
                return $"JSON_VALUE({target}, '$.{fieldName.Value}')";
            }
            else if (_dialect is ClickHouseDialect)
            {
                return $"tupleElement({target}, {_dialect.QuoteString(fieldName.Value)})";
            }
        }

        return _dialect.SubscriptExpr(target, index);
    }

    private string TranslateIn(InExpression inExpr)
    {
        var element = Translate(inExpr.Element);
        var collection = Translate(inExpr.Collection);

        // Different dialects handle "in" differently for arrays
        if (_dialect is ClickHouseDialect)
        {
            return $"has({collection}, {element})";
        }

        if (_dialect is TSqlDialect)
        {
            return $"{element} IN (SELECT value FROM OPENJSON({collection}))";
        }

        return $"{element} IN ({collection})";
    }

    private string TranslateCast(CastExpr cast)
    {
        var value = Translate(cast.Value);
        return _dialect.CastExpr(value, cast.TargetType);
    }

    private string TranslateConditional(ConditionalExpr cond)
    {
        var condition = Translate(cond.Condition);
        var thenExpr = Translate(cond.ThenExpr);
        var elseExpr = Translate(cond.ElseExpr);

        return $"CASE WHEN {condition} THEN {thenExpr} ELSE {elseExpr} END";
    }

    private string TranslateAggregation(Aggregation agg)
    {
        var expr = Translate(agg.Expression);

        return agg.Operator switch
        {
            "Sum" or "+" => $"SUM({expr})",
            "Count" or "#" => $"COUNT({expr})",
            "Min" => $"MIN({expr})",
            "Max" => $"MAX({expr})",
            "Avg" => $"AVG({expr})",
            "List" or "Collect" => _dialect.ArrayAggFunction(expr),
            "Set" => _dialect.ArrayAggFunction(expr, distinct: true),
            _ => throw new CompilationException($"Unknown aggregation operator: {agg.Operator}")
        };
    }

    private string TranslateSqlExpr(SqlExpr sql)
    {
        var template = sql.Template;

        foreach (var field in sql.Parameters.Fields)
        {
            if (field.Value != null)
            {
                var paramValue = Translate(field.Value);
                template = template.Replace($"{{{field.Field}}}", paramValue);
            }
        }

        return template;
    }

    private string TranslateLambda(Lambda lambda)
    {
        // Lambdas are typically used with array functions
        // For ClickHouse: x -> expr
        // For others, we need to inline
        if (_dialect is ClickHouseDialect)
        {
            var oldBindings = new Dictionary<string, string>(_variableBindings);
            foreach (var param in lambda.Parameters)
            {
                _variableBindings[param] = param;
            }

            var body = Translate(lambda.Body);

            _variableBindings.Clear();
            foreach (var kvp in oldBindings)
            {
                _variableBindings[kvp.Key] = kvp.Value;
            }

            return $"{string.Join(", ", lambda.Parameters)} -> {body}";
        }

        throw new CompilationException("Lambda expressions are not supported in this dialect");
    }

    private static string ReplaceFirst(string text, string search, string replace)
    {
        var pos = text.IndexOf(search, StringComparison.Ordinal);
        if (pos < 0)
        {
            return text;
        }
        return text[..pos] + replace + text[(pos + search.Length)..];
    }
}

/// <summary>
/// Exception thrown during compilation.
/// </summary>
public class CompilationException : Exception
{
    public CompilationException(string message) : base(message) { }
}
