namespace LogicaSharp.Dialects;

/// <summary>
/// Interface for SQL dialect implementations.
/// </summary>
public interface IDialect
{
    /// <summary>
    /// Gets the name of the dialect.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets the built-in functions for this dialect.
    /// Maps function name to SQL template (with %s placeholders).
    /// </summary>
    IReadOnlyDictionary<string, string> BuiltInFunctions { get; }

    /// <summary>
    /// Gets the infix operators for this dialect.
    /// Maps operator to SQL template (with %s placeholders).
    /// </summary>
    IReadOnlyDictionary<string, string> InfixOperators { get; }

    /// <summary>
    /// Gets the type mappings for this dialect.
    /// </summary>
    IReadOnlyDictionary<string, string> TypeMappings { get; }

    /// <summary>
    /// Formats a table identifier (handles escaping/quoting).
    /// </summary>
    string QuoteIdentifier(string identifier);

    /// <summary>
    /// Formats a string literal.
    /// </summary>
    string QuoteString(string value);

    /// <summary>
    /// Gets the UNNEST phrase for array expansion.
    /// </summary>
    string UnnestPhrase(string arrayExpr, string alias);

    /// <summary>
    /// Gets the array construction phrase.
    /// </summary>
    string ArrayPhrase(string elementsExpr);

    /// <summary>
    /// Gets the GROUP BY specification method.
    /// </summary>
    string GroupBySpec { get; }

    /// <summary>
    /// Gets the null handling expression.
    /// </summary>
    string CoalesceFunction { get; }

    /// <summary>
    /// Gets the string aggregation function.
    /// </summary>
    string StringAggFunction(string expr, string separator);

    /// <summary>
    /// Gets the array aggregation function.
    /// </summary>
    string ArrayAggFunction(string expr, bool distinct = false);

    /// <summary>
    /// Generates a type cast expression.
    /// </summary>
    string CastExpr(string expr, string targetType);

    /// <summary>
    /// Gets the subscript expression for accessing record fields.
    /// </summary>
    string SubscriptExpr(string record, string subscript);

    /// <summary>
    /// Gets the substring function.
    /// </summary>
    string SubstringFunction(string str, string start, string length);

    /// <summary>
    /// Gets the modulo operator.
    /// </summary>
    string ModuloOperator { get; }

    /// <summary>
    /// Gets the power function.
    /// </summary>
    string PowerFunction(string baseExpr, string exponent);

    /// <summary>
    /// Gets the boolean literal representation.
    /// </summary>
    string BooleanLiteral(bool value);

    /// <summary>
    /// Gets the null literal representation.
    /// </summary>
    string NullLiteral { get; }

    /// <summary>
    /// Gets the current timestamp expression.
    /// </summary>
    string CurrentTimestamp { get; }

    /// <summary>
    /// Wraps a recursive CTE query.
    /// </summary>
    string RecursiveCte(string cteName, string anchorQuery, string recursiveQuery, string selectQuery);
}
