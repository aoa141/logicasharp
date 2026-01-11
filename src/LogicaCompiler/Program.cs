using LogicaSharp;
using LogicaSharp.Dialects;

class Program
{
    static int Main(string[] args)
    {
        if (args.Length == 0 || args.Contains("--help") || args.Contains("-h"))
        {
            PrintUsage();
            return args.Length == 0 ? 1 : 0;
        }

        string? filePath = null;
        string? predicateName = null;
        string? dialect = null;
        bool listPredicates = false;
        bool parseOnly = false;

        // Parse arguments
        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "-p":
                case "--predicate":
                    if (i + 1 < args.Length)
                    {
                        predicateName = args[++i];
                    }
                    else
                    {
                        Console.Error.WriteLine("Error: --predicate requires an argument");
                        return 1;
                    }
                    break;

                case "-d":
                case "--dialect":
                    if (i + 1 < args.Length)
                    {
                        dialect = args[++i];
                    }
                    else
                    {
                        Console.Error.WriteLine("Error: --dialect requires an argument");
                        return 1;
                    }
                    break;

                case "-l":
                case "--list":
                    listPredicates = true;
                    break;

                case "--parse":
                    parseOnly = true;
                    break;

                case "--dialects":
                    Console.WriteLine("Available dialects:");
                    foreach (var d in DialectRegistry.AvailableDialects)
                    {
                        Console.WriteLine($"  {d}");
                    }
                    return 0;

                default:
                    if (args[i].StartsWith("-"))
                    {
                        Console.Error.WriteLine($"Error: Unknown option '{args[i]}'");
                        return 1;
                    }
                    filePath = args[i];
                    break;
            }
        }

        if (string.IsNullOrEmpty(filePath))
        {
            Console.Error.WriteLine("Error: No input file specified");
            PrintUsage();
            return 1;
        }

        if (!File.Exists(filePath))
        {
            Console.Error.WriteLine($"Error: File not found: {filePath}");
            return 1;
        }

        try
        {
            var source = File.ReadAllText(filePath);

            // Parse only mode
            if (parseOnly)
            {
                var program = Logica.Parse(source);
                Console.WriteLine($"Successfully parsed {program.Statements.Count} statements");
                foreach (var stmt in program.Statements)
                {
                    Console.WriteLine($"  - {stmt.GetType().Name}: {GetStatementName(stmt)}");
                }
                return 0;
            }

            // Detect dialect from source if not specified
            if (string.IsNullOrEmpty(dialect))
            {
                dialect = Logica.DetectEngine(source) ?? "mssql";
                Console.Error.WriteLine($"# Using dialect: {dialect}");
            }

            // Validate dialect
            if (!DialectRegistry.Exists(dialect))
            {
                Console.Error.WriteLine($"Error: Unknown dialect '{dialect}'");
                Console.Error.WriteLine($"Available dialects: {string.Join(", ", DialectRegistry.AvailableDialects)}");
                return 1;
            }

            // List predicates mode
            if (listPredicates)
            {
                var program = Logica.Parse(source);
                var predicates = GetPredicateNames(program);
                Console.WriteLine("Available predicates:");
                foreach (var pred in predicates.OrderBy(p => p))
                {
                    Console.WriteLine($"  {pred}");
                }
                return 0;
            }

            // Compile specific predicate or all
            if (!string.IsNullOrEmpty(predicateName))
            {
                var sql = Logica.Compile(source, predicateName, dialect);
                Console.WriteLine(sql);
            }
            else
            {
                // Compile all predicates
                var results = Logica.CompileAll(source, dialect);
                if (results.Count == 0)
                {
                    Console.Error.WriteLine("No predicates found to compile");
                    return 1;
                }

                foreach (var (name, sql) in results.OrderBy(r => r.Key))
                {
                    Console.WriteLine($"-- Predicate: {name}");
                    Console.WriteLine(sql);
                    Console.WriteLine();
                }
            }

            return 0;
        }
        catch (LogicaSharp.Parsing.ParseException ex)
        {
            Console.Error.WriteLine($"Parse error: {ex.Message}");
            return 1;
        }
        catch (LogicaSharp.Compilation.CompilationException ex)
        {
            Console.Error.WriteLine($"Compilation error: {ex.Message}");
            return 1;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            return 1;
        }
    }

    static void PrintUsage()
    {
        Console.WriteLine(@"LogicaCompiler - Logica to SQL Compiler

Usage: LogicaCompiler <file.l> [options]

Arguments:
  <file.l>              Input Logica file

Options:
  -p, --predicate NAME  Compile only the specified predicate
  -d, --dialect NAME    Target SQL dialect (mssql, clickhouse)
                        If not specified, uses @Engine annotation or defaults to mssql
  -l, --list            List all predicates in the file
  --parse               Parse only, don't compile
  --dialects            Show available dialects
  -h, --help            Show this help message

Examples:
  LogicaCompiler program.l                      # Compile all predicates
  LogicaCompiler program.l -p MyPredicate       # Compile specific predicate
  LogicaCompiler program.l -d clickhouse        # Use ClickHouse dialect
  LogicaCompiler program.l -l                   # List predicates
");
    }

    static string GetStatementName(LogicaSharp.Ast.IStatement stmt)
    {
        return stmt switch
        {
            LogicaSharp.Ast.Annotation a => $"@{a.Name}",
            LogicaSharp.Ast.Rule r => r.Head.PredicateName,
            LogicaSharp.Ast.FunctionRule f => $"{f.Head.PredicateName}()",
            LogicaSharp.Ast.FunctorRule fr => fr.PredicateName,
            LogicaSharp.Ast.Import i => $"import {i.Path}",
            _ => stmt.ToString() ?? ""
        };
    }

    static HashSet<string> GetPredicateNames(LogicaSharp.Ast.Program program)
    {
        var names = new HashSet<string>();
        foreach (var stmt in program.Statements)
        {
            switch (stmt)
            {
                case LogicaSharp.Ast.Rule r:
                    names.Add(r.Head.PredicateName);
                    break;
                case LogicaSharp.Ast.FunctionRule f:
                    names.Add(f.Head.PredicateName);
                    break;
            }
        }
        return names;
    }
}
