namespace LogicaSharp.Dialects;

/// <summary>
/// Registry for SQL dialects.
/// </summary>
public static class DialectRegistry
{
    private static readonly Dictionary<string, Func<IDialect>> Dialects = new(StringComparer.OrdinalIgnoreCase)
    {
        ["mssql"] = () => new TSqlDialect(),
        ["tsql"] = () => new TSqlDialect(),
        ["sqlserver"] = () => new TSqlDialect(),
        ["clickhouse"] = () => new ClickHouseDialect(),
    };

    /// <summary>
    /// Gets a dialect by name.
    /// </summary>
    /// <param name="name">The dialect name.</param>
    /// <returns>The dialect instance.</returns>
    /// <exception cref="ArgumentException">Thrown if the dialect is not found.</exception>
    public static IDialect Get(string name)
    {
        if (Dialects.TryGetValue(name, out var factory))
        {
            return factory();
        }

        throw new ArgumentException($"Unknown dialect: {name}. Available dialects: {string.Join(", ", Dialects.Keys)}");
    }

    /// <summary>
    /// Checks if a dialect is registered.
    /// </summary>
    /// <param name="name">The dialect name.</param>
    /// <returns>True if the dialect exists.</returns>
    public static bool Exists(string name) => Dialects.ContainsKey(name);

    /// <summary>
    /// Gets all registered dialect names.
    /// </summary>
    public static IEnumerable<string> AvailableDialects => Dialects.Keys;

    /// <summary>
    /// Registers a custom dialect.
    /// </summary>
    /// <param name="name">The dialect name.</param>
    /// <param name="factory">Factory function to create the dialect.</param>
    public static void Register(string name, Func<IDialect> factory)
    {
        Dialects[name] = factory;
    }
}
