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
    /// Checks if a predicate has any rules.
    /// </summary>
    public bool HasRules(string predicateName) => Rules.ContainsKey(predicateName);

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
