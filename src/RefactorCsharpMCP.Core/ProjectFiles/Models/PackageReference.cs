namespace RefactorCsharpMCP.Core.ProjectFiles.Models;

/// <summary>
/// Represents a NuGet package reference in a project file.
/// </summary>
public class PackageReference
{
    /// <summary>
    /// The NuGet package identifier (e.g., "Newtonsoft.Json").
    /// </summary>
    public required string PackageId { get; init; }

    /// <summary>
    /// The package version (e.g., "13.0.3").
    /// Null for Central Package Management where version is defined in Directory.Packages.props.
    /// </summary>
    public string? Version { get; init; }

    /// <summary>
    /// Condition attribute for conditional package references.
    /// Example: "'$(TargetFramework)' == 'net48'"
    /// </summary>
    public string? Condition { get; init; }

    /// <summary>
    /// PrivateAssets metadata (e.g., "all", "runtime", "compile").
    /// </summary>
    public string? PrivateAssets { get; init; }

    /// <summary>
    /// IncludeAssets metadata.
    /// </summary>
    public string? IncludeAssets { get; init; }

    /// <summary>
    /// ExcludeAssets metadata.
    /// </summary>
    public string? ExcludeAssets { get; init; }

    /// <summary>
    /// The project file path where this package reference is defined.
    /// </summary>
    public string? ProjectPath { get; init; }

    /// <summary>
    /// Creates a simple package reference with just package ID and version.
    /// </summary>
    public static PackageReference Create(string packageId, string version)
    {
        return new PackageReference
        {
            PackageId = packageId,
            Version = version
        };
    }

    /// <summary>
    /// Returns a string representation of this package reference.
    /// </summary>
    public override string ToString()
    {
        var version = !string.IsNullOrEmpty(Version) ? $" {Version}" : "";
        var condition = !string.IsNullOrEmpty(Condition) ? $" (Condition: {Condition})" : "";
        return $"{PackageId}{version}{condition}";
    }
}
