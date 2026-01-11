using System.Text;
using LogicaSharp.Ast;
using LogicaSharp.Dialects;

namespace LogicaSharp.Compilation;

/// <summary>
/// Translates Logica rules to SQL queries.
/// </summary>
public class RuleTranslator
{
    private readonly CompilationContext _context;
    private readonly ExpressionTranslator _exprTranslator;
    private readonly HashSet<string> _compilingPredicates = new();

    public RuleTranslator(CompilationContext context)
    {
        _context = context;
        _exprTranslator = new ExpressionTranslator(context.Dialect, context);
    }

    /// <summary>
    /// Compiles a predicate to SQL.
    /// </summary>
    /// <param name="predicateName">The predicate to compile.</param>
    /// <returns>The generated SQL query.</returns>
    public string CompilePredicate(string predicateName)
    {
        var rules = _context.GetRules(predicateName);
        if (rules.Count == 0)
        {
            throw new CompilationException($"Predicate '{predicateName}' not found");
        }

        // Prevent infinite recursion - if we're already compiling this predicate,
        // we're in a recursive reference and should not try to recompile
        if (_compilingPredicates.Contains(predicateName))
        {
            // Return a reference to the CTE that will be created
            return $"SELECT * FROM {_context.Dialect.QuoteIdentifier(predicateName)}";
        }

        _compilingPredicates.Add(predicateName);
        try
        {
            // Check if recursive
            if (_context.IsRecursive(predicateName))
            {
                return CompileRecursivePredicate(predicateName, rules);
            }

            // Compile each rule and UNION ALL them
            var queries = rules.Select(r => CompileRule(r, predicateName)).ToList();

            if (queries.Count == 1)
            {
                return queries[0];
            }

            return string.Join("\nUNION ALL\n", queries.Select(q => $"({q})"));
        }
        finally
        {
            _compilingPredicates.Remove(predicateName);
        }
    }

    /// <summary>
    /// Compiles a single rule to SQL.
    /// </summary>
    private string CompileRule(Rule rule, string predicateName)
    {
        var sb = new StringBuilder();

        // Build FROM/WHERE clauses from body FIRST to establish variable bindings
        var (fromClause, whereClause, _) = BuildFromWhereClause(rule.Body, predicateName);

        // Build SELECT clause from head (now with variable bindings available)
        var selectClause = BuildSelectClause(rule.Head);

        // Build GROUP BY clause if there are aggregations
        var groupByClause = BuildGroupByClause(rule.Head);

        sb.Append("SELECT ");
        sb.AppendLine(selectClause);

        if (!string.IsNullOrEmpty(fromClause))
        {
            sb.Append("FROM ");
            sb.AppendLine(fromClause);
        }

        if (!string.IsNullOrEmpty(whereClause))
        {
            sb.Append("WHERE ");
            sb.AppendLine(whereClause);
        }

        if (!string.IsNullOrEmpty(groupByClause))
        {
            sb.Append("GROUP BY ");
            sb.AppendLine(groupByClause);
        }

        return sb.ToString().Trim();
    }

    /// <summary>
    /// Builds the GROUP BY clause based on non-aggregated fields in the head.
    /// </summary>
    private string BuildGroupByClause(PredicateCall head)
    {
        // Check if there are any aggregations
        bool hasAggregations = head.Arguments.Fields.Any(f =>
            f.Aggregation.HasValue && f.Aggregation.Value != AggregationType.None);

        if (!hasAggregations)
        {
            return "";
        }

        // Collect non-aggregated field expressions for GROUP BY
        var groupByExprs = new List<string>();

        foreach (var field in head.Arguments.Fields)
        {
            // Skip aggregated fields
            if (field.Aggregation.HasValue && field.Aggregation.Value != AggregationType.None)
            {
                continue;
            }

            // For non-aggregated fields, get the expression
            if (field.Value != null)
            {
                var expr = _exprTranslator.Translate(field.Value);
                groupByExprs.Add(expr);
            }
        }

        return string.Join(", ", groupByExprs);
    }

    /// <summary>
    /// Compiles a recursive predicate using CTEs.
    /// </summary>
    private string CompileRecursivePredicate(string predicateName, IReadOnlyList<Rule> rules)
    {
        // Separate base cases (non-recursive) from recursive cases
        var baseCases = new List<Rule>();
        var recursiveCases = new List<Rule>();

        foreach (var rule in rules)
        {
            if (ReferencesPredicateInBody(rule.Body, predicateName))
            {
                recursiveCases.Add(rule);
            }
            else
            {
                baseCases.Add(rule);
            }
        }

        if (baseCases.Count == 0)
        {
            throw new CompilationException($"Recursive predicate '{predicateName}' has no base case");
        }

        // Build anchor query (base cases)
        var anchorQueries = baseCases.Select(r => CompileRule(r, predicateName));
        var anchorQuery = string.Join("\nUNION ALL\n", anchorQueries);

        // Build recursive query
        var recursiveQueries = recursiveCases.Select(r => CompileRule(r, predicateName));
        var recursiveQuery = string.Join("\nUNION ALL\n", recursiveQueries);

        // Build final SELECT
        var outputColumns = GetOutputColumns(rules[0].Head);
        var selectQuery = $"SELECT {string.Join(", ", outputColumns)} FROM {_context.Dialect.QuoteIdentifier(predicateName)}";

        return _context.Dialect.RecursiveCte(
            _context.Dialect.QuoteIdentifier(predicateName),
            anchorQuery,
            recursiveQuery,
            selectQuery
        );
    }

    /// <summary>
    /// Builds the SELECT clause from a predicate call (rule head).
    /// </summary>
    private string BuildSelectClause(PredicateCall head)
    {
        var columns = new List<string>();
        int positionalIndex = 0;

        foreach (var field in head.Arguments.Fields)
        {
            var columnName = string.IsNullOrEmpty(field.Field) || field.Field.StartsWith("col")
                ? $"col{positionalIndex++}"
                : field.Field;

            if (field.Value == null)
            {
                // Field reference without value means use the field name as both column and alias
                columns.Add($"{_context.Dialect.QuoteIdentifier(columnName)} AS {_context.Dialect.QuoteIdentifier(columnName)}");
            }
            else
            {
                var expr = _exprTranslator.Translate(field.Value);

                // Handle aggregations
                if (field.Aggregation.HasValue && field.Aggregation.Value != AggregationType.None)
                {
                    expr = ApplyAggregation(expr, field.Aggregation.Value);
                }

                columns.Add($"{expr} AS {_context.Dialect.QuoteIdentifier(columnName)}");
            }
        }

        return string.Join(", ", columns);
    }

    /// <summary>
    /// Applies an aggregation function to an expression.
    /// </summary>
    private string ApplyAggregation(string expr, AggregationType aggregation)
    {
        return aggregation switch
        {
            AggregationType.Sum => $"SUM({expr})",
            AggregationType.Count => $"COUNT({expr})",
            AggregationType.Min => $"MIN({expr})",
            AggregationType.Max => $"MAX({expr})",
            AggregationType.Avg => $"AVG({expr})",
            AggregationType.Collect => _context.Dialect.ArrayAggFunction(expr),
            _ => expr
        };
    }

    /// <summary>
    /// Builds the FROM and WHERE clauses from a rule body.
    /// </summary>
    private (string From, string Where, string GroupBy) BuildFromWhereClause(IBody? body, string currentPredicate)
    {
        if (body == null)
        {
            return ("", "", "");
        }

        var tables = new List<string>();
        var conditions = new List<string>();
        var groupByColumns = new List<string>();
        var variableBindings = new Dictionary<string, string>();

        ProcessBody(body, tables, conditions, variableBindings, groupByColumns, currentPredicate);

        // Update expression translator with variable bindings
        foreach (var (varName, binding) in variableBindings)
        {
            _exprTranslator.BindVariable(varName, binding);
        }

        var fromClause = string.Join(", ", tables);
        var whereClause = conditions.Count > 0 ? string.Join(" AND ", conditions) : "";
        var groupByClause = groupByColumns.Count > 0 ? string.Join(", ", groupByColumns) : "";

        return (fromClause, whereClause, groupByClause);
    }

    /// <summary>
    /// Processes a body element to extract tables and conditions.
    /// </summary>
    private void ProcessBody(
        IBody body,
        List<string> tables,
        List<string> conditions,
        Dictionary<string, string> variableBindings,
        List<string> groupByColumns,
        string currentPredicate)
    {
        switch (body)
        {
            case Conjunction conj:
                foreach (var conjunct in conj.Conjuncts)
                {
                    ProcessBody(conjunct, tables, conditions, variableBindings, groupByColumns, currentPredicate);
                }
                break;

            case Disjunction disj:
                // Convert disjunction to OR conditions
                var orConditions = new List<string>();
                foreach (var disjunct in disj.Disjuncts)
                {
                    var subTables = new List<string>();
                    var subConditions = new List<string>();
                    ProcessBody(disjunct, subTables, subConditions, variableBindings, groupByColumns, currentPredicate);

                    // For disjunctions, we need to handle tables differently
                    // This is a simplification - full implementation would use UNION
                    if (subConditions.Count > 0)
                    {
                        orConditions.Add($"({string.Join(" AND ", subConditions)})");
                    }
                }
                if (orConditions.Count > 0)
                {
                    conditions.Add($"({string.Join(" OR ", orConditions)})");
                }
                break;

            case Negation neg:
                var negTables = new List<string>();
                var negConditions = new List<string>();
                ProcessBody(neg.Body, negTables, negConditions, variableBindings, groupByColumns, currentPredicate);

                if (neg.Body is BodyCall negCall)
                {
                    // Convert to NOT EXISTS
                    var subQuery = BuildNotExistsSubquery(negCall.Call, variableBindings);
                    conditions.Add($"NOT EXISTS ({subQuery})");
                }
                else if (negConditions.Count > 0)
                {
                    conditions.Add($"NOT ({string.Join(" AND ", negConditions)})");
                }
                break;

            case BodyCall call:
                ProcessPredicateCall(call.Call, tables, conditions, variableBindings, groupByColumns, currentPredicate);
                break;

            case ExpressionCondition exprCond:
                var condExpr = _exprTranslator.Translate(exprCond.Expression);
                conditions.Add(condExpr);
                break;
        }
    }

    /// <summary>
    /// Processes a predicate call in the body.
    /// </summary>
    private void ProcessPredicateCall(
        PredicateCall call,
        List<string> tables,
        List<string> conditions,
        Dictionary<string, string> variableBindings,
        List<string> groupByColumns,
        string currentPredicate)
    {
        var predicateName = call.PredicateName;

        // Check if this is a backtick-quoted external table reference
        bool isExternalTable = predicateName.StartsWith('`') && predicateName.EndsWith('`');
        string tableName = predicateName;
        string aliasBase;

        if (isExternalTable)
        {
            // Remove backticks and get the raw table name
            tableName = predicateName[1..^1];
            // Create alias by replacing dots and special chars with underscores
            aliasBase = tableName.Replace(".", "_").Replace("-", "_");
        }
        else
        {
            aliasBase = predicateName.ToLowerInvariant();
        }

        var alias = _context.NextAlias(aliasBase);

        // Get the output column names for defined predicates
        List<string>? predicateColumnNames = null;

        // Check if this is a reference to a defined predicate or a table
        if (!isExternalTable && _context.HasRules(predicateName))
        {
            // It's a defined predicate - need to inline or use subquery
            var subQuery = CompilePredicate(predicateName);
            tables.Add($"({subQuery}) AS {_context.Dialect.QuoteIdentifier(alias)}");

            // Get the output column names from the predicate definition
            var rules = _context.GetRules(predicateName);
            if (rules.Count > 0)
            {
                predicateColumnNames = GetOutputColumnNames(rules[0].Head);
            }
        }
        else
        {
            // It's a table reference (external or undefined predicate treated as table)
            var quotedTable = _context.Dialect.QuoteIdentifier(tableName);
            tables.Add($"{quotedTable} AS {alias}");
        }

        // Process field bindings
        for (int i = 0; i < call.Arguments.Fields.Count; i++)
        {
            var field = call.Arguments.Fields[i];

            // Determine the actual column name to use
            // If this is a defined predicate, map positional args to the predicate's column names
            string actualColumnName = field.Field;
            if (predicateColumnNames != null && i < predicateColumnNames.Count && field.Field.StartsWith("col"))
            {
                actualColumnName = predicateColumnNames[i];
            }

            var columnRef = $"{alias}.{_context.Dialect.QuoteIdentifier(actualColumnName)}";

            if (field.Value is Variable v)
            {
                // Bind the variable to this column
                if (variableBindings.TryGetValue(v.Name, out var existingBinding))
                {
                    // Variable already bound - add equality condition
                    conditions.Add($"{columnRef} = {existingBinding}");
                }
                else
                {
                    variableBindings[v.Name] = columnRef;
                    // Immediately bind to expression translator so subsequent expressions can use it
                    _exprTranslator.BindVariable(v.Name, columnRef);
                }
            }
            else if (field.Value != null)
            {
                // Literal value - add equality condition
                var valueExpr = _exprTranslator.Translate(field.Value);
                conditions.Add($"{columnRef} = {valueExpr}");
            }
        }
    }

    /// <summary>
    /// Gets the output column names from a predicate call (used for field mapping).
    /// </summary>
    private List<string> GetOutputColumnNames(PredicateCall head)
    {
        var columns = new List<string>();
        int positionalIndex = 0;

        foreach (var field in head.Arguments.Fields)
        {
            var columnName = string.IsNullOrEmpty(field.Field) || field.Field.StartsWith("col")
                ? $"col{positionalIndex++}"
                : field.Field;

            columns.Add(columnName);
        }

        return columns;
    }

    /// <summary>
    /// Builds a NOT EXISTS subquery for negation.
    /// </summary>
    private string BuildNotExistsSubquery(PredicateCall call, Dictionary<string, string> outerBindings)
    {
        var sb = new StringBuilder();
        sb.Append("SELECT 1 FROM ");

        var predicateName = call.PredicateName;
        bool isExternalTable = predicateName.StartsWith('`') && predicateName.EndsWith('`');

        if (!isExternalTable && _context.HasRules(predicateName))
        {
            var subQuery = CompilePredicate(predicateName);
            sb.Append($"({subQuery}) AS neg_sub");
        }
        else
        {
            var tableName = isExternalTable ? predicateName[1..^1] : predicateName;
            sb.Append($"{_context.Dialect.QuoteIdentifier(tableName)} AS neg_sub");
        }

        var conditions = new List<string>();
        foreach (var field in call.Arguments.Fields)
        {
            if (field.Value is Variable v && outerBindings.TryGetValue(v.Name, out var outerRef))
            {
                conditions.Add($"neg_sub.{_context.Dialect.QuoteIdentifier(field.Field)} = {outerRef}");
            }
            else if (field.Value != null)
            {
                var valueExpr = _exprTranslator.Translate(field.Value);
                conditions.Add($"neg_sub.{_context.Dialect.QuoteIdentifier(field.Field)} = {valueExpr}");
            }
        }

        if (conditions.Count > 0)
        {
            sb.Append(" WHERE ");
            sb.Append(string.Join(" AND ", conditions));
        }

        return sb.ToString();
    }

    /// <summary>
    /// Gets the output column names from a predicate call.
    /// </summary>
    private List<string> GetOutputColumns(PredicateCall head)
    {
        var columns = new List<string>();
        int positionalIndex = 0;

        foreach (var field in head.Arguments.Fields)
        {
            var columnName = string.IsNullOrEmpty(field.Field) || field.Field.StartsWith("col")
                ? $"col{positionalIndex++}"
                : field.Field;

            columns.Add(_context.Dialect.QuoteIdentifier(columnName));
        }

        return columns;
    }

    /// <summary>
    /// Checks if a body references a specific predicate.
    /// </summary>
    private static bool ReferencesPredicateInBody(IBody? body, string predicateName)
    {
        if (body == null) return false;

        return body switch
        {
            BodyCall call => call.Call.PredicateName == predicateName,
            Conjunction conj => conj.Conjuncts.Any(c => ReferencesPredicateInBody(c, predicateName)),
            Disjunction disj => disj.Disjuncts.Any(d => ReferencesPredicateInBody(d, predicateName)),
            Negation neg => ReferencesPredicateInBody(neg.Body, predicateName),
            _ => false
        };
    }
}
