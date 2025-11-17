using System.Xml.Linq;

namespace RefactorCsharpMCP.Core.ProjectFiles.Models;

/// <summary>
/// Represents the context and metadata of a project file.
/// Contains information extracted from parsing a .csproj file.
/// </summary>
public class ProjectFileContext
{
    /// <summary>
    /// The absolute path to the project file.
    /// </summary>
    public required string FilePath { get; init; }

    /// <summary>
    /// The project type (SDK-style, Legacy, ASP.NET Web App).
    /// </summary>
    public ProjectType ProjectType { get; init; }

    /// <summary>
    /// The target framework(s) for this project.
    /// For multi-targeting projects, contains all target frameworks.
    /// Examples: ["net8.0"], ["net8.0", "net48"]
    /// </summary>
    public List<string> TargetFrameworks { get; init; } = new();

    /// <summary>
    /// The SDK identifier for SDK-style projects (e.g., "Microsoft.NET.Sdk").
    /// Null for legacy projects.
    /// </summary>
    public string? Sdk { get; init; }

    /// <summary>
    /// The output type (Exe, WinExe, Library).
    /// </summary>
    public string? OutputType { get; init; }

    /// <summary>
    /// The assembly name.
    /// </summary>
    public string? AssemblyName { get; init; }

    /// <summary>
    /// The root namespace.
    /// </summary>
    public string? RootNamespace { get; init; }

    /// <summary>
    /// All package references in this project.
    /// </summary>
    public List<PackageReference> PackageReferences { get; init; } = new();

    /// <summary>
    /// The parsed XML document for this project file.
    /// Used for manipulation and transformation.
    /// </summary>
    public XDocument? Document { get; init; }

    /// <summary>
    /// Whether this project uses Central Package Management.
    /// Determined by presence of &lt;ManagePackageVersionsCentrally&gt;true&lt;/ManagePackageVersionsCentrally&gt;
    /// </summary>
    public bool UsesCentralPackageManagement { get; init; }

    /// <summary>
    /// The directory containing this project file.
    /// </summary>
    public string DirectoryPath => Path.GetDirectoryName(FilePath) ?? string.Empty;

    /// <summary>
    /// The project file name (including .csproj extension).
    /// </summary>
    public string FileName => Path.GetFileName(FilePath);

    /// <summary>
    /// The project name (without .csproj extension).
    /// </summary>
    public string ProjectName => Path.GetFileNameWithoutExtension(FilePath);

    /// <summary>
    /// Whether this is a multi-targeting project (targets multiple frameworks).
    /// </summary>
    public bool IsMultiTargeting => TargetFrameworks.Count > 1;

    /// <summary>
    /// Returns a string representation of this project context.
    /// </summary>
    public override string ToString()
    {
        var frameworks = string.Join(", ", TargetFrameworks);
        return $"{ProjectName} ({ProjectType}, {frameworks})";
    }
}
