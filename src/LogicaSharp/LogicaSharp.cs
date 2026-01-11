using LogicaSharp.Ast;
using LogicaSharp.Compilation;
using LogicaSharp.Dialects;
using LogicaSharp.Parsing;

namespace LogicaSharp;

/// <summary>
/// Main entry point for the LogicaSharp library.
/// Provides static methods for parsing and compiling Logica programs.
/// </summary>
public static class Logica
{
    /// <summary>
    /// Parses a Logica source string into an AST.
    /// </summary>
    /// <param name="source">The Logica source code.</param>
    /// <returns>The parsed program.</returns>
    public static Program Parse(string source)
    {
        var lexer = new Lexer(source);
        var tokens = lexer.Tokenize();
        var parser = new Parser(tokens);
        return parser.Parse();
    }

    /// <summary>
    /// Compiles a Logica program to SQL for the specified predicate.
    /// </summary>
    /// <param name="source">The Logica source code.</param>
    /// <param name="predicateName">The predicate to compile.</param>
    /// <param name="dialect">The SQL dialect name (e.g., "mssql", "clickhouse").</param>
    /// <returns>The generated SQL query.</returns>
    public static string Compile(string source, string predicateName, string dialect = "mssql")
    {
        var compiler = new LogicaCompiler(dialect);
        return compiler.Compile(source, predicateName);
    }

    /// <summary>
    /// Compiles a Logica program to SQL for the specified predicate using a dialect instance.
    /// </summary>
    /// <param name="source">The Logica source code.</param>
    /// <param name="predicateName">The predicate to compile.</param>
    /// <param name="dialect">The SQL dialect instance.</param>
    /// <returns>The generated SQL query.</returns>
    public static string Compile(string source, string predicateName, IDialect dialect)
    {
        var compiler = new LogicaCompiler(dialect);
        return compiler.Compile(source, predicateName);
    }

    /// <summary>
    /// Compiles all predicates in a Logica program to SQL.
    /// </summary>
    /// <param name="source">The Logica source code.</param>
    /// <param name="dialect">The SQL dialect name.</param>
    /// <returns>A dictionary mapping predicate names to SQL queries.</returns>
    public static Dictionary<string, string> CompileAll(string source, string dialect = "mssql")
    {
        var compiler = new LogicaCompiler(dialect);
        return compiler.CompileAll(source);
    }

    /// <summary>
    /// Tokenizes a Logica source string.
    /// </summary>
    /// <param name="source">The Logica source code.</param>
    /// <returns>The list of tokens.</returns>
    public static IReadOnlyList<Token> Tokenize(string source)
    {
        var lexer = new Lexer(source);
        return lexer.Tokenize().ToList();
    }

    /// <summary>
    /// Creates a compiler with the specified dialect.
    /// </summary>
    /// <param name="dialect">The SQL dialect name.</param>
    /// <returns>A new compiler instance.</returns>
    public static LogicaCompiler CreateCompiler(string dialect = "mssql")
    {
        return new LogicaCompiler(dialect);
    }

    /// <summary>
    /// Creates a compiler with the specified dialect instance.
    /// </summary>
    /// <param name="dialect">The SQL dialect instance.</param>
    /// <returns>A new compiler instance.</returns>
    public static LogicaCompiler CreateCompiler(IDialect dialect)
    {
        return new LogicaCompiler(dialect);
    }

    /// <summary>
    /// Gets the available SQL dialects.
    /// </summary>
    public static IEnumerable<string> AvailableDialects => DialectRegistry.AvailableDialects;

    /// <summary>
    /// Detects the target engine from a Logica source's annotations.
    /// </summary>
    /// <param name="source">The Logica source code.</param>
    /// <returns>The engine name (e.g., "mssql", "clickhouse"), or null if not specified.</returns>
    public static string? DetectEngine(string source)
    {
        var program = Parse(source);

        foreach (var statement in program.Statements)
        {
            if (statement is Annotation { Name: "Engine" } annotation)
            {
                var firstArg = annotation.Arguments?.Fields.FirstOrDefault();
                if (firstArg?.Value is StringLiteral s)
                {
                    return s.Value;
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Compiles a Logica program, auto-detecting the dialect from @Engine annotation.
    /// </summary>
    /// <param name="source">The Logica source code.</param>
    /// <param name="predicateName">The predicate to compile.</param>
    /// <param name="defaultDialect">The default dialect if not specified in source.</param>
    /// <returns>The generated SQL query.</returns>
    public static string CompileWithAutoDialect(string source, string predicateName, string defaultDialect = "mssql")
    {
        var engine = DetectEngine(source) ?? defaultDialect;
        return Compile(source, predicateName, engine);
    }
}
