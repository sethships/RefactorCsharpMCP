namespace RefactorCsharpMCP.Core.Utilities;

/// <summary>
/// Provides naming convention conversion utilities for refactorings.
/// </summary>
public static class NamingHelper
{
    /// <summary>
    /// Converts a name to PascalCase.
    /// </summary>
    /// <param name="name">The name to convert.</param>
    /// <returns>The PascalCase version of the name.</returns>
    public static string ToPascalCase(string name)
    {
        if (string.IsNullOrEmpty(name))
            return name;
        return char.ToUpperInvariant(name[0]) + name.Substring(1);
    }

    /// <summary>
    /// Converts a name to camelCase.
    /// </summary>
    /// <param name="name">The name to convert.</param>
    /// <returns>The camelCase version of the name.</returns>
    public static string ToCamelCase(string name)
    {
        if (string.IsNullOrEmpty(name))
            return name;
        return char.ToLowerInvariant(name[0]) + name.Substring(1);
    }
}
