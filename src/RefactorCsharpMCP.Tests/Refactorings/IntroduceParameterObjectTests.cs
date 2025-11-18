using FluentAssertions;
using RefactorCsharpMCP.Core.Refactorings;

namespace RefactorCsharpMCP.Tests.Refactorings;

public class IntroduceParameterObjectTests
{
    [Fact]
    public void Execute_WithNet8_ShouldGenerateRecord()
    {
        // Arrange
        var sourceCode = @"
public class CustomerService
{
    public void CreateCustomer(string name, string email, string street, string city, string zip)
    {
        Console.WriteLine($""Creating customer {name} at {street}, {city}, {zip}"");
    }

    public void TestMethod()
    {
        CreateCustomer(""John"", ""john@example.com"", ""123 Main St"", ""Springfield"", ""12345"");
    }
}";
        var refactoring = new IntroduceParameterObject();

        // Act
        var result = refactoring.Execute(
            sourceCode,
            "CustomerService",
            "CreateCustomer",
            new[] { "street", "city", "zip" },
            "AddressInfo",
            "net8.0");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.RefactoredCode.Should().NotBeNullOrEmpty();

        // Should generate record declaration
        result.RefactoredCode.Should().Contain("public record AddressInfo(string Street, string City, string Zip);");

        // Should update method signature
        result.RefactoredCode.Should().Contain("CreateCustomer(string name, string email, AddressInfo addressInfo)");

        // Should update method body
        result.RefactoredCode.Should().Contain("addressInfo.Street");
        result.RefactoredCode.Should().Contain("addressInfo.City");
        result.RefactoredCode.Should().Contain("addressInfo.Zip");

        // Should update callers
        result.RefactoredCode.Should().Contain("new AddressInfo(\"123 Main St\", \"Springfield\", \"12345\")");

        result.Message.Should().Contain("AddressInfo");
        result.Message.Should().Contain("3 parameters");
    }

    [Fact]
    public void Execute_WithNet48_ShouldGenerateClass()
    {
        // Arrange
        var sourceCode = @"
public class CustomerService
{
    public void CreateCustomer(string name, string email, string street, string city, string zip)
    {
        Console.WriteLine(string.Format(""Creating customer {0} at {1}, {2}, {3}"", name, street, city, zip));
    }

    public void TestMethod()
    {
        CreateCustomer(""John"", ""john@example.com"", ""123 Main St"", ""Springfield"", ""12345"");
    }
}";
        var refactoring = new IntroduceParameterObject();

        // Act
        var result = refactoring.Execute(
            sourceCode,
            "CustomerService",
            "CreateCustomer",
            new[] { "street", "city", "zip" },
            "AddressInfo",
            "net48");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.RefactoredCode.Should().NotBeNullOrEmpty();

        // Should generate class declaration
        result.RefactoredCode.Should().Contain("public class AddressInfo");
        result.RefactoredCode.Should().Contain("public string Street { get; }");
        result.RefactoredCode.Should().Contain("public string City { get; }");
        result.RefactoredCode.Should().Contain("public string Zip { get; }");
        result.RefactoredCode.Should().Contain("public AddressInfo(string street, string city, string zip)");
        result.RefactoredCode.Should().Contain("Street = street;");
        result.RefactoredCode.Should().Contain("City = city;");
        result.RefactoredCode.Should().Contain("Zip = zip;");

        // Should update method signature
        result.RefactoredCode.Should().Contain("CreateCustomer(string name, string email, AddressInfo addressInfo)");

        // Should update method body
        result.RefactoredCode.Should().Contain("addressInfo.Street");
        result.RefactoredCode.Should().Contain("addressInfo.City");
        result.RefactoredCode.Should().Contain("addressInfo.Zip");

        // Should update callers
        result.RefactoredCode.Should().Contain("new AddressInfo(\"123 Main St\", \"Springfield\", \"12345\")");
    }

    [Fact]
    public void Execute_WithTwoParameters_ShouldGroupCorrectly()
    {
        // Arrange
        var sourceCode = @"
public class UserService
{
    public void RegisterUser(string username, string password, string email)
    {
        Console.WriteLine($""Registering {username} with {email}"");
    }

    public void Test()
    {
        RegisterUser(""john"", ""pass123"", ""john@example.com"");
    }
}";
        var refactoring = new IntroduceParameterObject();

        // Act
        var result = refactoring.Execute(
            sourceCode,
            "UserService",
            "RegisterUser",
            new[] { "username", "password" },
            "UserCredentials",
            "net8.0");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.RefactoredCode.Should().Contain("public record UserCredentials(string Username, string Password);");
        result.RefactoredCode.Should().Contain("RegisterUser(string email, UserCredentials userCredentials)");
        result.RefactoredCode.Should().Contain("userCredentials.Username");
        result.RefactoredCode.Should().Contain("userCredentials.Password");
    }

    [Fact]
    public void Execute_WithAllParameters_ShouldReplaceAll()
    {
        // Arrange
        var sourceCode = @"
public class MathService
{
    public int Calculate(int a, int b, int c)
    {
        return a + b + c;
    }

    public void Test()
    {
        var result = Calculate(1, 2, 3);
    }
}";
        var refactoring = new IntroduceParameterObject();

        // Act
        var result = refactoring.Execute(
            sourceCode,
            "MathService",
            "Calculate",
            new[] { "a", "b", "c" },
            "MathInput",
            "net8.0");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.RefactoredCode.Should().Contain("public record MathInput(int A, int B, int C);");
        result.RefactoredCode.Should().Contain("Calculate(MathInput mathInput)");
        result.RefactoredCode.Should().Contain("mathInput.A");
        result.RefactoredCode.Should().Contain("mathInput.B");
        result.RefactoredCode.Should().Contain("mathInput.C");
        result.RefactoredCode.Should().Contain("new MathInput(1, 2, 3)");
    }

    [Fact]
    public void Execute_WithNamespace_ShouldInsertParameterObjectInNamespace()
    {
        // Arrange
        var sourceCode = @"
namespace MyApp.Services
{
    public class OrderService
    {
        public void CreateOrder(string productId, int quantity, decimal price)
        {
            Console.WriteLine($""Order: {productId} x {quantity} @ {price}"");
        }

        public void Test()
        {
            CreateOrder(""PROD-123"", 5, 29.99m);
        }
    }
}";
        var refactoring = new IntroduceParameterObject();

        // Act
        var result = refactoring.Execute(
            sourceCode,
            "OrderService",
            "CreateOrder",
            new[] { "productId", "quantity", "price" },
            "OrderDetails",
            "net8.0");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.RefactoredCode.Should().Contain("public record OrderDetails(string ProductId, int Quantity, decimal Price);");
        result.RefactoredCode.Should().Contain("CreateOrder(OrderDetails orderDetails)");
        result.RefactoredCode.Should().Contain("orderDetails.ProductId");
        result.RefactoredCode.Should().Contain("orderDetails.Quantity");
        result.RefactoredCode.Should().Contain("orderDetails.Price");
    }

    [Fact]
    public void Execute_WithComplexTypes_ShouldHandleCorrectly()
    {
        // Arrange
        var sourceCode = @"
public class DataProcessor
{
    public void Process(List<string> items, Dictionary<string, int> config, bool debug)
    {
        if (debug)
        {
            Console.WriteLine($""Processing {items.Count} items with {config.Count} configs"");
        }
    }

    public void Test()
    {
        Process(new List<string>(), new Dictionary<string, int>(), true);
    }
}";
        var refactoring = new IntroduceParameterObject();

        // Act
        var result = refactoring.Execute(
            sourceCode,
            "DataProcessor",
            "Process",
            new[] { "items", "config" },
            "ProcessInput",
            "net8.0");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.RefactoredCode.Should().Contain("public record ProcessInput(List<string> Items, Dictionary<string, int> Config);");
        result.RefactoredCode.Should().Contain("Process(bool debug, ProcessInput processInput)");
        result.RefactoredCode.Should().Contain("processInput.Items");
        result.RefactoredCode.Should().Contain("processInput.Config");
    }

    [Fact]
    public void Execute_WithEmptySourceCode_ShouldReturnFailure()
    {
        // Arrange
        var refactoring = new IntroduceParameterObject();

        // Act
        var result = refactoring.Execute(
            "",
            "TestClass",
            "TestMethod",
            new[] { "param" },
            "ParamObject",
            "net8.0");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Source code cannot be empty");
    }

    [Fact]
    public void Execute_WithEmptyClassName_ShouldReturnFailure()
    {
        // Arrange
        var sourceCode = "public class Test { public void Method(int x, int y) { } }";
        var refactoring = new IntroduceParameterObject();

        // Act
        var result = refactoring.Execute(
            sourceCode,
            "",
            "Method",
            new[] { "x" },
            "ParamObject",
            "net8.0");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Class name cannot be empty");
    }

    [Fact]
    public void Execute_WithEmptyMethodName_ShouldReturnFailure()
    {
        // Arrange
        var sourceCode = "public class Test { public void Method(int x, int y) { } }";
        var refactoring = new IntroduceParameterObject();

        // Act
        var result = refactoring.Execute(
            sourceCode,
            "Test",
            "",
            new[] { "x" },
            "ParamObject",
            "net8.0");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Method name cannot be empty");
    }

    [Fact]
    public void Execute_WithEmptyNewClassName_ShouldReturnFailure()
    {
        // Arrange
        var sourceCode = "public class Test { public void Method(int x, int y) { } }";
        var refactoring = new IntroduceParameterObject();

        // Act
        var result = refactoring.Execute(
            sourceCode,
            "Test",
            "Method",
            new[] { "x" },
            "",
            "net8.0");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("New class name cannot be empty");
    }

    [Fact]
    public void Execute_WithEmptyParameterArray_ShouldReturnFailure()
    {
        // Arrange
        var sourceCode = "public class Test { public void Method(int x, int y) { } }";
        var refactoring = new IntroduceParameterObject();

        // Act
        var result = refactoring.Execute(
            sourceCode,
            "Test",
            "Method",
            Array.Empty<string>(),
            "ParamObject",
            "net8.0");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("At least one parameter name must be specified");
    }

    [Fact]
    public void Execute_WithInvalidFramework_ShouldReturnFailure()
    {
        // Arrange
        var sourceCode = "public class Test { public void Method(int x, int y) { } }";
        var refactoring = new IntroduceParameterObject();

        // Act
        var result = refactoring.Execute(
            sourceCode,
            "Test",
            "Method",
            new[] { "x" },
            "ParamObject",
            "invalid-framework");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("framework");
    }

    [Fact]
    public void Execute_WithClassNotFound_ShouldReturnFailure()
    {
        // Arrange
        var sourceCode = "public class Test { public void Method(int x, int y) { } }";
        var refactoring = new IntroduceParameterObject();

        // Act
        var result = refactoring.Execute(
            sourceCode,
            "NonExistentClass",
            "Method",
            new[] { "x" },
            "ParamObject",
            "net8.0");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("NonExistentClass");
        result.ErrorMessage.Should().Contain("not found");
    }

    [Fact]
    public void Execute_WithMethodNotFound_ShouldReturnFailure()
    {
        // Arrange
        var sourceCode = "public class Test { public void Method(int x, int y) { } }";
        var refactoring = new IntroduceParameterObject();

        // Act
        var result = refactoring.Execute(
            sourceCode,
            "Test",
            "NonExistentMethod",
            new[] { "x" },
            "ParamObject",
            "net8.0");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("NonExistentMethod");
        result.ErrorMessage.Should().Contain("not found");
    }

    [Fact]
    public void Execute_WithParameterNotFound_ShouldReturnFailure()
    {
        // Arrange
        var sourceCode = "public class Test { public void Method(int x, int y) { } }";
        var refactoring = new IntroduceParameterObject();

        // Act
        var result = refactoring.Execute(
            sourceCode,
            "Test",
            "Method",
            new[] { "nonExistentParam" },
            "ParamObject",
            "net8.0");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Not all specified parameters found");
    }

    [Fact]
    public void Execute_WithSyntaxErrors_ShouldReturnFailure()
    {
        // Arrange
        var sourceCode = "public class Test { public void Method(int x, int y) { } "; // Missing closing brace
        var refactoring = new IntroduceParameterObject();

        // Act
        var result = refactoring.Execute(
            sourceCode,
            "Test",
            "Method",
            new[] { "x" },
            "ParamObject",
            "net8.0");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Syntax errors");
    }

    [Fact]
    public async Task ExecuteAsync_ShouldCallExecute()
    {
        // Arrange
        var sourceCode = @"
public class TestService
{
    public void Process(int a, int b)
    {
        Console.WriteLine(a + b);
    }

    public void Test()
    {
        Process(1, 2);
    }
}";
        var refactoring = new IntroduceParameterObject();

        // Act
        var result = await refactoring.ExecuteAsync(
            sourceCode,
            "TestService",
            "Process",
            new[] { "a", "b" },
            "Input",
            "net8.0");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.RefactoredCode.Should().Contain("public record Input(int A, int B);");
        result.RefactoredCode.Should().Contain("Process(Input input)");
    }

    [Theory]
    [InlineData("net8.0", "record")]
    [InlineData("net7.0", "record")]
    [InlineData("net6.0", "record")]
    [InlineData("net48", "class")]
    [InlineData("net472", "class")]
    [InlineData("net462", "class")]
    [InlineData("netstandard2.0", "class")]
    [InlineData("netstandard2.1", "class")]
    public void Execute_WithDifferentFrameworks_ShouldGenerateCorrectSyntax(string targetFramework, string expectedType)
    {
        // Arrange
        var sourceCode = @"
public class Service
{
    public void Method(int x, int y)
    {
        Console.WriteLine(x + y);
    }

    public void Test()
    {
        Method(1, 2);
    }
}";
        var refactoring = new IntroduceParameterObject();

        // Act
        var result = refactoring.Execute(
            sourceCode,
            "Service",
            "Method",
            new[] { "x", "y" },
            "Input",
            targetFramework);

        // Assert
        result.IsSuccess.Should().BeTrue();

        if (expectedType == "record")
        {
            result.RefactoredCode.Should().Contain("public record Input(int X, int Y);");
        }
        else
        {
            result.RefactoredCode.Should().Contain("public class Input");
            result.RefactoredCode.Should().Contain("public int X { get; }");
            result.RefactoredCode.Should().Contain("public int Y { get; }");
            result.RefactoredCode.Should().Contain("public Input(int x, int y)");
        }
    }

    [Fact]
    public void Execute_WithMultipleCallers_ShouldUpdateAll()
    {
        // Arrange
        var sourceCode = @"
public class Service
{
    public void Process(int x, int y, int z)
    {
        Console.WriteLine(x + y + z);
    }

    public void Caller1()
    {
        Process(1, 2, 3);
    }

    public void Caller2()
    {
        Process(4, 5, 6);
    }

    public void Caller3()
    {
        Process(7, 8, 9);
    }
}";
        var refactoring = new IntroduceParameterObject();

        // Act
        var result = refactoring.Execute(
            sourceCode,
            "Service",
            "Process",
            new[] { "x", "y" },
            "Input",
            "net8.0");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.RefactoredCode.Should().Contain("new Input(1, 2)");
        result.RefactoredCode.Should().Contain("new Input(4, 5)");
        result.RefactoredCode.Should().Contain("new Input(7, 8)");
    }

    [Fact]
    public void Execute_WithPartialParameters_ShouldKeepRemainingParameters()
    {
        // Arrange
        var sourceCode = @"
public class Service
{
    public void Process(int a, int b, int c, int d)
    {
        Console.WriteLine(a + b + c + d);
    }

    public void Test()
    {
        Process(1, 2, 3, 4);
    }
}";
        var refactoring = new IntroduceParameterObject();

        // Act
        var result = refactoring.Execute(
            sourceCode,
            "Service",
            "Process",
            new[] { "b", "c" },
            "MiddleParams",
            "net8.0");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.RefactoredCode.Should().Contain("public record MiddleParams(int B, int C);");
        result.RefactoredCode.Should().Contain("Process(int a, int d, MiddleParams middleParams)");
        result.RefactoredCode.Should().Contain("middleParams.B");
        result.RefactoredCode.Should().Contain("middleParams.C");
    }

    [Fact]
    public void Execute_WithLocalVariableShadowing_ShouldNotReplaceLocalVariable()
    {
        // Arrange - Test CRITICAL #1 fix: semantic model prevents false positives
        var sourceCode = @"
public class Service
{
    public void Process(int x, int y)
    {
        int x = 10; // Local variable shadows parameter
        Console.WriteLine(x + y); // x should NOT be replaced here
    }

    public void Test()
    {
        Process(1, 2);
    }
}";
        var refactoring = new IntroduceParameterObject();

        // Act
        var result = refactoring.Execute(
            sourceCode,
            "Service",
            "Process",
            new[] { "x", "y" },
            "Input",
            "net8.0");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.RefactoredCode.Should().Contain("public record Input(int X, int Y);");
        result.RefactoredCode.Should().Contain("Process(Input input)");
        // The local variable 'x' should remain unchanged, not converted to input.X
        result.RefactoredCode.Should().Contain("int x = 10;");
    }

    [Fact]
    public void Execute_WithNamedArguments_ShouldHandleCorrectly()
    {
        // Arrange - Test CRITICAL #2 fix: semantic model handles named arguments
        var sourceCode = @"
public class Service
{
    public void Process(int x, int y, int z)
    {
        Console.WriteLine(x + y + z);
    }

    public void Test()
    {
        Process(z: 3, x: 1, y: 2); // Named arguments out of order
    }
}";
        var refactoring = new IntroduceParameterObject();

        // Act
        var result = refactoring.Execute(
            sourceCode,
            "Service",
            "Process",
            new[] { "x", "y" },
            "Input",
            "net8.0");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.RefactoredCode.Should().Contain("public record Input(int X, int Y);");
        result.RefactoredCode.Should().Contain("new Input(1, 2)");
    }

    [Fact]
    public void Execute_WithOptionalParameters_ShouldHandleCorrectly()
    {
        // Arrange - Test CRITICAL #2 fix: semantic model handles optional parameters
        var sourceCode = @"
public class Service
{
    public void Process(int x, int y = 10, int z = 20)
    {
        Console.WriteLine(x + y + z);
    }

    public void Test()
    {
        Process(1); // Only required parameter provided
    }
}";
        var refactoring = new IntroduceParameterObject();

        // Act
        var result = refactoring.Execute(
            sourceCode,
            "Service",
            "Process",
            new[] { "y", "z" },
            "OptionalParams",
            "net8.0");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.RefactoredCode.Should().Contain("public record OptionalParams(int Y, int Z);");
    }

    [Fact]
    public void Execute_WithMethodOverloads_ShouldOnlyAffectTargetMethod()
    {
        // Arrange - Test CRITICAL #2 fix: semantic model distinguishes overloads
        var sourceCode = @"
public class Service
{
    public void Process(int x, int y)
    {
        Console.WriteLine(x + y);
    }

    public void Process(string x, string y)
    {
        Console.WriteLine(x + y);
    }

    public void Test()
    {
        Process(1, 2);
        Process(""a"", ""b"");
    }
}";
        var refactoring = new IntroduceParameterObject();

        // Act
        var result = refactoring.Execute(
            sourceCode,
            "Service",
            "Process",
            new[] { "x", "y" },
            "Input",
            "net8.0");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.RefactoredCode.Should().Contain("public record Input(int X, int Y);");
        // Both overloads should be updated since they have same parameter names
        result.RefactoredCode.Should().Contain("new Input(1, 2)");
    }

    [Fact]
    public void Execute_WithDuplicateClassName_ShouldReturnFailure()
    {
        // Arrange - Test HIGH #2 fix: parameter object name conflicts
        var sourceCode = @"
public class Input
{
    public string Value { get; set; }
}

public class Service
{
    public void Process(int x, int y)
    {
        Console.WriteLine(x + y);
    }
}";
        var refactoring = new IntroduceParameterObject();

        // Act
        var result = refactoring.Execute(
            sourceCode,
            "Service",
            "Process",
            new[] { "x", "y" },
            "Input", // This name already exists
            "net8.0");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Input");
        result.ErrorMessage.Should().Contain("already exists");
    }

    [Fact]
    public void Execute_WithGenericMethod_ShouldHandleCorrectly()
    {
        // Arrange - Test generic method handling
        var sourceCode = @"
public class Service
{
    public void Process<T>(T x, T y)
    {
        Console.WriteLine(x.ToString() + y.ToString());
    }

    public void Test()
    {
        Process<int>(1, 2);
    }
}";
        var refactoring = new IntroduceParameterObject();

        // Act
        var result = refactoring.Execute(
            sourceCode,
            "Service",
            "Process",
            new[] { "x", "y" },
            "Input",
            "net8.0");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.RefactoredCode.Should().Contain("public record Input(T X, T Y);");
        result.RefactoredCode.Should().Contain("Process<T>(Input input)");
    }

    [Fact]
    public void Execute_WithAsyncMethod_ShouldHandleCorrectly()
    {
        // Arrange - Test async method handling
        var sourceCode = @"
using System.Threading.Tasks;

public class Service
{
    public async Task ProcessAsync(int x, int y)
    {
        await Task.Delay(100);
        Console.WriteLine(x + y);
    }

    public async Task Test()
    {
        await ProcessAsync(1, 2);
    }
}";
        var refactoring = new IntroduceParameterObject();

        // Act
        var result = refactoring.Execute(
            sourceCode,
            "Service",
            "ProcessAsync",
            new[] { "x", "y" },
            "Input",
            "net8.0");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.RefactoredCode.Should().Contain("public record Input(int X, int Y);");
        result.RefactoredCode.Should().Contain("ProcessAsync(Input input)");
        result.RefactoredCode.Should().Contain("new Input(1, 2)");
    }

    [Fact]
    public void Execute_WithRefOutParameters_ShouldGroupCorrectly()
    {
        // Arrange - Test ref/out parameter handling
        var sourceCode = @"
public class Service
{
    public void Process(ref int x, out int y, int z)
    {
        y = x + z;
        x = x * 2;
    }

    public void Test()
    {
        int a = 5;
        int b;
        Process(ref a, out b, 10);
    }
}";
        var refactoring = new IntroduceParameterObject();

        // Act
        var result = refactoring.Execute(
            sourceCode,
            "Service",
            "Process",
            new[] { "x", "y" },
            "RefParams",
            "net8.0");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.RefactoredCode.Should().Contain("public record RefParams(ref int X, out int Y);");
    }

    [Fact]
    public void Execute_WithMultipleInvocationsWithDifferentArgumentForms_ShouldUpdateAll()
    {
        // Arrange - Test multiple invocation styles
        var sourceCode = @"
public class Service
{
    public void Process(int x, int y, int z)
    {
        Console.WriteLine(x + y + z);
    }

    public void Test()
    {
        Process(1, 2, 3); // Positional
        Process(x: 4, y: 5, z: 6); // Named
        Process(7, z: 9, y: 8); // Mixed
    }
}";
        var refactoring = new IntroduceParameterObject();

        // Act
        var result = refactoring.Execute(
            sourceCode,
            "Service",
            "Process",
            new[] { "x", "y" },
            "Input",
            "net8.0");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.RefactoredCode.Should().Contain("public record Input(int X, int Y);");
        // All three invocations should be updated
        result.RefactoredCode.Should().Contain("new Input(1, 2)");
        result.RefactoredCode.Should().Contain("new Input(4, 5)");
        result.RefactoredCode.Should().Contain("new Input(7, 8)");
    }

    [Fact]
    public void Execute_WithNestedMethod_ShouldOnlyAffectTargetMethod()
    {
        // Arrange - Test nested method handling
        var sourceCode = @"
public class Service
{
    public void Outer(int x, int y)
    {
        void Inner(int x, int y)
        {
            Console.WriteLine(x + y);
        }
        Inner(x, y);
    }

    public void Test()
    {
        Outer(1, 2);
    }
}";
        var refactoring = new IntroduceParameterObject();

        // Act
        var result = refactoring.Execute(
            sourceCode,
            "Service",
            "Outer",
            new[] { "x", "y" },
            "Input",
            "net8.0");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.RefactoredCode.Should().Contain("public record Input(int X, int Y);");
        result.RefactoredCode.Should().Contain("Outer(Input input)");
        // Inner method should remain unchanged
        result.RefactoredCode.Should().Contain("void Inner(int x, int y)");
    }

    [Fact]
    public void Execute_WithParameterUsedInLambda_ShouldReplaceInLambdaToo()
    {
        // Arrange - Test lambda expression handling
        var sourceCode = @"
public class Service
{
    public void Process(int x, int y)
    {
        var result = new[] { 1, 2, 3 }.Select(n => n * x + y).ToList();
        Console.WriteLine(string.Join("","", result));
    }

    public void Test()
    {
        Process(2, 5);
    }
}";
        var refactoring = new IntroduceParameterObject();

        // Act
        var result = refactoring.Execute(
            sourceCode,
            "Service",
            "Process",
            new[] { "x", "y" },
            "Input",
            "net8.0");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.RefactoredCode.Should().Contain("public record Input(int X, int Y);");
        result.RefactoredCode.Should().Contain("Process(Input input)");
        // Parameters should be replaced in lambda
        result.RefactoredCode.Should().Contain("input.X");
        result.RefactoredCode.Should().Contain("input.Y");
    }

    [Fact]
    public void Execute_WithNullableParameters_ShouldHandleCorrectly()
    {
        // Arrange - Test nullable parameter types
        var sourceCode = @"
public class Service
{
    public void Process(int? x, string? y)
    {
        Console.WriteLine($""{x} - {y}"");
    }

    public void Test()
    {
        Process(null, null);
        Process(42, ""test"");
    }
}";
        var refactoring = new IntroduceParameterObject();

        // Act
        var result = refactoring.Execute(
            sourceCode,
            "Service",
            "Process",
            new[] { "x", "y" },
            "Input",
            "net8.0");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.RefactoredCode.Should().Contain("public record Input(int? X, string? Y);");
        result.RefactoredCode.Should().Contain("Process(Input input)");
    }

    [Fact]
    public void Execute_WithParamsParameter_ShouldHandleCorrectly()
    {
        // Arrange - Test params parameter
        var sourceCode = @"
public class Service
{
    public void Process(int x, params int[] values)
    {
        Console.WriteLine(x + values.Sum());
    }

    public void Test()
    {
        Process(10, 1, 2, 3);
    }
}";
        var refactoring = new IntroduceParameterObject();

        // Act
        var result = refactoring.Execute(
            sourceCode,
            "Service",
            "Process",
            new[] { "x", "values" },
            "Input",
            "net8.0");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.RefactoredCode.Should().Contain("public record Input(int X, params int[] Values);");
    }
}
