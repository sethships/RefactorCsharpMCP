using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using RefactorCsharpMCP.Core.Framework;
using Xunit;

namespace RefactorCsharpMCP.Tests.Framework;

/// <summary>
/// Unit tests for CompilationContextBuilder covering compilation creation and configuration.
/// </summary>
public class CompilationContextBuilderTests
{
    #region Builder Pattern Tests

    [Fact]
    public void Build_WithTargetFramework_CreatesCompilation()
    {
        // Arrange
        var builder = new CompilationContextBuilder()
            .WithTargetFramework("net8.0")
            .WithAssemblyName("TestAssembly");

        var syntaxTree = CSharpSyntaxTree.ParseText("class TestClass { }");
        builder.AddSyntaxTree(syntaxTree);

        // Act
        var compilation = builder.Build();

        // Assert
        Assert.NotNull(compilation);
        Assert.Equal("TestAssembly", compilation.AssemblyName);
        Assert.Single(compilation.SyntaxTrees);
    }

    [Fact]
    public void Build_WithoutTargetFramework_ThrowsInvalidOperationException()
    {
        // Arrange
        var builder = new CompilationContextBuilder();

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() => builder.Build());
        Assert.Contains("Target framework must be set", exception.Message);
    }

    [Fact]
    public void Build_WithUnsupportedFramework_ThrowsInvalidOperationException()
    {
        // Arrange
        var builder = new CompilationContextBuilder()
            .WithTargetFramework("net6.0"); // EOL framework

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() => builder.Build());
        Assert.Contains("unsupported target framework", exception.Message);
    }

    [Fact]
    public void Build_ConfiguresCorrectLanguageVersion_ForNet8()
    {
        // Arrange
        var sourceCode = "class TestClass { }";
        var builder = new CompilationContextBuilder()
            .WithTargetFramework("net8.0")
            .AddSyntaxTree(CSharpSyntaxTree.ParseText(sourceCode));

        // Act
        var compilation = builder.Build();

        // Assert
        var tree = compilation.SyntaxTrees.First();
        Assert.Equal(LanguageVersion.CSharp12, ((CSharpParseOptions)tree.Options).LanguageVersion);
    }

    [Fact]
    public void Build_ConfiguresCorrectLanguageVersion_ForNet48()
    {
        // Arrange
        var sourceCode = "class TestClass { }";
        var builder = new CompilationContextBuilder()
            .WithTargetFramework("net48")
            .AddSyntaxTree(CSharpSyntaxTree.ParseText(sourceCode));

        // Act
        var compilation = builder.Build();

        // Assert
        var tree = compilation.SyntaxTrees.First();
        Assert.Equal(LanguageVersion.CSharp7_3, ((CSharpParseOptions)tree.Options).LanguageVersion);
    }

    [Fact]
    public void AddReference_AddsMetadataReference()
    {
        // Arrange
        var reference = MetadataReference.CreateFromFile(typeof(object).Assembly.Location);
        var builder = new CompilationContextBuilder()
            .WithTargetFramework("net8.0")
            .AddReference(reference);

        // Act
        var compilation = builder.Build();

        // Assert
        Assert.Contains(reference, compilation.References);
    }

    [Fact]
    public void AddReferences_AddsMultipleReferences()
    {
        // Arrange
        var references = new[]
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Console).Assembly.Location)
        };
        var builder = new CompilationContextBuilder()
            .WithTargetFramework("net8.0")
            .AddReferences(references);

        // Act
        var compilation = builder.Build();

        // Assert
        Assert.True(compilation.References.Count() >= 2);
    }

    [Fact]
    public void AddSyntaxTrees_AddsMultipleTrees()
    {
        // Arrange
        var tree1 = CSharpSyntaxTree.ParseText("class Class1 { }");
        var tree2 = CSharpSyntaxTree.ParseText("class Class2 { }");
        var builder = new CompilationContextBuilder()
            .WithTargetFramework("net8.0")
            .AddSyntaxTrees(new[] { tree1, tree2 });

        // Act
        var compilation = builder.Build();

        // Assert
        Assert.Equal(2, compilation.SyntaxTrees.Count());
    }

    #endregion

    #region CreateSimple Tests

    [Fact]
    public void CreateSimple_WithValidSource_CreatesCompilation()
    {
        // Arrange
        var sourceCode = "class TestClass { public void Method() { } }";

        // Act
        var compilation = CompilationContextBuilder.CreateSimple(sourceCode, "net8.0");

        // Assert
        Assert.NotNull(compilation);
        Assert.Equal("RefactoringCompilation", compilation.AssemblyName);
        Assert.Single(compilation.SyntaxTrees);
    }

    [Fact]
    public void CreateSimple_WithCustomAssemblyName_UsesProvidedName()
    {
        // Arrange
        var sourceCode = "class TestClass { }";

        // Act
        var compilation = CompilationContextBuilder.CreateSimple(
            sourceCode,
            "net8.0",
            "CustomAssembly");

        // Assert
        Assert.Equal("CustomAssembly", compilation.AssemblyName);
    }

    [Fact]
    public void WithReferenceAssemblyResolver_UsesCustomResolver()
    {
        // Arrange
        var customReferenceCalled = false;
        var customReferences = new[]
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(System.Console).Assembly.Location)
        };

        Func<string?, IEnumerable<MetadataReference>> customResolver = (tfm) =>
        {
            customReferenceCalled = true;
            Assert.Equal("net8.0", tfm);
            return customReferences;
        };

        var builder = new CompilationContextBuilder()
            .WithTargetFramework("net8.0")
            .WithReferenceAssemblyResolver(customResolver)
            .AddSyntaxTree(CSharpSyntaxTree.ParseText("class Test { }"));

        // Act
        var compilation = builder.Build();

        // Assert
        Assert.True(customReferenceCalled, "Custom resolver should have been called");
        Assert.NotNull(compilation);
        // Verify custom references are present (at least the two we added)
        Assert.True(compilation.References.Count() >= 2, $"Expected at least 2 references, got {compilation.References.Count()}");
    }

    [Fact]
    public void WithReferenceAssemblyResolver_WithNullResolver_ThrowsArgumentNullException()
    {
        // Arrange
        var builder = new CompilationContextBuilder();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => builder.WithReferenceAssemblyResolver(null!));
    }

    [Fact]
    public void Build_WithoutCustomResolver_UsesDefaultReferences()
    {
        // Arrange
        var builder = new CompilationContextBuilder()
            .WithTargetFramework("net8.0")
            .AddSyntaxTree(CSharpSyntaxTree.ParseText("class Test { }"));

        // Act
        var compilation = builder.Build();

        // Assert
        Assert.NotNull(compilation);
        // Default BCL loading provides multiple references
        Assert.True(compilation.References.Count() > 5,
            $"Expected more than 5 default references, got {compilation.References.Count()}");
    }

    #endregion
}
