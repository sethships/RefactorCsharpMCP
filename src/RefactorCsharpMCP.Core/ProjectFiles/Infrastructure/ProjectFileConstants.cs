using System.Xml.Linq;

namespace RefactorCsharpMCP.Core.ProjectFiles.Infrastructure;

/// <summary>
/// Constants for working with MSBuild project files (.csproj, .props, .targets).
/// Provides XML namespaces, element names, and attribute names used in project file manipulation.
/// </summary>
public static class ProjectFileConstants
{
    /// <summary>
    /// MSBuild XML namespace used in legacy .NET Framework project files.
    /// Example: &lt;Project xmlns="http://schemas.microsoft.com/developer/msbuild/2003"&gt;
    /// </summary>
    public static readonly XNamespace MsBuildNamespace = "http://schemas.microsoft.com/developer/msbuild/2003";

    /// <summary>
    /// Element names commonly used in project files.
    /// </summary>
    public static class Elements
    {
        public const string Project = "Project";
        public const string PropertyGroup = "PropertyGroup";
        public const string ItemGroup = "ItemGroup";
        public const string PackageReference = "PackageReference";
        public const string Reference = "Reference";
        public const string ProjectReference = "ProjectReference";
        public const string Compile = "Compile";
        public const string Content = "Content";
        public const string None = "None";
        public const string EmbeddedResource = "EmbeddedResource";

        // Property elements
        public const string TargetFramework = "TargetFramework";
        public const string TargetFrameworks = "TargetFrameworks";
        public const string OutputType = "OutputType";
        public const string RootNamespace = "RootNamespace";
        public const string AssemblyName = "AssemblyName";
        public const string LangVersion = "LangVersion";
        public const string Nullable = "Nullable";
        public const string ImplicitUsings = "ImplicitUsings";

        // Central Package Management
        public const string ManagePackageVersionsCentrally = "ManagePackageVersionsCentrally";
        public const string PackageVersion = "PackageVersion";

        // Legacy elements
        public const string TargetFrameworkVersion = "TargetFrameworkVersion";
        public const string FileAlignment = "FileAlignment";
        public const string Configuration = "Configuration";
        public const string Platform = "Platform";
        public const string ProductVersion = "ProductVersion";
        public const string SchemaVersion = "SchemaVersion";
        public const string ProjectGuid = "ProjectGuid";
        public const string ProjectTypeGuids = "ProjectTypeGuids";

        // ASP.NET specific
        public const string WebProjectProperties = "WebProjectProperties";
        public const string UseIISExpress = "UseIISExpress";
        public const string IISExpressSSLPort = "IISExpressSSLPort";
        public const string IISExpressAnonymousAuthentication = "IISExpressAnonymousAuthentication";
        public const string IISExpressWindowsAuthentication = "IISExpressWindowsAuthentication";
        public const string IISExpressUseClassicPipelineMode = "IISExpressUseClassicPipelineMode";
    }

    /// <summary>
    /// Attribute names commonly used in project files.
    /// </summary>
    public static class Attributes
    {
        public const string Sdk = "Sdk";
        public const string Include = "Include";
        public const string Version = "Version";
        public const string Condition = "Condition";
        public const string Update = "Update";
        public const string Remove = "Remove";
        public const string PrivateAssets = "PrivateAssets";
        public const string IncludeAssets = "IncludeAssets";
        public const string ExcludeAssets = "ExcludeAssets";
    }

    /// <summary>
    /// SDK identifiers for SDK-style projects.
    /// </summary>
    public static class Sdks
    {
        public const string MicrosoftNetSdk = "Microsoft.NET.Sdk";
        public const string MicrosoftNetSdkWeb = "Microsoft.NET.Sdk.Web";
        public const string MicrosoftNetSdkWorker = "Microsoft.NET.Sdk.Worker";
        public const string MicrosoftNetSdkRazor = "Microsoft.NET.Sdk.Razor";
        public const string MicrosoftNetSdkWindowsDesktop = "Microsoft.NET.Sdk.WindowsDesktop";
    }

    /// <summary>
    /// Project type GUIDs for legacy project files.
    /// Used to detect project types (Web, WinForms, WPF, etc.).
    /// </summary>
    public static class ProjectTypeGuids
    {
        // ASP.NET (various versions)
        public const string AspNetMvc1 = "{603C0E0B-DB56-11DC-BE95-000D561079B0}";
        public const string AspNetMvc2 = "{F85E285D-A4E0-4152-9332-AB1D724D3325}";
        public const string AspNetMvc3 = "{E53F8FEA-EAE0-44A6-8774-FFD645390401}";
        public const string AspNetMvc4 = "{E3E379DF-F4C6-4180-9B81-6769533ABE47}";
        public const string AspNetMvc5 = "{349C5851-65DF-11DA-9384-00065B846F21}";
        public const string AspNet5 = "{8BB2217D-0F2D-49D1-97BC-3654ED321F3B}";

        // Windows apps
        public const string WindowsPhone = "{76F1466A-8B6D-4E39-A767-685A06062A39}";
        public const string WinFormsApp = "{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}";
        public const string WpfApp = "{60DC8134-EBA5-43B8-BCC9-BB4BC16C2548}";

        // Other
        public const string ClassLibrary = "{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}";
        public const string WebApplication = "{349C5851-65DF-11DA-9384-00065B846F21}";
        public const string WebSite = "{E24C65DC-7377-472B-9ABA-BC803B73C61A}";
    }

    /// <summary>
    /// Output types for projects.
    /// </summary>
    public static class OutputTypes
    {
        public const string Exe = "Exe";
        public const string WinExe = "WinExe";
        public const string Library = "Library";
    }

    /// <summary>
    /// Common file names for MSBuild files.
    /// </summary>
    public static class FileNames
    {
        public const string DirectoryBuildProps = "Directory.Build.props";
        public const string DirectoryBuildTargets = "Directory.Build.targets";
        public const string DirectoryPackagesProps = "Directory.Packages.props";
        public const string PackagesConfig = "packages.config";
    }

    /// <summary>
    /// Framework monikers for target frameworks.
    /// </summary>
    public static class FrameworkMonikers
    {
        // .NET (modern)
        public const string Net8 = "net8.0";
        public const string Net7 = "net7.0";
        public const string Net6 = "net6.0";
        public const string Net5 = "net5.0";

        // .NET Core
        public const string NetCoreApp31 = "netcoreapp3.1";
        public const string NetCoreApp30 = "netcoreapp3.0";
        public const string NetCoreApp22 = "netcoreapp2.2";
        public const string NetCoreApp21 = "netcoreapp2.1";

        // .NET Standard
        public const string NetStandard21 = "netstandard2.1";
        public const string NetStandard20 = "netstandard2.0";
        public const string NetStandard16 = "netstandard1.6";

        // .NET Framework
        public const string Net48 = "net48";
        public const string Net472 = "net472";
        public const string Net471 = "net471";
        public const string Net47 = "net47";
        public const string Net462 = "net462";
        public const string Net461 = "net461";
        public const string Net46 = "net46";
        public const string Net452 = "net452";
        public const string Net451 = "net451";
        public const string Net45 = "net45";

        // Legacy .NET Framework versions
        public const string NetFramework48 = "v4.8";
        public const string NetFramework472 = "v4.7.2";
        public const string NetFramework471 = "v4.7.1";
        public const string NetFramework47 = "v4.7";
        public const string NetFramework462 = "v4.6.2";
        public const string NetFramework461 = "v4.6.1";
        public const string NetFramework46 = "v4.6";
        public const string NetFramework452 = "v4.5.2";
        public const string NetFramework451 = "v4.5.1";
        public const string NetFramework45 = "v4.5";
        public const string NetFramework40 = "v4.0";
        public const string NetFramework35 = "v3.5";
    }
}
