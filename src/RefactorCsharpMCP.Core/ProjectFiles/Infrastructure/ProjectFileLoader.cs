using System.Security;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using RefactorCsharpMCP.Core.ProjectFiles.Models;

namespace RefactorCsharpMCP.Core.ProjectFiles.Infrastructure;

/// <summary>
/// Loads and saves .NET project files (.csproj, .props, .targets) with format preservation.
/// Handles both SDK-style and legacy project file formats.
/// </summary>
public class ProjectFileLoader
{
    private readonly ILogger<ProjectFileLoader> _logger;

    public ProjectFileLoader(ILogger<ProjectFileLoader>? logger = null)
    {
        _logger = logger ?? NullLogger<ProjectFileLoader>.Instance;
    }

    /// <summary>
    /// Loads a project file and returns the XML document.
    /// </summary>
    /// <param name="path">Path to the project file.</param>
    /// <param name="preserveFormatting">Whether to preserve whitespace and formatting.</param>
    /// <returns>The loaded XML document.</returns>
    /// <exception cref="FileNotFoundException">If the file doesn't exist.</exception>
    /// <exception cref="XmlException">If the file contains invalid XML.</exception>
    /// <exception cref="SecurityException">If the path is invalid or attempts path traversal.</exception>
    public XDocument LoadProject(string path, bool preserveFormatting = true)
    {
        // Validate and normalize the path to prevent path traversal attacks
        var validatedPath = PathValidator.ValidateAndNormalizePath(path);

        if (!File.Exists(validatedPath))
        {
            throw new FileNotFoundException(
                $"Project file not found: {path}. " +
                "Ensure the file exists, is a valid project file, and you have read permissions.",
                validatedPath);
        }

        try
        {
            var loadOptions = preserveFormatting
                ? LoadOptions.PreserveWhitespace
                : LoadOptions.None;

            var document = XDocument.Load(validatedPath, loadOptions);

            _logger.LogDebug("Loaded project file: {Path}", validatedPath);

            return document;
        }
        catch (XmlException ex)
        {
            _logger.LogError(ex, "Invalid XML in project file: {Path}", path);
            throw new XmlException($"Invalid XML in project file: {path}", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load project file: {Path}", path);
            throw new IOException($"Failed to load project file: {path}", ex);
        }
    }

    /// <summary>
    /// Saves a project file with optional formatting preservation.
    /// </summary>
    /// <param name="document">The XML document to save.</param>
    /// <param name="path">Path where the file should be saved.</param>
    /// <param name="preserveFormatting">Whether to preserve original formatting.</param>
    /// <exception cref="SecurityException">If the path is invalid or attempts path traversal.</exception>
    public void SaveProject(XDocument document, string path, bool preserveFormatting = true)
    {
        // Validate and normalize the path to prevent path traversal attacks
        var validatedPath = PathValidator.ValidateAndNormalizePath(path);

        try
        {
            var settings = new XmlWriterSettings
            {
                Indent = true,
                IndentChars = "  ", // 2 spaces (MSBuild standard)
                OmitXmlDeclaration = false,
                Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
                NewLineChars = Environment.NewLine
            };

            if (preserveFormatting)
            {
                // Save with minimal changes to preserve original formatting
                using var writer = XmlWriter.Create(validatedPath, settings);
                document.Save(writer);
            }
            else
            {
                // Save with standard formatting
                using var writer = XmlWriter.Create(validatedPath, settings);
                document.Save(writer);
            }

            _logger.LogDebug("Saved project file: {Path}", validatedPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save project file: {Path}", path);
            throw new IOException(
                $"Failed to save project file: {path}. " +
                "Ensure you have write permissions and the path is valid.",
                ex);
        }
    }

    /// <summary>
    /// Detects the project type (SDK-style vs legacy).
    /// </summary>
    /// <param name="document">The project XML document.</param>
    /// <returns>The detected project type.</returns>
    public ProjectType DetectProjectType(XDocument document)
    {
        var root = document.Root;
        if (root == null || root.Name.LocalName != ProjectFileConstants.Elements.Project)
        {
            return ProjectType.Unknown;
        }

        // Check for SDK attribute (SDK-style project)
        var sdkAttribute = root.Attribute(ProjectFileConstants.Attributes.Sdk);
        if (sdkAttribute != null && !string.IsNullOrWhiteSpace(sdkAttribute.Value))
        {
            return ProjectType.SdkStyle;
        }

        // Check for ASP.NET Web App indicators
        if (IsAspNetWebApp(document))
        {
            return ProjectType.AspNetWebApp;
        }

        // Default to legacy if it has the MSBuild namespace
        var xmlns = root.Attribute("xmlns");
        if (xmlns != null && xmlns.Value == ProjectFileConstants.MsBuildNamespace.NamespaceName)
        {
            return ProjectType.Legacy;
        }

        return ProjectType.Unknown;
    }

    /// <summary>
    /// Extracts target framework(s) from the project file.
    /// </summary>
    /// <param name="document">The project XML document.</param>
    /// <returns>List of target frameworks.</returns>
    public List<string> GetTargetFrameworks(XDocument document)
    {
        var frameworks = new List<string>();
        var ns = document.Root?.Name.Namespace ?? XNamespace.None;

        // Try TargetFramework (single)
        var targetFramework = document.Descendants(ns + ProjectFileConstants.Elements.TargetFramework).FirstOrDefault();
        if (targetFramework != null && !string.IsNullOrWhiteSpace(targetFramework.Value))
        {
            frameworks.Add(targetFramework.Value.Trim());
            return frameworks;
        }

        // Try TargetFrameworks (multiple, semicolon-separated)
        var targetFrameworks = document.Descendants(ns + ProjectFileConstants.Elements.TargetFrameworks).FirstOrDefault();
        if (targetFrameworks != null && !string.IsNullOrWhiteSpace(targetFrameworks.Value))
        {
            frameworks.AddRange(
                targetFrameworks.Value.Split(';', StringSplitOptions.RemoveEmptyEntries)
                    .Select(f => f.Trim())
            );
            return frameworks;
        }

        // Try legacy TargetFrameworkVersion
        var legacyVersion = document.Descendants(ns + ProjectFileConstants.Elements.TargetFrameworkVersion).FirstOrDefault();
        if (legacyVersion != null && !string.IsNullOrWhiteSpace(legacyVersion.Value))
        {
            // Convert legacy version (e.g., "v4.8") to modern moniker (e.g., "net48")
            var modernMoniker = ConvertLegacyFrameworkToMoniker(legacyVersion.Value.Trim());
            if (modernMoniker != null)
            {
                frameworks.Add(modernMoniker);
            }
        }

        return frameworks;
    }

    /// <summary>
    /// Extracts all package references from the project file.
    /// </summary>
    /// <param name="document">The project XML document.</param>
    /// <param name="projectPath">Path to the project file (for context).</param>
    /// <returns>List of package references.</returns>
    public List<PackageReference> GetPackageReferences(XDocument document, string? projectPath = null)
    {
        var references = new List<PackageReference>();
        var ns = document.Root?.Name.Namespace ?? XNamespace.None;

        var packageRefs = document.Descendants(ns + ProjectFileConstants.Elements.PackageReference);

        foreach (var packageRef in packageRefs)
        {
            var packageId = packageRef.Attribute(ProjectFileConstants.Attributes.Include)?.Value;
            if (string.IsNullOrWhiteSpace(packageId))
            {
                continue;
            }

            var reference = new PackageReference
            {
                PackageId = packageId,
                Version = packageRef.Attribute(ProjectFileConstants.Attributes.Version)?.Value,
                Condition = packageRef.Attribute(ProjectFileConstants.Attributes.Condition)?.Value,
                PrivateAssets = packageRef.Element(ns + "PrivateAssets")?.Value
                    ?? packageRef.Attribute("PrivateAssets")?.Value,
                IncludeAssets = packageRef.Element(ns + "IncludeAssets")?.Value
                    ?? packageRef.Attribute("IncludeAssets")?.Value,
                ExcludeAssets = packageRef.Element(ns + "ExcludeAssets")?.Value
                    ?? packageRef.Attribute("ExcludeAssets")?.Value,
                ProjectPath = projectPath
            };

            references.Add(reference);
        }

        return references;
    }

    /// <summary>
    /// Loads a project file and creates a complete ProjectFileContext.
    /// </summary>
    /// <param name="filePath">Path to the project file.</param>
    /// <param name="preserveFormatting">Whether to preserve whitespace and formatting.</param>
    /// <returns>The project file context with all metadata.</returns>
    /// <exception cref="SecurityException">If the path is invalid or attempts path traversal.</exception>
    public ProjectFileContext LoadProjectContext(string filePath, bool preserveFormatting = true)
    {
        // Validate path before loading (LoadProject will also validate, but we need the validated path here)
        var validatedPath = PathValidator.ValidateAndNormalizePath(filePath);

        var document = LoadProject(validatedPath, preserveFormatting);
        var projectType = DetectProjectType(document);
        var targetFrameworks = GetTargetFrameworks(document);
        var packageReferences = GetPackageReferences(document, validatedPath);

        var ns = document.Root?.Name.Namespace ?? XNamespace.None;

        var context = new ProjectFileContext
        {
            FilePath = validatedPath,
            ProjectType = projectType,
            TargetFrameworks = targetFrameworks,
            Sdk = document.Root?.Attribute(ProjectFileConstants.Attributes.Sdk)?.Value,
            OutputType = document.Descendants(ns + ProjectFileConstants.Elements.OutputType).FirstOrDefault()?.Value,
            AssemblyName = document.Descendants(ns + ProjectFileConstants.Elements.AssemblyName).FirstOrDefault()?.Value,
            RootNamespace = document.Descendants(ns + ProjectFileConstants.Elements.RootNamespace).FirstOrDefault()?.Value,
            PackageReferences = packageReferences,
            Document = document,
            UsesCentralPackageManagement = IsCentralPackageManagementEnabled(document)
        };

        _logger.LogInformation("Loaded project context: {Context}", context);

        return context;
    }

    /// <summary>
    /// Checks if Central Package Management is enabled in the project.
    /// </summary>
    private static bool IsCentralPackageManagementEnabled(XDocument document)
    {
        var ns = document.Root?.Name.Namespace ?? XNamespace.None;
        var cpmElement = document.Descendants(ns + ProjectFileConstants.Elements.ManagePackageVersionsCentrally)
            .FirstOrDefault();

        return cpmElement != null
            && bool.TryParse(cpmElement.Value, out var enabled)
            && enabled;
    }

    /// <summary>
    /// Checks if the project is an ASP.NET Web Application.
    /// </summary>
    private static bool IsAspNetWebApp(XDocument document)
    {
        var ns = document.Root?.Name.Namespace ?? XNamespace.None;

        // Check for WebProjectProperties element
        var webProps = document.Descendants(ns + ProjectFileConstants.Elements.WebProjectProperties).Any();
        if (webProps)
        {
            return true;
        }

        // Check for project type GUIDs
        var projectTypeGuids = document.Descendants(ns + ProjectFileConstants.Elements.ProjectTypeGuids)
            .FirstOrDefault()?.Value;

        if (!string.IsNullOrWhiteSpace(projectTypeGuids))
        {
            // Use case-insensitive comparison for GUIDs
            return projectTypeGuids.Contains(ProjectFileConstants.ProjectTypeGuids.AspNetMvc1, StringComparison.OrdinalIgnoreCase)
                || projectTypeGuids.Contains(ProjectFileConstants.ProjectTypeGuids.AspNetMvc2, StringComparison.OrdinalIgnoreCase)
                || projectTypeGuids.Contains(ProjectFileConstants.ProjectTypeGuids.AspNetMvc3, StringComparison.OrdinalIgnoreCase)
                || projectTypeGuids.Contains(ProjectFileConstants.ProjectTypeGuids.AspNetMvc4, StringComparison.OrdinalIgnoreCase)
                || projectTypeGuids.Contains(ProjectFileConstants.ProjectTypeGuids.AspNetMvc5, StringComparison.OrdinalIgnoreCase)
                || projectTypeGuids.Contains(ProjectFileConstants.ProjectTypeGuids.WebApplication, StringComparison.OrdinalIgnoreCase);
        }

        return false;
    }

    /// <summary>
    /// Converts legacy framework version (e.g., "v4.8") to modern moniker (e.g., "net48").
    /// </summary>
    private static string? ConvertLegacyFrameworkToMoniker(string legacyVersion)
    {
        return legacyVersion switch
        {
            "v4.8" => "net48",
            "v4.7.2" => "net472",
            "v4.7.1" => "net471",
            "v4.7" => "net47",
            "v4.6.2" => "net462",
            "v4.6.1" => "net461",
            "v4.6" => "net46",
            "v4.5.2" => "net452",
            "v4.5.1" => "net451",
            "v4.5" => "net45",
            "v4.0" => "net40",
            "v3.5" => "net35",
            _ => null
        };
    }
}
