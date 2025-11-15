using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System.Reflection;
using System.Runtime.InteropServices;

namespace RefactorCsharpMCP.Core.Framework;

/// <summary>
/// Builds CSharpCompilation instances with framework-specific configuration.
/// Configures language version, parse options, and reference assemblies based on target framework.
/// </summary>
public class CompilationContextBuilder
{
    private readonly LanguageVersionMapper _languageMapper;
    private readonly FrameworkValidator _validator;

    private string? _targetFramework;
    private string _assemblyName = "RefactoringCompilation";
    private List<SyntaxTree> _syntaxTrees = new();
    private List<MetadataReference> _additionalReferences = new();
    private CSharpCompilationOptions? _compilationOptions;

    /// <summary>
    /// Initializes a new instance of CompilationContextBuilder.
    /// </summary>
    /// <param name="validator">Optional validator for framework validation</param>
    /// <param name="languageMapper">Optional language version mapper</param>
    public CompilationContextBuilder(
        FrameworkValidator? validator = null,
        LanguageVersionMapper? languageMapper = null)
    {
        _validator = validator ?? new FrameworkValidator();
        _languageMapper = languageMapper ?? new LanguageVersionMapper(_validator);
    }

    /// <summary>
    /// Sets the target framework for the compilation context.
    /// </summary>
    /// <param name="targetFramework">The TFM (e.g., "net8.0", "net48")</param>
    /// <returns>This builder for fluent chaining</returns>
    public CompilationContextBuilder WithTargetFramework(string targetFramework)
    {
        _targetFramework = targetFramework ?? throw new ArgumentNullException(nameof(targetFramework));
        return this;
    }

    /// <summary>
    /// Sets the assembly name for the compilation.
    /// </summary>
    /// <param name="assemblyName">The assembly name</param>
    /// <returns>This builder for fluent chaining</returns>
    public CompilationContextBuilder WithAssemblyName(string assemblyName)
    {
        _assemblyName = assemblyName ?? throw new ArgumentNullException(nameof(assemblyName));
        return this;
    }

    /// <summary>
    /// Adds a syntax tree to the compilation.
    /// </summary>
    /// <param name="syntaxTree">The syntax tree to add</param>
    /// <returns>This builder for fluent chaining</returns>
    public CompilationContextBuilder AddSyntaxTree(SyntaxTree syntaxTree)
    {
        if (syntaxTree == null)
            throw new ArgumentNullException(nameof(syntaxTree));

        _syntaxTrees.Add(syntaxTree);
        return this;
    }

    /// <summary>
    /// Adds multiple syntax trees to the compilation.
    /// </summary>
    /// <param name="syntaxTrees">The syntax trees to add</param>
    /// <returns>This builder for fluent chaining</returns>
    public CompilationContextBuilder AddSyntaxTrees(IEnumerable<SyntaxTree> syntaxTrees)
    {
        if (syntaxTrees == null)
            throw new ArgumentNullException(nameof(syntaxTrees));

        _syntaxTrees.AddRange(syntaxTrees);
        return this;
    }

    /// <summary>
    /// Adds an additional metadata reference to the compilation.
    /// </summary>
    /// <param name="reference">The metadata reference to add</param>
    /// <returns>This builder for fluent chaining</returns>
    public CompilationContextBuilder AddReference(MetadataReference reference)
    {
        if (reference == null)
            throw new ArgumentNullException(nameof(reference));

        _additionalReferences.Add(reference);
        return this;
    }

    /// <summary>
    /// Adds multiple metadata references to the compilation.
    /// </summary>
    /// <param name="references">The metadata references to add</param>
    /// <returns>This builder for fluent chaining</returns>
    public CompilationContextBuilder AddReferences(IEnumerable<MetadataReference> references)
    {
        if (references == null)
            throw new ArgumentNullException(nameof(references));

        _additionalReferences.AddRange(references);
        return this;
    }

    /// <summary>
    /// Sets custom compilation options.
    /// If not set, default options suitable for refactoring will be used.
    /// </summary>
    /// <param name="options">The compilation options</param>
    /// <returns>This builder for fluent chaining</returns>
    public CompilationContextBuilder WithCompilationOptions(CSharpCompilationOptions options)
    {
        _compilationOptions = options ?? throw new ArgumentNullException(nameof(options));
        return this;
    }

    /// <summary>
    /// Builds the CSharpCompilation with framework-specific configuration.
    /// Validates the target framework and configures language version appropriately.
    /// </summary>
    /// <returns>Configured CSharpCompilation instance</returns>
    /// <exception cref="InvalidOperationException">Thrown if target framework is not set or invalid</exception>
    public CSharpCompilation Build()
    {
        // Validate target framework is set
        if (string.IsNullOrWhiteSpace(_targetFramework))
        {
            throw new InvalidOperationException(
                "Target framework must be set using WithTargetFramework() before building compilation.");
        }

        // Validate framework support
        var validationResult = _validator.Validate(_targetFramework);
        if (!validationResult.IsValid || !validationResult.IsSupported)
        {
            throw new InvalidOperationException(
                $"Invalid or unsupported target framework '{_targetFramework}': {validationResult.ErrorMessage}");
        }

        // Get language version for the target framework
        var languageVersion = _languageMapper.GetLanguageVersion(_targetFramework);
        if (!languageVersion.HasValue)
        {
            throw new InvalidOperationException(
                $"Could not determine language version for framework '{_targetFramework}'.");
        }

        // Create parse options with the correct language version
        var parseOptions = new CSharpParseOptions(languageVersion.Value);

        // Re-parse syntax trees with the correct language version
        var parsedTrees = _syntaxTrees
            .Select(tree => CSharpSyntaxTree.ParseText(tree.GetText(), parseOptions))
            .ToList();

        // Get compilation options (use provided or default)
        var compilationOptions = _compilationOptions ?? CreateDefaultCompilationOptions();

        // Load framework-specific BCL references
        var bclReferences = LoadBclReferences();

        // Combine BCL and additional references
        var allReferences = bclReferences.Concat(_additionalReferences);

        // Create compilation
        return CSharpCompilation.Create(
            _assemblyName,
            parsedTrees,
            allReferences,
            compilationOptions);
    }

    /// <summary>
    /// Creates default compilation options suitable for refactoring scenarios.
    /// </summary>
    private static CSharpCompilationOptions CreateDefaultCompilationOptions()
    {
        return new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
            .WithOptimizationLevel(OptimizationLevel.Debug)
            .WithPlatform(Platform.AnyCpu)
            .WithAllowUnsafe(true); // Allow unsafe code in refactoring scenarios
    }

    /// <summary>
    /// Loads BCL (Base Class Library) reference assemblies for the current runtime.
    /// Phase 1: Uses current runtime's references.
    /// Phase 2+: Will load framework-specific reference assemblies based on target framework.
    /// </summary>
    private IEnumerable<MetadataReference> LoadBclReferences()
    {
        // For Phase 1 (v1.0), we use the current runtime's BCL references
        // This works well for net8.0 and provides reasonable compatibility for other frameworks

        var references = new List<MetadataReference>();

        // Add core runtime assemblies
        var runtimeAssemblies = new[]
        {
            typeof(object).Assembly,                    // System.Private.CoreLib
            typeof(Console).Assembly,                   // System.Console
            typeof(Enumerable).Assembly,                // System.Linq
            typeof(System.Collections.Generic.List<>).Assembly, // System.Collections
            Assembly.Load("System.Runtime"),
            Assembly.Load("System.Collections"),
            Assembly.Load("netstandard")
        };

        foreach (var assembly in runtimeAssemblies)
        {
            if (!string.IsNullOrWhiteSpace(assembly.Location))
            {
                references.Add(MetadataReference.CreateFromFile(assembly.Location));
            }
        }

        // Add additional common references based on runtime location
        var runtimePath = Path.GetDirectoryName(typeof(object).Assembly.Location);
        if (!string.IsNullOrWhiteSpace(runtimePath))
        {
            var additionalAssemblies = new[]
            {
                "System.Linq.Expressions.dll",
                "System.ObjectModel.dll",
                "System.Text.RegularExpressions.dll",
                "System.Threading.dll",
                "System.Threading.Tasks.dll"
            };

            foreach (var assemblyName in additionalAssemblies)
            {
                var assemblyPath = Path.Combine(runtimePath, assemblyName);
                if (File.Exists(assemblyPath))
                {
                    references.Add(MetadataReference.CreateFromFile(assemblyPath));
                }
            }
        }

        return references;
    }

    /// <summary>
    /// Creates a simple compilation context for quick refactoring scenarios.
    /// Uses current runtime references and parses source code with framework-appropriate language version.
    /// </summary>
    /// <param name="sourceCode">The C# source code to parse</param>
    /// <param name="targetFramework">The target framework (e.g., "net8.0")</param>
    /// <param name="assemblyName">Optional assembly name (defaults to "RefactoringCompilation")</param>
    /// <returns>Configured CSharpCompilation</returns>
    public static CSharpCompilation CreateSimple(
        string sourceCode,
        string targetFramework,
        string assemblyName = "RefactoringCompilation")
    {
        if (string.IsNullOrWhiteSpace(sourceCode))
            throw new ArgumentNullException(nameof(sourceCode));

        var builder = new CompilationContextBuilder()
            .WithTargetFramework(targetFramework)
            .WithAssemblyName(assemblyName);

        // Parse the source code (will be re-parsed with correct language version in Build())
        var syntaxTree = CSharpSyntaxTree.ParseText(sourceCode);
        builder.AddSyntaxTree(syntaxTree);

        return builder.Build();
    }
}
