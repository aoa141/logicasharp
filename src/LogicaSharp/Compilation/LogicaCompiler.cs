using LogicaSharp.Ast;
using LogicaSharp.Dialects;
using LogicaSharp.Parsing;

namespace LogicaSharp.Compilation;

/// <summary>
/// Main compiler for the Logica programming language.
/// </summary>
public class LogicaCompiler
{
    private readonly IDialect _dialect;

    /// <summary>
    /// Creates a new compiler with the specified dialect.
    /// </summary>
    /// <param name="dialect">The SQL dialect to target.</param>
    public LogicaCompiler(IDialect dialect)
    {
        _dialect = dialect;
    }

    /// <summary>
    /// Creates a new compiler with the specified dialect name.
    /// </summary>
    /// <param name="dialectName">The name of the SQL dialect (e.g., "mssql", "clickhouse").</param>
    public LogicaCompiler(string dialectName)
        : this(DialectRegistry.Get(dialectName))
    {
    }

    /// <summary>
    /// Compiles a Logica program to SQL for the specified predicate.
    /// </summary>
    /// <param name="source">The Logica source code.</param>
    /// <param name="predicateName">The predicate to compile.</param>
    /// <returns>The generated SQL query.</returns>
    public string Compile(string source, string predicateName)
    {
        // Parse the source
        var program = Parse(source);

        // Create compilation context
        var context = CreateContext(program);

        // Compile the predicate
        var translator = new RuleTranslator(context);
        return translator.CompilePredicate(predicateName);
    }

    /// <summary>
    /// Compiles all predicates in a Logica program to SQL.
    /// </summary>
    /// <param name="source">The Logica source code.</param>
    /// <returns>A dictionary mapping predicate names to SQL queries.</returns>
    public Dictionary<string, string> CompileAll(string source)
    {
        var program = Parse(source);
        var context = CreateContext(program);
        var translator = new RuleTranslator(context);

        var result = new Dictionary<string, string>();
        foreach (var predicateName in context.Rules.Keys)
        {
            try
            {
                result[predicateName] = translator.CompilePredicate(predicateName);
            }
            catch (CompilationException)
            {
                // Skip predicates that can't be compiled (e.g., helper predicates)
            }
        }

        return result;
    }

    /// <summary>
    /// Parses a Logica source file.
    /// </summary>
    /// <param name="source">The Logica source code.</param>
    /// <returns>The parsed program.</returns>
    public Program Parse(string source)
    {
        var lexer = new Lexer(source);
        var tokens = lexer.Tokenize();
        var parser = new Parser(tokens);
        return parser.Parse();
    }

    /// <summary>
    /// Gets the detected engine from a program's annotations.
    /// </summary>
    /// <param name="source">The Logica source code.</param>
    /// <returns>The engine name, or null if not specified.</returns>
    public string? GetEngine(string source)
    {
        var program = Parse(source);
        var context = CreateContext(program);
        return context.GetEngine();
    }

    /// <summary>
    /// Debug method to describe the structure of a rule's body.
    /// </summary>
    public string DescribeRuleBody(string source, string predicateName)
    {
        var program = Parse(source);
        var context = CreateContext(program);
        return context.DescribeRuleBody(predicateName);
    }

    /// <summary>
    /// Creates a compilation context from a program.
    /// </summary>
    private CompilationContext CreateContext(Program program)
    {
        var context = new CompilationContext(_dialect);
        var functorRules = new List<FunctorRule>();

        // First pass: collect all rules, annotations, and functor rules
        foreach (var statement in program.Statements)
        {
            switch (statement)
            {
                case Annotation annotation:
                    context.Annotations.Add(annotation);
                    break;

                case Rule rule:
                    context.AddRule(rule);
                    break;

                case FunctionRule func:
                    context.AddFunction(func);
                    break;

                case FunctorRule functorRule:
                    functorRules.Add(functorRule);
                    break;
            }
        }

        // Second pass: register functors based on @Functor annotations
        foreach (var annotation in context.Annotations)
        {
            if (annotation.Name.Equals("Functor", StringComparison.OrdinalIgnoreCase))
            {
                // @Functor(PredicateName) marks a predicate as a functor template
                if (annotation.Arguments?.Fields.Count > 0)
                {
                    var firstArg = annotation.Arguments.Fields[0].Value;
                    string? functorName = firstArg switch
                    {
                        StringLiteral sl => sl.Value,
                        Variable v => v.Name,
                        _ => null
                    };

                    if (functorName != null)
                    {
                        context.RegisterFunctor(functorName);
                    }
                }
            }
        }

        // Third pass: expand functor instantiations
        foreach (var functorRule in functorRules)
        {
            context.ExpandFunctor(functorRule);
        }

        return context;
    }
}

/// <summary>
/// Result of compiling a Logica program.
/// </summary>
public record CompilationResult(
    string Sql,
    string PredicateName,
    string Dialect,
    IReadOnlyList<string> Warnings);
