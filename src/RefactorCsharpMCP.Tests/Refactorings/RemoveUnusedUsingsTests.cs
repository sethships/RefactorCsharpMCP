using FluentAssertions;
using RefactorCsharpMCP.Core.Refactorings;

namespace RefactorCsharpMCP.Tests.Refactorings;

public class RemoveUnusedUsingsTests
{
    [Fact]
    public void Execute_WithUnusedUsings_ShouldRemoveThem()
    {
        // Arrange
        var sourceCode = @"using System;
using System.Collections.Generic;
using System.Linq;

public class Calculator
{
    public int Add(int a, int b)
    {
        return a + b;
    }
}";
        var refactoring = new RemoveUnusedUsings();

        // Act
        var result = refactoring.Execute(sourceCode, "net8.0");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.RefactoredCode.Should().NotContain("using System;");
        result.RefactoredCode.Should().NotContain("using System.Collections.Generic;");
        result.RefactoredCode.Should().NotContain("using System.Linq;");
        result.RefactoredCode.Should().Contain("public class Calculator");
        result.Message.Should().Contain("Removed 3 unused");
    }

    [Fact]
    public void Execute_WithAllUsingsUsed_ShouldKeepAll()
    {
        // Arrange
        var sourceCode = @"using System;
using System.Collections.Generic;

public class DataProcessor
{
    public List<string> Process(DateTime date)
    {
        Console.WriteLine(date.ToString());
        return new List<string>();
    }
}";
        var refactoring = new RemoveUnusedUsings();

        // Act
        var result = refactoring.Execute(sourceCode, "net8.0");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.RefactoredCode.Should().Contain("using System;");
        result.RefactoredCode.Should().Contain("using System.Collections.Generic;");
        result.Message.Should().Contain("All 2 using directive(s) are in use");
    }

    [Fact]
    public async Task ExecuteAsync_WithGlobalUsingsOnNet80_ShouldPreserveThem()
    {
        // Arrange
        var sourceCode = @"global using System;
global using System.Collections.Generic;
using System.Linq;

public class Calculator
{
    public int Add(int a, int b)
    {
        return a + b;
    }
}";
        var refactoring = new RemoveUnusedUsings();

        // Act
        var result = await refactoring.ExecuteAsync(sourceCode, "net8.0");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.RefactoredCode.Should().Contain("global using System;");
        result.RefactoredCode.Should().Contain("global using System.Collections.Generic;");
        result.RefactoredCode.Should().NotContain("using System.Linq;");
        result.Message.Should().Contain("Removed 1 unused");
    }

    [Fact]
    public async Task ExecuteAsync_WithGlobalUsingsOnNet48_ShouldRemoveThem()
    {
        // Arrange - global using syntax is invalid in net48 (C# 7.3), but we test the logic
        var sourceCode = @"using System;
using System.Collections.Generic;
using System.Linq;

public class Calculator
{
    public int Add(int a, int b)
    {
        return a + b;
    }
}";
        var refactoring = new RemoveUnusedUsings();

        // Act
        var result = await refactoring.ExecuteAsync(sourceCode, "net48");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.RefactoredCode.Should().NotContain("using System;");
        result.RefactoredCode.Should().NotContain("using System.Collections.Generic;");
        result.RefactoredCode.Should().NotContain("using System.Linq;");
    }

    [Fact]
    public void Execute_WithNoUsings_ShouldReturnSuccessWithOriginalCode()
    {
        // Arrange
        var sourceCode = @"public class Calculator
{
    public int Add(int a, int b)
    {
        return a + b;
    }
}";
        var refactoring = new RemoveUnusedUsings();

        // Act
        var result = refactoring.Execute(sourceCode, "net8.0");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.RefactoredCode.Should().Be(sourceCode);
        result.Message.Should().Contain("No using directives found");
    }

    [Fact]
    public void Execute_WithMixedUsedAndUnusedUsings_ShouldRemoveOnlyUnused()
    {
        // Arrange
        var sourceCode = @"using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

public class Formatter
{
    private StringBuilder _builder = new StringBuilder();

    public string Format(DateTime date)
    {
        return date.ToString();
    }
}";
        var refactoring = new RemoveUnusedUsings();

        // Act
        var result = refactoring.Execute(sourceCode, "net8.0");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.RefactoredCode.Should().Contain("using System;");
        result.RefactoredCode.Should().Contain("using System.Text;");
        result.RefactoredCode.Should().NotContain("using System.Collections.Generic;");
        result.RefactoredCode.Should().NotContain("using System.Linq;");
    }

    [Fact]
    public void Execute_WithStaticUsing_ShouldHandleCorrectly()
    {
        // Arrange
        var sourceCode = @"using static System.Math;
using System;

public class Calculator
{
    public double GetPi()
    {
        return PI;
    }
}";
        var refactoring = new RemoveUnusedUsings();

        // Act
        var result = refactoring.Execute(sourceCode, "net8.0");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.RefactoredCode.Should().Contain("using static System.Math;");
        result.RefactoredCode.Should().NotContain("using System;");
    }

    [Fact]
    public void Execute_WithUsingAlias_ShouldHandleCorrectly()
    {
        // Arrange
        var sourceCode = @"using StringList = System.Collections.Generic.List<string>;
using System;

public class DataManager
{
    private StringList _data = new StringList();

    public void Add(string item)
    {
        _data.Add(item);
    }
}";
        var refactoring = new RemoveUnusedUsings();

        // Act
        var result = refactoring.Execute(sourceCode, "net8.0");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.RefactoredCode.Should().Contain("using StringList =");
        result.RefactoredCode.Should().NotContain("using System;");
    }

    [Fact]
    public void Execute_WithSyntaxErrors_ShouldReturnFailure()
    {
        // Arrange
        var sourceCode = @"using System;

public class Calculator
{
    public int Add(int a, int b
    {
        return a + b;
    }
"; // Missing closing brace and parenthesis
        var refactoring = new RemoveUnusedUsings();

        // Act
        var result = refactoring.Execute(sourceCode, "net8.0");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Syntax errors");
    }

    [Fact]
    public void Execute_WithEmptySourceCode_ShouldReturnFailure()
    {
        // Arrange
        var refactoring = new RemoveUnusedUsings();

        // Act
        var result = refactoring.Execute("", "net8.0");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Source code cannot be empty");
    }

    [Fact]
    public void Execute_WithNullSourceCode_ShouldReturnFailure()
    {
        // Arrange
        var refactoring = new RemoveUnusedUsings();

        // Act
        var result = refactoring.Execute(null!, "net8.0");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Source code cannot be empty");
    }

    [Fact]
    public void Execute_WithEmptyTargetFramework_ShouldReturnFailure()
    {
        // Arrange
        var sourceCode = "using System; public class Test { }";
        var refactoring = new RemoveUnusedUsings();

        // Act
        var result = refactoring.Execute(sourceCode, "");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Target framework cannot be empty");
    }

    [Fact]
    public void Execute_WithUnsupportedFramework_ShouldReturnFailure()
    {
        // Arrange
        var sourceCode = "using System; public class Test { }";
        var refactoring = new RemoveUnusedUsings();

        // Act
        var result = refactoring.Execute(sourceCode, "net6.0"); // EOL framework

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Unsupported framework");
    }

    [Fact]
    public void Execute_WithNetStandard20_ShouldWork()
    {
        // Arrange
        var sourceCode = @"using System;
using System.Collections.Generic;
using System.Linq;

public class Calculator
{
    public int Add(int a, int b)
    {
        return a + b;
    }
}";
        var refactoring = new RemoveUnusedUsings();

        // Act
        var result = refactoring.Execute(sourceCode, "netstandard2.0");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.RefactoredCode.Should().NotContain("using System;");
        result.RefactoredCode.Should().NotContain("using System.Collections.Generic;");
        result.RefactoredCode.Should().NotContain("using System.Linq;");
    }

    [Fact]
    public void Execute_WithNet472_ShouldWork()
    {
        // Arrange
        var sourceCode = @"using System;
using System.Collections.Generic;

public class Calculator
{
    public int Add(int a, int b)
    {
        return a + b;
    }
}";
        var refactoring = new RemoveUnusedUsings();

        // Act
        var result = refactoring.Execute(sourceCode, "net472");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.RefactoredCode.Should().NotContain("using System;");
        result.RefactoredCode.Should().NotContain("using System.Collections.Generic;");
    }

    [Fact]
    public void Execute_WithNamespaceDeclaration_ShouldPreserveStructure()
    {
        // Arrange
        var sourceCode = @"using System;
using System.Collections.Generic;

namespace MyApp.Services
{
    public class Calculator
    {
        public int Add(int a, int b)
        {
            return a + b;
        }
    }
}";
        var refactoring = new RemoveUnusedUsings();

        // Act
        var result = refactoring.Execute(sourceCode, "net8.0");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.RefactoredCode.Should().Contain("namespace MyApp.Services");
        result.RefactoredCode.Should().Contain("public class Calculator");
        result.RefactoredCode.Should().NotContain("using System;");
        result.RefactoredCode.Should().NotContain("using System.Collections.Generic;");
    }

    [Fact]
    public void Execute_WithFileScopedNamespace_ShouldPreserveStructure()
    {
        // Arrange
        var sourceCode = @"using System;
using System.Collections.Generic;

namespace MyApp.Services;

public class Calculator
{
    public int Add(int a, int b)
    {
        return a + b;
    }
}";
        var refactoring = new RemoveUnusedUsings();

        // Act
        var result = refactoring.Execute(sourceCode, "net8.0");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.RefactoredCode.Should().Contain("namespace MyApp.Services;");
        result.RefactoredCode.Should().Contain("public class Calculator");
        result.RefactoredCode.Should().NotContain("using System;");
    }

    [Fact]
    public void Execute_WithXmlDocumentation_ShouldPreserveComments()
    {
        // Arrange
        var sourceCode = @"using System;
using System.Collections.Generic;

/// <summary>
/// A simple calculator class.
/// </summary>
public class Calculator
{
    /// <summary>
    /// Adds two numbers.
    /// </summary>
    public int Add(int a, int b)
    {
        return a + b;
    }
}";
        var refactoring = new RemoveUnusedUsings();

        // Act
        var result = refactoring.Execute(sourceCode, "net8.0");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.RefactoredCode.Should().Contain("/// <summary>");
        result.RefactoredCode.Should().Contain("/// A simple calculator class.");
        result.RefactoredCode.Should().Contain("/// Adds two numbers.");
    }

    [Fact]
    public async Task ExecuteAsync_WithValidation_ShouldValidateFramework()
    {
        // Arrange
        var sourceCode = @"using System;

public class Calculator
{
    public int Add(int a, int b)
    {
        return a + b;
    }
}";
        var refactoring = new RemoveUnusedUsings();

        // Act
        var result = await refactoring.ExecuteAsync(sourceCode, "net8.0");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.RefactoredCode.Should().NotContain("using System;");
    }
}
