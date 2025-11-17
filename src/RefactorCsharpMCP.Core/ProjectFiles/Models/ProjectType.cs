namespace RefactorCsharpMCP.Core.ProjectFiles.Models;

/// <summary>
/// Represents the type of a .NET project file.
/// </summary>
public enum ProjectType
{
    /// <summary>
    /// Unknown or unrecognized project type.
    /// </summary>
    Unknown,

    /// <summary>
    /// SDK-style project (modern .NET Core/.NET 5+).
    /// Identified by presence of Sdk attribute on Project element.
    /// Example: &lt;Project Sdk="Microsoft.NET.Sdk"&gt;
    /// </summary>
    SdkStyle,

    /// <summary>
    /// Legacy .NET Framework project file format.
    /// Uses verbose XML with explicit file includes.
    /// Typically has xmlns="http://schemas.microsoft.com/developer/msbuild/2003"
    /// </summary>
    Legacy,

    /// <summary>
    /// ASP.NET Web Application (legacy format).
    /// Contains WebProjectProperties or specific project type GUIDs.
    /// </summary>
    AspNetWebApp
}
