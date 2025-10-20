using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using RefactorCsharpMCP.Core.Utilities;

namespace RefactorCsharpMCP.Tests.Utilities;

public class SymbolResolutionHelperTests
{
    [Fact]
    public void GetSymbolAtPosition_WithValidPosition_ReturnsSymbol()
    {
        // Arrange
        var helper = new SymbolResolutionHelper();
        var sourceCode = @"
public class TestClass
{
    public void TestMethod()
    {
        var x = 5;
    }
}";

        // Act - Position on "TestMethod" (line 4, column 17)
        var result = helper.GetSymbolAtPosition(sourceCode, 4, 17);

        // Assert
        result.Success.Should().BeTrue();
        result.Symbol.Should().NotBeNull();
        result.Symbol!.Name.Should().Be("TestMethod");
    }

    [Fact]
    public void GetSymbolAtPosition_WithEmptySourceCode_ReturnsFailure()
    {
        // Arrange
        var helper = new SymbolResolutionHelper();

        // Act
        var result = helper.GetSymbolAtPosition("", 1, 1);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Source code cannot be empty");
    }

    [Fact]
    public void GetSymbolAtPosition_WithInvalidLineNumber_ReturnsFailure()
    {
        // Arrange
        var helper = new SymbolResolutionHelper();
        var sourceCode = "public class TestClass { }";

        // Act
        var result = helper.GetSymbolAtPosition(sourceCode, 0, 1);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Invalid position");
    }

    [Fact]
    public void GetSymbolAtPosition_WithInvalidColumnNumber_ReturnsFailure()
    {
        // Arrange
        var helper = new SymbolResolutionHelper();
        var sourceCode = "public class TestClass { }";

        // Act
        var result = helper.GetSymbolAtPosition(sourceCode, 1, 0);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Invalid position");
    }

    [Fact]
    public void GetSymbolAtPosition_WithOutOfRangePosition_ReturnsFailure()
    {
        // Arrange
        var helper = new SymbolResolutionHelper();
        var sourceCode = "public class TestClass { }";

        // Act
        var result = helper.GetSymbolAtPosition(sourceCode, 100, 100);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("out of range");
    }

    [Fact]
    public void GetSymbolAtPosition_WithExistingSemanticModel_ReturnsSymbol()
    {
        // Arrange
        var helper = new SymbolResolutionHelper();
        var sourceCode = @"
public class TestClass
{
    public void TestMethod()
    {
        var x = 5;
    }
}";
        // Create compilation context
        var syntaxTree = CSharpSyntaxTree.ParseText(sourceCode);
        var compilation = CSharpCompilation.Create("temp")
            .AddReferences(MetadataReference.CreateFromFile(typeof(object).Assembly.Location))
            .AddSyntaxTrees(syntaxTree);
        var semanticModel = compilation.GetSemanticModel(syntaxTree);

        // Act - Position on "x" (line 6, column 13)
        var result = helper.GetSymbolAtPosition(semanticModel, syntaxTree, 6, 13);

        // Assert
        result.Success.Should().BeTrue();
        result.Symbol.Should().NotBeNull();
        result.Symbol!.Name.Should().Be("x");
        result.Symbol.Kind.Should().Be(SymbolKind.Local);
    }

    [Fact]
    public void GetSymbolAtPosition_WithExistingSemanticModel_MaintainsSyntaxTreeIdentity()
    {
        // Arrange
        var helper = new SymbolResolutionHelper();
        var sourceCode = @"
public class TestClass
{
    private int _field;

    public void Method()
    {
        var x = _field;
        var y = _field + 1;
    }
}";
        // Create compilation context
        var syntaxTree = CSharpSyntaxTree.ParseText(sourceCode);
        var compilation = CSharpCompilation.Create("temp")
            .AddReferences(MetadataReference.CreateFromFile(typeof(object).Assembly.Location))
            .AddSyntaxTrees(syntaxTree);
        var semanticModel = compilation.GetSemanticModel(syntaxTree);

        // Act - Resolve _field symbol
        var result = helper.GetSymbolAtPosition(semanticModel, syntaxTree, 4, 17);

        // Assert - Symbol should be resolvable
        result.Success.Should().BeTrue();
        result.Symbol.Should().NotBeNull();
        result.Symbol!.Name.Should().Be("_field");

        // Verify that we can find references using the SAME compilation
        var references = helper.GetAllReferences(result.Symbol, compilation);
        references.Should().NotBeEmpty();
        references.Count.Should().Be(2); // Two usages in Method()
    }

    [Fact]
    public void GetSymbolAtPosition_WithNullSemanticModel_ReturnsFailure()
    {
        // Arrange
        var helper = new SymbolResolutionHelper();
        var sourceCode = "public class TestClass { }";
        var syntaxTree = CSharpSyntaxTree.ParseText(sourceCode);

        // Act
        var result = helper.GetSymbolAtPosition(null!, syntaxTree, 1, 1);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Semantic model cannot be null");
    }

    [Fact]
    public void GetSymbolAtPosition_WithNullSyntaxTree_ReturnsFailure()
    {
        // Arrange
        var helper = new SymbolResolutionHelper();
        var sourceCode = "public class TestClass { }";
        var syntaxTree = CSharpSyntaxTree.ParseText(sourceCode);
        var compilation = CSharpCompilation.Create("temp")
            .AddReferences(MetadataReference.CreateFromFile(typeof(object).Assembly.Location))
            .AddSyntaxTrees(syntaxTree);
        var semanticModel = compilation.GetSemanticModel(syntaxTree);

        // Act
        var result = helper.GetSymbolAtPosition(semanticModel, null!, 1, 1);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Syntax tree cannot be null");
    }

    [Fact]
    public void GetSymbolAtPosition_WithExistingSemanticModel_HandlesInvalidPosition()
    {
        // Arrange
        var helper = new SymbolResolutionHelper();
        var sourceCode = "public class TestClass { }";
        var syntaxTree = CSharpSyntaxTree.ParseText(sourceCode);
        var compilation = CSharpCompilation.Create("temp")
            .AddReferences(MetadataReference.CreateFromFile(typeof(object).Assembly.Location))
            .AddSyntaxTrees(syntaxTree);
        var semanticModel = compilation.GetSemanticModel(syntaxTree);

        // Act
        var result = helper.GetSymbolAtPosition(semanticModel, syntaxTree, 100, 100);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("out of range");
    }

    [Fact]
    public void FindSymbolConflicts_WithNoConflicts_ReturnsNoConflicts()
    {
        // Arrange
        var helper = new SymbolResolutionHelper();
        var sourceCode = @"
public class TestClass
{
    private int _field;
    public void Method() { }
}";
        var syntaxTree = CSharpSyntaxTree.ParseText(sourceCode);
        var root = syntaxTree.GetRoot();
        var compilation = CSharpCompilation.Create("temp")
            .AddReferences(MetadataReference.CreateFromFile(typeof(object).Assembly.Location))
            .AddSyntaxTrees(syntaxTree);
        var semanticModel = compilation.GetSemanticModel(syntaxTree);
        var classDeclaration = root.DescendantNodes().OfType<ClassDeclarationSyntax>().First();

        // Act
        var result = helper.FindSymbolConflicts(semanticModel, "NewSymbol", classDeclaration);

        // Assert
        result.HasConflicts.Should().BeFalse();
    }

    [Fact]
    public void FindSymbolConflicts_WithFieldConflict_ReturnsConflict()
    {
        // Arrange
        var helper = new SymbolResolutionHelper();
        var sourceCode = @"
public class TestClass
{
    private int _field;

    public void Method()
    {
        var x = 1;
    }
}";
        var syntaxTree = CSharpSyntaxTree.ParseText(sourceCode);
        var root = syntaxTree.GetRoot();
        var compilation = CSharpCompilation.Create("temp")
            .AddReferences(MetadataReference.CreateFromFile(typeof(object).Assembly.Location))
            .AddSyntaxTrees(syntaxTree);
        var semanticModel = compilation.GetSemanticModel(syntaxTree);
        // Get a scope within a method where we can look up the field
        var methodDeclaration = root.DescendantNodes().OfType<MethodDeclarationSyntax>().First();

        // Act - Check for conflict within method scope
        var result = helper.FindSymbolConflicts(semanticModel, "_field", methodDeclaration);

        // Assert
        result.HasConflicts.Should().BeTrue();
        result.ConflictDescription.Should().Contain("_field");
    }

    [Fact]
    public void FindSymbolConflicts_WithMethodConflict_ReturnsConflict()
    {
        // Arrange
        var helper = new SymbolResolutionHelper();
        var sourceCode = @"
public class TestClass
{
    public void Method() { }
}";
        var syntaxTree = CSharpSyntaxTree.ParseText(sourceCode);
        var root = syntaxTree.GetRoot();
        var compilation = CSharpCompilation.Create("temp")
            .AddReferences(MetadataReference.CreateFromFile(typeof(object).Assembly.Location))
            .AddSyntaxTrees(syntaxTree);
        var semanticModel = compilation.GetSemanticModel(syntaxTree);
        var classDeclaration = root.DescendantNodes().OfType<ClassDeclarationSyntax>().First();

        // Act
        var result = helper.FindSymbolConflicts(semanticModel, "Method", classDeclaration);

        // Assert
        result.HasConflicts.Should().BeTrue();
        result.ConflictDescription.Should().Contain("Method");
    }

    [Fact]
    public void FindSymbolConflicts_WithEmptySymbolName_ReturnsNoConflicts()
    {
        // Arrange
        var helper = new SymbolResolutionHelper();
        var sourceCode = "public class TestClass { }";
        var syntaxTree = CSharpSyntaxTree.ParseText(sourceCode);
        var root = syntaxTree.GetRoot();
        var compilation = CSharpCompilation.Create("temp")
            .AddReferences(MetadataReference.CreateFromFile(typeof(object).Assembly.Location))
            .AddSyntaxTrees(syntaxTree);
        var semanticModel = compilation.GetSemanticModel(syntaxTree);
        var classDeclaration = root.DescendantNodes().OfType<ClassDeclarationSyntax>().First();

        // Act
        var result = helper.FindSymbolConflicts(semanticModel, "", classDeclaration);

        // Assert
        result.HasConflicts.Should().BeFalse();
        result.ConflictDescription.Should().Contain("empty");
    }

    [Fact]
    public void AnalyzeSymbolScope_WithMethodSymbol_ReturnsCorrectInfo()
    {
        // Arrange
        var helper = new SymbolResolutionHelper();
        var sourceCode = @"
public class TestClass
{
    public void TestMethod() { }
}";
        var syntaxTree = CSharpSyntaxTree.ParseText(sourceCode);
        var root = syntaxTree.GetRoot();
        var compilation = CSharpCompilation.Create("temp")
            .AddReferences(MetadataReference.CreateFromFile(typeof(object).Assembly.Location))
            .AddSyntaxTrees(syntaxTree);
        var semanticModel = compilation.GetSemanticModel(syntaxTree);
        var methodDeclaration = root.DescendantNodes().OfType<MethodDeclarationSyntax>().First();
        var symbol = semanticModel.GetDeclaredSymbol(methodDeclaration);

        // Act
        var result = helper.AnalyzeSymbolScope(symbol!);

        // Assert
        result.IsMethod.Should().BeTrue();
        result.IsPublic.Should().BeTrue();
        result.IsLocal.Should().BeFalse();
        result.IsField.Should().BeFalse();
        result.ScopeName.Should().Be("TestClass");
    }

    [Fact]
    public void AnalyzeSymbolScope_WithFieldSymbol_ReturnsCorrectInfo()
    {
        // Arrange
        var helper = new SymbolResolutionHelper();
        var sourceCode = @"
public class TestClass
{
    private int _field;
}";
        var syntaxTree = CSharpSyntaxTree.ParseText(sourceCode);
        var root = syntaxTree.GetRoot();
        var compilation = CSharpCompilation.Create("temp")
            .AddReferences(MetadataReference.CreateFromFile(typeof(object).Assembly.Location))
            .AddSyntaxTrees(syntaxTree);
        var semanticModel = compilation.GetSemanticModel(syntaxTree);
        var fieldDeclaration = root.DescendantNodes().OfType<FieldDeclarationSyntax>().First();
        var variable = fieldDeclaration.Declaration.Variables.First();
        var symbol = semanticModel.GetDeclaredSymbol(variable);

        // Act
        var result = helper.AnalyzeSymbolScope(symbol!);

        // Assert
        result.IsField.Should().BeTrue();
        result.IsPrivate.Should().BeTrue();
        result.IsMethod.Should().BeFalse();
        result.IsLocal.Should().BeFalse();
        result.ScopeName.Should().Be("TestClass");
    }

    [Fact]
    public void AnalyzeSymbolScope_WithNullSymbol_ReturnsUnknownScope()
    {
        // Arrange
        var helper = new SymbolResolutionHelper();

        // Act
        var result = helper.AnalyzeSymbolScope(null!);

        // Assert
        result.ScopeName.Should().Be("Unknown");
        result.IsLocal.Should().BeFalse();
        result.IsMethod.Should().BeFalse();
        result.IsField.Should().BeFalse();
    }

    [Fact]
    public void GetAllReferences_WithSymbol_ReturnsReferences()
    {
        // Arrange
        var helper = new SymbolResolutionHelper();
        var sourceCode = @"
public class TestClass
{
    private int _field;

    public void Method()
    {
        var x = _field;
        var y = _field + 1;
    }
}";
        var syntaxTree = CSharpSyntaxTree.ParseText(sourceCode);
        var root = syntaxTree.GetRoot();
        var compilation = CSharpCompilation.Create("temp")
            .AddReferences(MetadataReference.CreateFromFile(typeof(object).Assembly.Location))
            .AddSyntaxTrees(syntaxTree);
        var semanticModel = compilation.GetSemanticModel(syntaxTree);
        var fieldDeclaration = root.DescendantNodes().OfType<FieldDeclarationSyntax>().First();
        var variable = fieldDeclaration.Declaration.Variables.First();
        var symbol = semanticModel.GetDeclaredSymbol(variable);

        // Act
        var references = helper.GetAllReferences(symbol!, compilation);

        // Assert
        references.Should().NotBeEmpty();
        references.Count.Should().Be(2); // Two references to _field in the method
    }

    [Fact]
    public void GetAllReferences_WithNullSymbol_ReturnsEmptyList()
    {
        // Arrange
        var helper = new SymbolResolutionHelper();
        var sourceCode = "public class TestClass { }";
        var syntaxTree = CSharpSyntaxTree.ParseText(sourceCode);
        var compilation = CSharpCompilation.Create("temp")
            .AddReferences(MetadataReference.CreateFromFile(typeof(object).Assembly.Location))
            .AddSyntaxTrees(syntaxTree);

        // Act
        var references = helper.GetAllReferences(null!, compilation);

        // Assert
        references.Should().BeEmpty();
    }

    [Fact]
    public void GetAllReferences_WithNullCompilation_ReturnsEmptyList()
    {
        // Arrange
        var helper = new SymbolResolutionHelper();
        var sourceCode = @"
public class TestClass
{
    private int _field;
}";
        var syntaxTree = CSharpSyntaxTree.ParseText(sourceCode);
        var root = syntaxTree.GetRoot();
        var compilation = CSharpCompilation.Create("temp")
            .AddReferences(MetadataReference.CreateFromFile(typeof(object).Assembly.Location))
            .AddSyntaxTrees(syntaxTree);
        var semanticModel = compilation.GetSemanticModel(syntaxTree);
        var fieldDeclaration = root.DescendantNodes().OfType<FieldDeclarationSyntax>().First();
        var variable = fieldDeclaration.Declaration.Variables.First();
        var symbol = semanticModel.GetDeclaredSymbol(variable);

        // Act
        var references = helper.GetAllReferences(symbol!, null!);

        // Assert
        references.Should().BeEmpty();
    }
}
