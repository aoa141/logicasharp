# LogicaSharp

A .NET 10 implementation of the [Logica](https://logica.dev/) programming language compiler, targeting T-SQL and ClickHouse.

## Overview

LogicaSharp compiles Logica programs (a logic programming language for data manipulation) to SQL queries. It supports:

- **Facts and Rules** - Define data and transformations declaratively
- **Joins** - Automatic join generation from variable bindings
- **Recursive CTEs** - Transitive closure and graph traversal
- **Aggregations** - COUNT, SUM, MIN, MAX, AVG with GROUP BY
- **Negation** - NOT EXISTS subqueries
- **Multiple Dialects** - T-SQL (SQL Server) and ClickHouse

## Examples

See [Examples.md](Examples.md) for detailed examples showing Logica programs and their generated T-SQL.

## Quick Start

### Compile a Logica file to T-SQL

```bash
dotnet run --project src/LogicaCompiler -- examples/ancestor_example.l -d mssql
```

### Compile to ClickHouse

```bash
dotnet run --project src/LogicaCompiler -- examples/ancestor_example.l -d clickhouse
```

### Use as a library

```csharp
using LogicaSharp;

var source = @"
@Engine(""mssql"");
Parent(""Alice"", ""Bob"");
Parent(""Bob"", ""Carol"");

Ancestor(a, d) :- Parent(a, d);
Ancestor(a, d) :- Parent(a, c), Ancestor(c, d);
";

var sql = Logica.Compile(source, "Ancestor", "mssql");
Console.WriteLine(sql);
```

## Project Structure

```
logicasharp/
├── src/
│   ├── LogicaSharp/           # Core library
│   │   ├── Parsing/           # Lexer and Parser
│   │   ├── Ast/               # Abstract Syntax Tree
│   │   ├── Compilation/       # SQL code generation
│   │   └── Dialects/          # T-SQL, ClickHouse
│   └── LogicaCompiler/        # Command-line tool
├── tests/
│   └── LogicaSharp.Tests/     # Unit and integration tests
├── examples/                   # Example Logica programs
└── Examples.md                 # Detailed examples with generated SQL
```

## Building

```bash
dotnet build
```

## Testing

```bash
dotnet test
```

The test suite includes 111 tests covering parsing, compilation, and LocalDB integration tests that execute generated SQL against SQL Server LocalDB.

## License

Based on [Logica](https://github.com/EvgSkv/logica) by Google.
