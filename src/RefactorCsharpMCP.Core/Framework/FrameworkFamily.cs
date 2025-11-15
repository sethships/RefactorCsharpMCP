namespace RefactorCsharpMCP.Core.Framework;

/// <summary>
/// Categorizes .NET frameworks for reference selection and behavior.
/// </summary>
public enum FrameworkFamily
{
    /// <summary>
    /// Unrecognized or uninitialized framework family.
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// Modern .NET (net8.0, net9.0).
    /// </summary>
    Modern = 1,

    /// <summary>
    /// .NET Framework (net462, net48, net481, net35).
    /// </summary>
    Framework = 2,

    /// <summary>
    /// .NET Standard (netstandard2.0, netstandard2.1).
    /// </summary>
    Standard = 3
}
