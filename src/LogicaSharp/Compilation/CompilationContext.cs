using LogicaSharp.Ast;
using LogicaSharp.Dialects;

namespace LogicaSharp.Compilation;

/// <summary>
/// Context for compiling a Logica program.
/// </summary>
public class CompilationContext
{
    /// <summary>
    /// The target SQL dialect.
    /// </summary>
    public IDialect Dialect { get; }

    /// <summary>
    /// All rules indexed by predicate name.
    /// </summary>
    public Dictionary<string, List<Rule>> Rules { get; } = new();

    /// <summary>
    /// All function definitions indexed by name.
    /// </summary>
    public Dictionary<string, FunctionRule> Functions { get; } = new();

    /// <summary>
    /// Functor (template) definitions indexed by name.
    /// A functor is a predicate template that can be instantiated with different source predicates.
    /// </summary>
    public Dictionary<string, List<Rule>> Functors { get; } = new();

    /// <summary>
    /// Annotations from the program.
    /// </summary>
    public List<Annotation> Annotations { get; } = [];

    /// <summary>
    /// Table alias counter for generating unique aliases.
    /// </summary>
    private int _aliasCounter;

    /// <summary>
    /// CTE counter for generating unique CTE names.
    /// </summary>
    private int _cteCounter;

    public CompilationContext(IDialect dialect)
    {
        Dialect = dialect;
    }

    /// <summary>
    /// Generates a unique table alias.
    /// </summary>
    public string NextAlias(string? prefix = null)
    {
        return $"{prefix ?? "t"}{_aliasCounter++}";
    }

    /// <summary>
    /// Generates a unique CTE name.
    /// </summary>
    public string NextCteName(string? predicateName = null)
    {
        return $"{predicateName ?? "cte"}_{_cteCounter++}";
    }

    /// <summary>
    /// Adds a rule to the context.
    /// </summary>
    public void AddRule(Rule rule)
    {
        var name = rule.Head.PredicateName;
        if (!Rules.ContainsKey(name))
        {
            Rules[name] = [];
        }
        Rules[name].Add(rule);
    }

    /// <summary>
    /// Adds a function to the context.
    /// </summary>
    public void AddFunction(FunctionRule func)
    {
        Functions[func.Head.PredicateName] = func;
    }

    /// <summary>
    /// Registers a predicate as a functor template.
    /// </summary>
    public void RegisterFunctor(string name)
    {
        if (Rules.TryGetValue(name, out var rules))
        {
            Functors[name] = rules;
        }
    }

    /// <summary>
    /// Expands a functor rule into concrete rules by substituting predicate references.
    /// </summary>
    /// <param name="functorRule">The functor instantiation.</param>
    public void ExpandFunctor(FunctorRule functorRule)
    {
        if (!Functors.TryGetValue(functorRule.FunctorName, out var templateRules))
        {
            throw new CompilationException($"Functor '{functorRule.FunctorName}' not found. Make sure to annotate it with @Functor.");
        }

        // Build substitution maps from functor arguments
        // predicateSubstitutions: for predicate name replacements (e.g., source -> DailyActiveUsers)
        // expressionSubstitutions: for variable-to-expression replacements (e.g., filterProduct -> "App")
        var predicateSubstitutions = new Dictionary<string, string>();
        var expressionSubstitutions = new Dictionary<string, IExpression>();

        foreach (var field in functorRule.Arguments.Fields)
        {
            if (field.Value is Variable v)
            {
                // Variable argument - could be a predicate reference
                predicateSubstitutions[field.Field] = v.Name;
            }
            else if (field.Value is PredicateCall pc)
            {
                // Explicit predicate call - use predicate name
                predicateSubstitutions[field.Field] = pc.PredicateName;
            }
            else if (field.Value != null)
            {
                // Literal values (strings, numbers, etc.) - substitute as expressions
                expressionSubstitutions[field.Field] = field.Value;
            }
        }

        // Clone and substitute each template rule
        foreach (var templateRule in templateRules)
        {
            var newRule = SubstituteInRule(templateRule, functorRule.PredicateName, predicateSubstitutions, expressionSubstitutions);
            AddRule(newRule);
        }
    }

    /// <summary>
    /// Substitutes predicate references in a rule.
    /// </summary>
    private Rule SubstituteInRule(Rule rule, string newPredicateName,
        Dictionary<string, string> predicateSubs, Dictionary<string, IExpression> exprSubs)
    {
        var newHead = new PredicateCall(newPredicateName, SubstituteInRecord(rule.Head.Arguments, predicateSubs, exprSubs));
        var newBody = rule.Body != null ? SubstituteInBody(rule.Body, predicateSubs, exprSubs) : null;
        return new Rule(newHead, newBody);
    }

    private IBody SubstituteInBody(IBody body,
        Dictionary<string, string> predicateSubs, Dictionary<string, IExpression> exprSubs)
    {
        return body switch
        {
            BodyCall bc => new BodyCall(SubstituteInPredicateCall(bc.Call, predicateSubs, exprSubs)),
            Conjunction conj => new Conjunction(conj.Conjuncts.Select(c => SubstituteInBody(c, predicateSubs, exprSubs)).ToList()),
            Disjunction disj => new Disjunction(disj.Disjuncts.Select(d => SubstituteInBody(d, predicateSubs, exprSubs)).ToList()),
            Negation neg => new Negation(SubstituteInBody(neg.Body, predicateSubs, exprSubs)),
            ExpressionCondition ec => new ExpressionCondition(SubstituteInExpression(ec.Expression, predicateSubs, exprSubs)),
            _ => body
        };
    }

    private PredicateCall SubstituteInPredicateCall(PredicateCall call,
        Dictionary<string, string> predicateSubs, Dictionary<string, IExpression> exprSubs)
    {
        var newName = predicateSubs.TryGetValue(call.PredicateName, out var sub) ? sub : call.PredicateName;
        return new PredicateCall(newName, SubstituteInRecord(call.Arguments, predicateSubs, exprSubs));
    }

    private Record SubstituteInRecord(Record record,
        Dictionary<string, string> predicateSubs, Dictionary<string, IExpression> exprSubs)
    {
        var newFields = record.Fields.Select(f => SubstituteInFieldValue(f, predicateSubs, exprSubs)).ToList();
        return new Record(newFields, record.Spread != null ? SubstituteInExpression(record.Spread, predicateSubs, exprSubs) : null);
    }

    private FieldValue SubstituteInFieldValue(FieldValue field,
        Dictionary<string, string> predicateSubs, Dictionary<string, IExpression> exprSubs)
    {
        var newValue = field.Value != null ? SubstituteInExpression(field.Value, predicateSubs, exprSubs) : null;
        return new FieldValue(field.Field, newValue, field.Aggregation);
    }

    private IExpression SubstituteInExpression(IExpression expr,
        Dictionary<string, string> predicateSubs, Dictionary<string, IExpression> exprSubs)
    {
        return expr switch
        {
            // First check if this variable should be substituted with an expression (literal)
            Variable v when exprSubs.TryGetValue(v.Name, out var newExpr) => newExpr,
            // Then check if it's a predicate name substitution
            Variable v when predicateSubs.TryGetValue(v.Name, out var newName) => new Variable(newName),
            PredicateCall pc => SubstituteInPredicateCall(pc, predicateSubs, exprSubs),
            BinaryOp bo => new BinaryOp(
                SubstituteInExpression(bo.Left, predicateSubs, exprSubs),
                bo.Operator,
                SubstituteInExpression(bo.Right, predicateSubs, exprSubs)),
            UnaryOp uo => new UnaryOp(uo.Operator, SubstituteInExpression(uo.Operand, predicateSubs, exprSubs)),
            Record r => SubstituteInRecord(r, predicateSubs, exprSubs),
            _ => expr
        };
    }

    /// <summary>
    /// Checks if a predicate has any rules.
    /// </summary>
    public bool HasRules(string predicateName) => Rules.ContainsKey(predicateName);

    /// <summary>
    /// Debug method to describe the body structure of a rule.
    /// </summary>
    public string DescribeRuleBody(string predicateName)
    {
        if (!Rules.TryGetValue(predicateName, out var rules) || rules.Count == 0)
            return $"No rules found for {predicateName}";

        var rule = rules[0];
        if (rule.Body == null)
            return $"{predicateName}: body is null (fact)";

        return $"{predicateName}: body = {DescribeBody(rule.Body)}";
    }

    private string DescribeBody(IBody body)
    {
        return body switch
        {
            BodyCall bc => $"BodyCall({bc.Call.PredicateName})",
            Conjunction conj => $"Conjunction([{string.Join(", ", conj.Conjuncts.Select(c => DescribeBody(c)))}])",
            Disjunction disj => $"Disjunction([{string.Join(", ", disj.Disjuncts.Select(d => DescribeBody(d)))}])",
            Negation neg => $"Negation({DescribeBody(neg.Body)})",
            ExpressionCondition ec => $"ExpressionCondition({ec.Expression.GetType().Name})",
            _ => body.GetType().Name
        };
    }

    /// <summary>
    /// Gets all rules for a predicate.
    /// </summary>
    public IReadOnlyList<Rule> GetRules(string predicateName)
    {
        return Rules.TryGetValue(predicateName, out var rules) ? rules : [];
    }

    /// <summary>
    /// Checks if a name is a function.
    /// </summary>
    public bool IsFunction(string name) => Functions.ContainsKey(name);

    /// <summary>
    /// Gets a function by name.
    /// </summary>
    public FunctionRule? GetFunction(string name)
    {
        return Functions.TryGetValue(name, out var func) ? func : null;
    }

    /// <summary>
    /// Gets the engine annotation value.
    /// </summary>
    public string? GetEngine()
    {
        var engineAnnotation = Annotations.FirstOrDefault(a =>
            a.Name.Equals("Engine", StringComparison.OrdinalIgnoreCase));

        if (engineAnnotation?.Arguments?.Fields.Count > 0)
        {
            var firstArg = engineAnnotation.Arguments.Fields[0].Value;
            if (firstArg is StringLiteral s)
            {
                return s.Value;
            }
        }

        return null;
    }

    /// <summary>
    /// Checks if a predicate is recursive.
    /// </summary>
    public bool IsRecursive(string predicateName)
    {
        if (!Rules.TryGetValue(predicateName, out var rules))
        {
            return false;
        }

        // Check if any rule references itself
        var visited = new HashSet<string>();
        return CheckRecursive(predicateName, predicateName, visited);
    }

    private bool CheckRecursive(string target, string current, HashSet<string> visited)
    {
        if (visited.Contains(current))
        {
            return current == target;
        }

        visited.Add(current);

        if (!Rules.TryGetValue(current, out var rules))
        {
            return false;
        }

        foreach (var rule in rules)
        {
            if (rule.Body == null) continue;

            var referencedPredicates = GetReferencedPredicates(rule.Body);
            foreach (var pred in referencedPredicates)
            {
                if (pred == target)
                {
                    return true;
                }

                if (CheckRecursive(target, pred, visited))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static IEnumerable<string> GetReferencedPredicates(IBody body)
    {
        return body switch
        {
            BodyCall call => [call.Call.PredicateName],
            Conjunction conj => conj.Conjuncts.SelectMany(GetReferencedPredicates),
            Disjunction disj => disj.Disjuncts.SelectMany(GetReferencedPredicates),
            Negation neg => GetReferencedPredicates(neg.Body),
            _ => []
        };
    }
}
