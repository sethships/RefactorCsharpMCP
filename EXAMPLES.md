# RefactorCsharpMCP Examples

This document provides practical examples of using RefactorCsharpMCP's refactoring capabilities.

**Framework Version Awareness (v1.0+)**: All refactorings require the `targetFramework` parameter to ensure generated code is compatible with your .NET version. See [FRAMEWORK-SUPPORT.md](docs/FRAMEWORK-SUPPORT.md) for comprehensive framework documentation.

## Table of Contents

1. [Framework-Aware Refactoring](#framework-aware-refactoring)
2. [Extract Method](#extract-method)
3. [Constructor Injection](#constructor-injection)
4. [Introduce Parameter Object](#introduce-parameter-object)
5. [Make Field Readonly](#make-field-readonly)
6. [Safe Delete Method](#safe-delete-method)
7. [Inline Variable](#inline-variable)
8. [Remove Unused Usings](#remove-unused-usings)
9. [Extract Class](#extract-class)
10. [Inline Method](#inline-method-part-1)
11. [Rename Symbol](#rename-symbol)
12. [Fix Diagnostic](#fix-diagnostic)
13. [Analyze Code](#analyze-code)
14. [Framework Validation](#framework-aware-validation)
15. [Framework Limitations](#framework-limitations-and-workarounds)
16. [Diagnostic Integration](#diagnostic-integration-v15)

## Quick Reference: Tool Capabilities

| Tool | Scope | Framework-Aware | Position-Based | Primary Use Case |
|------|-------|-----------------|----------------|------------------|
| [Extract Method](#extract-method) | Single-File | ✅ | ❌ | Extract code into reusable methods |
| [Constructor Injection](#constructor-injection) | Single-File | ✅ | ❌ | Convert to dependency injection pattern |
| [Introduce Parameter Object](#introduce-parameter-object) | Single-File | ✅ | ❌ | Group related parameters into object |
| [Make Field Readonly](#make-field-readonly) | Single-File | ✅ | ❌ | Enforce immutability with readonly |
| [Safe Delete Method](#safe-delete-method) | Single-File | ✅ | ❌ | Delete unused methods safely |
| [Inline Variable](#inline-variable) | Single-File | ✅ | ✅ | Simplify by removing intermediates |
| [Remove Unused Usings](#remove-unused-usings) | Single-File | ✅ | ❌ | Clean up unused imports |
| [Extract Class](#extract-class) | Single-File | ✅ | ❌ | Split large classes (SRP) |
| [Inline Method](#inline-method-part-1) | Single-File | ✅ | ✅ | Remove single-use methods |
| [Rename Symbol](#rename-symbol) | Single-File | ✅ | ✅ | Rename variables/fields/methods |
| [Fix Diagnostic](#fix-diagnostic) | Single-File | ✅ | ❌ | Auto-fix compiler warnings |
| [Analyze Code](#analyze-code) | Single-File | ✅ | ❌ | Discover code quality issues |

**Legend:**
- **Scope**: Single-File (operates on one file at a time), Multi-File (future: cross-file refactoring)
- **Framework-Aware**: Adapts output to target .NET framework (net8.0, net48, netstandard2.0, etc.)
- **Position-Based**: Requires line/column coordinates to identify the target symbol

---

## Framework-Aware Refactoring

Starting with v1.0, all refactorings are framework-aware, ensuring generated code matches your target .NET framework's C# language version. This section demonstrates how refactorings adapt to different frameworks.

### Quick Reference: Framework → C# Version Mapping

| Framework | C# Version | Key Features |
|-----------|-----------|--------------|
| net9.0 | C# 13 | Latest features |
| net8.0 | C# 12 | Collection expressions, primary constructors |
| net48 | C# 7.3 | Tuples, pattern matching |
| netstandard2.0 | C# 7.3 | Same as net48 |
| net35 | C# 3.0 | LINQ, lambdas |

**See:** [docs/FRAMEWORK-SUPPORT.md](docs/FRAMEWORK-SUPPORT.md) for complete framework support documentation.

### Example 1: Extract Method - Framework Differences

Same refactoring produces different code based on target framework:

**Input Code:**
```csharp
public class DataProcessor
{
    public void Process()
    {
        var name = GetName();
        var age = GetAge();
        var email = GetEmail();
        SaveUser(name, age, email);
    }
}
```

**Targeting .NET 8 (C# 12) - Tuple Returns:**
```csharp
var refactoring = new ExtractMethod();
var result = await refactoring.ExecuteAsync(
    sourceCode,
    startLine: 3,
    endLine: 5,
    newMethodName: "GatherUserData",
    targetFramework: "net8.0"  // C# 12 supported
);

// Generated code uses tuples:
public class DataProcessor
{
    public void Process()
    {
        var (name, age, email) = GatherUserData();
        SaveUser(name, age, email);
    }

    private (string name, int age, string email) GatherUserData()
    {
        var name = GetName();
        var age = GetAge();
        var email = GetEmail();
        return (name, age, email);  // Tuple return
    }
}
```

**Targeting .NET Framework 3.5 (C# 3.0) - No Tuples:**
```csharp
var result = await refactoring.ExecuteAsync(
    sourceCode,
    startLine: 3,
    endLine: 5,
    newMethodName: "GatherUserData",
    targetFramework: "net35"  // C# 3.0 only
);

// Fails with error:
{
  "success": false,
  "errorCode": "UNSUPPORTED_LANGUAGE_FEATURE",
  "error": "Multiple return values require tuples (C# 7.0+). Target framework net35 uses C# 3.0.",
  "suggestion": "Extract single return value OR create custom return type OR upgrade to net472+"
}
```

**Workaround for .NET Framework 3.5:**
```csharp
// Extract each variable separately (3 separate extractions):
private string GatherName() { return GetName(); }
private int GatherAge() { return GetAge(); }
private string GatherEmail() { return GetEmail(); }

// Or manually create a return type:
public class UserData
{
    public string Name { get; set; }
    public int Age { get; set; }
    public string Email { get; set; }
}
```

### Example 2: Constructor Injection - Property Styles

**Input Code:**
```csharp
public class UserService
{
    public void CreateUser(ILogger logger, string username)
    {
        logger.Log("Creating: " + username);
    }
}
```

**Targeting .NET 8 (C# 12) - Read-Only Auto-Properties:**
```csharp
var refactoring = new ConstructorInjection();
var result = await refactoring.ExecuteAsync(
    sourceCode,
    className: "UserService",
    methodName: "CreateUser",
    parameterNames: new[] { "logger" },
    targetFramework: "net8.0",
    useProperties: true  // Use properties instead of fields
);

// Generated code with C# 6+ syntax:
public class UserService
{
    public ILogger Logger { get; }  // Read-only auto-property (C# 6)

    public UserService(ILogger logger)
    {
        Logger = logger;
    }

    public void CreateUser(string username)
    {
        Logger.Log("Creating: " + username);
    }
}
```

**Targeting .NET Framework 3.5 (C# 3.0) - Explicit Properties:**
```csharp
var result = await refactoring.ExecuteAsync(
    sourceCode,
    className: "UserService",
    methodName: "CreateUser",
    parameterNames: new[] { "logger" },
    targetFramework: "net35",
    useProperties: true
);

// Generated code with C# 3.0 syntax:
public class UserService
{
    private readonly ILogger _logger;

    public ILogger Logger  // C# 3.0 property with explicit getter
    {
        get { return _logger; }
    }

    public UserService(ILogger logger)
    {
        _logger = logger;
    }

    public void CreateUser(string username)
    {
        Logger.Log("Creating: " + username);
    }
}
```

### Example 3: Inline Variable - Collection Expressions

**Input Code (C# 12):**
```csharp
public class Calculator
{
    public void Process()
    {
        var numbers = [1, 2, 3];  // Collection expression (C# 12)
        var sum = numbers.Sum();
    }
}
```

**Targeting .NET 8 (C# 12) - Preserves Collection Expression:**
```csharp
var refactoring = new InlineVariable();
var result = await refactoring.ExecuteAsync(
    sourceCode,
    lineNumber: 3,
    columnNumber: 13,
    targetFramework: "net8.0"  // C# 12 supported
);

// Success - collection expression preserved:
public class Calculator
{
    public void Process()
    {
        var sum = [1, 2, 3].Sum();  // Inlined with collection expression
    }
}
```

**Targeting .NET Framework 4.8 (C# 7.3) - Input Validation Error:**
```csharp
var result = await refactoring.ExecuteAsync(
    sourceCode,
    lineNumber: 3,
    columnNumber: 13,
    targetFramework: "net48"  // C# 7.3 only
);

// Fails validation:
{
  "success": false,
  "errorCode": "INPUT_SYNTAX_MISMATCH",
  "error": "Input code contains C# 12 collection expressions, incompatible with net48 (C# 7.3).",
  "suggestion": "Rewrite input code using C# 7.3 syntax or target net8.0+"
}
```

**Solution - Rewrite Input for C# 7.3:**
```csharp
// Change input code to C# 7.3 compatible syntax:
public class Calculator
{
    public void Process()
    {
        var numbers = new[] { 1, 2, 3 };  // Array initializer (C# 3.0+)
        var sum = numbers.Sum();
    }
}

// Now refactoring succeeds on net48:
var result = await refactoring.ExecuteAsync(
    sourceCode,
    lineNumber: 3,
    columnNumber: 13,
    targetFramework: "net48"
);

// Refactored code:
public class Calculator
{
    public void Process()
    {
        var sum = new[] { 1, 2, 3 }.Sum();  // Inlined with array initializer
    }
}
```

### Example 4: Make Field Readonly - Universal Across Frameworks

Some refactorings work identically across all frameworks:

```csharp
public class Service
{
    private ILogger _logger;

    public Service(ILogger logger)
    {
        _logger = logger;
    }
}

// Works the same on ALL frameworks:
var refactoring = new MakeFieldReadonly();

// .NET 9
var result9 = await refactoring.ExecuteAsync(sourceCode, "Service", "_logger", "net9.0");

// .NET 8
var result8 = await refactoring.ExecuteAsync(sourceCode, "Service", "_logger", "net8.0");

// .NET Framework 4.8
var result48 = await refactoring.ExecuteAsync(sourceCode, "Service", "_logger", "net48");

// .NET Framework 3.5
var result35 = await refactoring.ExecuteAsync(sourceCode, "Service", "_logger", "net35");

// ALL produce identical output:
// private readonly ILogger _logger;
```

**Why?** The `readonly` keyword has been available since C# 1.0, so this refactoring is framework-independent.

### Best Practices for Framework-Aware Refactoring

1. **Match Your Project's TFM**: Always use the same `targetFramework` as your `.csproj` file:
   ```xml
   <TargetFramework>net8.0</TargetFramework>  <!-- Use "net8.0" -->
   ```

2. **Handle Validation Errors**: Check `result.IsSuccess` before using refactored code:
   ```csharp
   if (result.IsSuccess)
   {
       File.WriteAllText("output.cs", result.RefactoredCode);
   }
   else
   {
       Console.WriteLine($"Error: {result.ErrorMessage}");
       Console.WriteLine($"Suggestion: {result.SuggestedAction}");
   }
   ```

3. **Use Modern Frameworks When Possible**: .NET 8+ have the best refactoring support and reliability.

4. **Test Framework-Specific Behavior**: Run tests after refactoring to ensure compatibility with your target framework.

5. **Read Documentation**: See [FRAMEWORK-SUPPORT.md](docs/FRAMEWORK-SUPPORT.md) for:
   - Complete list of supported frameworks
   - Framework-specific limitations
   - Troubleshooting guide
   - Migration strategies

### Framework-Specific Limitations

**⚠️ .NET Framework 4.8 (Issue #75):**
Refactorings may fail due to reference assembly limitations. **Workaround:** Use `net8.0` for refactoring, then manually verify compatibility.

**⚠️ IDE Analyzer Limitations (Issue #72):**
`remove_unused_usings` may not detect all unused directives. **Workaround:** Use IDE-based tools for comprehensive using cleanup.

**See:** [Framework Limitations section](#framework-limitations-and-workarounds) for detailed examples and workarounds.

---

## Extract Method

### Example 1: Basic Code Extraction

**Before:**
```csharp
public class Calculator
{
    public void ProcessData()
    {
        var x = 10;
        var y = 20;
        var sum = x + y;
        Console.WriteLine($"Sum: {sum}");

        var data = LoadData();
        SaveData(data);
    }
}
```

**After (extracting lines 5-7):**
```csharp
public class Calculator
{
    public void ProcessData()
    {
        CalculateAndPrintSum();

        var data = LoadData();
        SaveData(data);
    }

    private void CalculateAndPrintSum()
    {
        var x = 10;
        var y = 20;
        var sum = x + y;
        Console.WriteLine($"Sum: {sum}");
    }
}
```

### Example 2: Complex Logic Extraction

**Before:**
```csharp
public class UserService
{
    public void RegisterUser(string email, string password)
    {
        // Validation logic
        if (string.IsNullOrEmpty(email))
            throw new ArgumentException("Email required");

        if (!email.Contains("@"))
            throw new ArgumentException("Invalid email");

        if (password.Length < 8)
            throw new ArgumentException("Password too short");

        // Registration logic
        var user = new User { Email = email, Password = password };
        _database.Save(user);
    }
}
```

**After (extracting validation logic):**
```csharp
public class UserService
{
    public void RegisterUser(string email, string password)
    {
        ValidateUserInput(email, password);

        // Registration logic
        var user = new User { Email = email, Password = password };
        _database.Save(user);
    }

    private void ValidateUserInput(string email, string password)
    {
        if (string.IsNullOrEmpty(email))
            throw new ArgumentException("Email required");

        if (!email.Contains("@"))
            throw new ArgumentException("Invalid email");

        if (password.Length < 8)
            throw new ArgumentException("Password too short");
    }
}
```

## Constructor Injection

The Constructor Injection refactoring converts method parameters into constructor-injected fields, following the dependency injection pattern. This helps decouple classes and improve testability.

**See also:** [Make Field Readonly](#make-field-readonly) to make injected dependencies immutable after construction.

### Example 1: Field Injection (Default)

**Before:**
```csharp
public class UserService
{
    public void CreateUser(ILogger logger, IConfig config, string username)
    {
        logger.Log("Creating user: " + username);
        var dbConnection = config.GetConnectionString();
        // ... user creation logic
    }
}
```

**After (injecting logger and config as fields):**
```csharp
public class UserService
{
    private readonly ILogger _logger;
    private readonly IConfig _config;

    public UserService(ILogger logger, IConfig config)
    {
        _logger = logger;
        _config = config;
    }

    public void CreateUser(string username)
    {
        _logger.Log("Creating user: " + username);
        var dbConnection = _config.GetConnectionString();
        // ... user creation logic
    }
}
```

### Example 2: Property Injection

**Before:**
```csharp
public class DataProcessor
{
    public void Process(ILogger logger, string data)
    {
        logger.Log($"Processing: {data}");
        // ... processing logic
    }
}
```

**After (injecting logger as property):**
```csharp
public class DataProcessor
{
    public ILogger Logger { get; }

    public DataProcessor(ILogger logger)
    {
        Logger = logger;
    }

    public void Process(string data)
    {
        Logger.Log($"Processing: {data}");
        // ... processing logic
    }
}
```

### Example 3: Multiple Parameters with Mixed Injection

**Before:**
```csharp
public class OrderService
{
    public void ProcessOrder(ILogger logger, IEmailService emailService,
                             IPaymentGateway paymentGateway, Order order)
    {
        logger.Log($"Processing order {order.Id}");

        var paymentResult = paymentGateway.ProcessPayment(order.Total);
        if (paymentResult.Success)
        {
            emailService.SendConfirmation(order.CustomerEmail);
        }
    }
}
```

**After (injecting logger, emailService, and paymentGateway):**
```csharp
public class OrderService
{
    private readonly ILogger _logger;
    private readonly IEmailService _emailService;
    private readonly IPaymentGateway _paymentGateway;

    public OrderService(ILogger logger, IEmailService emailService, IPaymentGateway paymentGateway)
    {
        _logger = logger;
        _emailService = emailService;
        _paymentGateway = paymentGateway;
    }

    public void ProcessOrder(Order order)
    {
        _logger.Log($"Processing order {order.Id}");

        var paymentResult = _paymentGateway.ProcessPayment(order.Total);
        if (paymentResult.Success)
        {
            _emailService.SendConfirmation(order.CustomerEmail);
        }
    }
}
```

## Introduce Parameter Object

The Introduce Parameter Object refactoring replaces a group of related parameters with a parameter object. This refactoring generates framework-aware parameter objects (records for .NET 8+, traditional classes for .NET Framework 4.8).

**Benefits:**
- Reduces parameter count for methods with many related parameters
- Groups related data together
- Makes method signatures more readable
- Easier to extend (add new fields to the parameter object instead of adding parameters)

### Example 1: Basic Parameter Grouping (.NET 8)

**Use Case:** Group address-related parameters into an AddressInfo parameter object

**Input Code:**
```csharp
public class CustomerService
{
    public void CreateCustomer(string name, string email, string street, string city, string zip)
    {
        Console.WriteLine($"Creating customer {name} at {street}, {city}, {zip}");
        // Send welcome email to {email}
    }

    public void TestMethod()
    {
        CreateCustomer("John Doe", "john@example.com", "123 Main St", "Springfield", "12345");
    }
}
```

**Tool Call:**
```json
{
  "tool": "introduce_parameter_object",
  "arguments": {
    "sourceCode": "...",
    "className": "CustomerService",
    "methodName": "CreateCustomer",
    "parameterNames": "street,city,zip",
    "newClassName": "AddressInfo",
    "targetFramework": "net8.0"
  }
}
```

**Output Code:**
```csharp
public record AddressInfo(string Street, string City, string Zip);

public class CustomerService
{
    public void CreateCustomer(string name, string email, AddressInfo addressInfo)
    {
        Console.WriteLine($"Creating customer {name} at {addressInfo.Street}, {addressInfo.City}, {addressInfo.Zip}");
        // Send welcome email to {email}
    }

    public void TestMethod()
    {
        CreateCustomer("John Doe", "john@example.com", new AddressInfo("123 Main St", "Springfield", "12345"));
    }
}
```

**Result:**
- ✅ Generated record with primary constructor (C# 9+)
- ✅ Method signature updated to accept AddressInfo
- ✅ Method body transformed to use addressInfo.Street, addressInfo.City, addressInfo.Zip
- ✅ Caller updated to create AddressInfo instance

### Example 2: Framework Differences (.NET Framework 4.8)

**Use Case:** Same refactoring but targeting .NET Framework 4.8 (generates traditional class instead of record)

**Tool Call:**
```json
{
  "tool": "introduce_parameter_object",
  "arguments": {
    "sourceCode": "...",
    "className": "CustomerService",
    "methodName": "CreateCustomer",
    "parameterNames": "street,city,zip",
    "newClassName": "AddressInfo",
    "targetFramework": "net48"
  }
}
```

**Output Code:**
```csharp
public class AddressInfo
{
    public string Street { get; }
    public string City { get; }
    public string Zip { get; }

    public AddressInfo(string street, string city, string zip)
    {
        Street = street;
        City = city;
        Zip = zip;
    }
}

public class CustomerService
{
    public void CreateCustomer(string name, string email, AddressInfo addressInfo)
    {
        Console.WriteLine($"Creating customer {name} at {addressInfo.Street}, {addressInfo.City}, {addressInfo.Zip}");
    }

    public void TestMethod()
    {
        CreateCustomer("John Doe", "john@example.com", new AddressInfo("123 Main St", "Springfield", "12345"));
    }
}
```

**Result:**
- ✅ Generated traditional class with readonly properties (C# 7.3)
- ✅ Constructor with parameter assignment
- ✅ Same behavior as .NET 8 version, but compatible with older frameworks

### Example 3: Partial Parameter Grouping

**Use Case:** Group only some parameters while keeping others

**Input Code:**
```csharp
public class PaymentService
{
    public void ProcessPayment(string customerId, decimal amount, string currency, string cardNumber, string cvv, string expiryDate)
    {
        Console.WriteLine($"Processing ${amount} {currency} for customer {customerId}");
        Console.WriteLine($"Card: {cardNumber}, CVV: {cvv}, Expiry: {expiryDate}");
    }

    public void Test()
    {
        ProcessPayment("CUST-123", 99.99m, "USD", "4111-1111-1111-1111", "123", "12/25");
    }
}
```

**Tool Call:**
```json
{
  "tool": "introduce_parameter_object",
  "arguments": {
    "sourceCode": "...",
    "className": "PaymentService",
    "methodName": "ProcessPayment",
    "parameterNames": "cardNumber,cvv,expiryDate",
    "newClassName": "PaymentCardInfo",
    "targetFramework": "net8.0"
  }
}
```

**Output Code:**
```csharp
public record PaymentCardInfo(string CardNumber, string Cvv, string ExpiryDate);

public class PaymentService
{
    public void ProcessPayment(string customerId, decimal amount, string currency, PaymentCardInfo paymentCardInfo)
    {
        Console.WriteLine($"Processing ${amount} {currency} for customer {customerId}");
        Console.WriteLine($"Card: {paymentCardInfo.CardNumber}, CVV: {paymentCardInfo.Cvv}, Expiry: {paymentCardInfo.ExpiryDate}");
    }

    public void Test()
    {
        ProcessPayment("CUST-123", 99.99m, "USD", new PaymentCardInfo("4111-1111-1111-1111", "123", "12/25"));
    }
}
```

**Result:**
- ✅ Grouped only card-related parameters
- ✅ Preserved other parameters (customerId, amount, currency)
- ✅ Multiple callers all updated automatically

## Make Field Readonly

The Make Field Readonly refactoring analyzes field assignments and adds the `readonly` modifier when fields are only assigned in constructors. This enforces immutability and prevents accidental modifications after object initialization.

**See also:** [Analyze Code](#analyze-code) to discover all fields in a class that can be made readonly (look for IDE0044 diagnostics).

### Example 1: Single Field - Basic Usage

**Use Case:** Make a configuration field readonly to prevent accidental modification

**Input Code:**
```csharp
public class EmailService
{
    private string _smtpServer;
    private int _port;

    public EmailService(string smtpServer, int port)
    {
        _smtpServer = smtpServer;
        _port = port;
    }

    public void SendEmail(string to, string subject)
    {
        // _smtpServer and _port are never modified after construction
        Console.WriteLine($"Sending via {_smtpServer}:{_port}");
    }
}
```

**Tool Call:**
```json
{
  "tool": "make_field_readonly",
  "arguments": {
    "sourceCode": "...",
    "className": "EmailService",
    "fieldName": "_smtpServer",
    "targetFramework": "net8.0"
  }
}
```

**Output Code:**
```csharp
public class EmailService
{
    private readonly string _smtpServer;
    private int _port;

    public EmailService(string smtpServer, int port)
    {
        _smtpServer = smtpServer;
        _port = port;
    }

    public void SendEmail(string to, string subject)
    {
        // _smtpServer is now readonly - compiler prevents modification
        Console.WriteLine($"Sending via {_smtpServer}:{_port}");
    }
}
```

**Explanation:** The `_smtpServer` field is only assigned in the constructor, so it's safe to add the `readonly` modifier. This prevents accidental modifications like `_smtpServer = "newserver"` elsewhere in the class.

### Example 2: Analyze All Fields

**Use Case:** Analyze an entire class to find all fields that can be made readonly

**Input Code:**
```csharp
public class UserService
{
    private ILogger _logger;
    private IDatabase _database;
    private string _tableName;
    private int _retryCount;

    public UserService(ILogger logger, IDatabase database)
    {
        _logger = logger;
        _database = database;
        _tableName = "Users";
        _retryCount = 3;
    }

    public void SaveUser(User user)
    {
        _logger.Log("Saving user");
        _database.Save(_tableName, user);
    }

    public void UpdateRetryCount(int newCount)
    {
        _retryCount = newCount;  // Modified outside constructor
    }
}
```

**Tool Call:**
```json
{
  "tool": "make_field_readonly",
  "arguments": {
    "sourceCode": "...",
    "className": "UserService",
    "targetFramework": "net8.0"
  }
}
```

**Output Code:**
```csharp
public class UserService
{
    private readonly ILogger _logger;
    private readonly IDatabase _database;
    private readonly string _tableName;
    private int _retryCount;  // Not readonly - modified in UpdateRetryCount

    public UserService(ILogger logger, IDatabase database)
    {
        _logger = logger;
        _database = database;
        _tableName = "Users";
        _retryCount = 3;
    }

    public void SaveUser(User user)
    {
        _logger.Log("Saving user");
        _database.Save(_tableName, user);
    }

    public void UpdateRetryCount(int newCount)
    {
        _retryCount = newCount;  // Modified outside constructor
    }
}
```

**Explanation:** When no specific field is provided, the refactoring analyzes all fields in the class. It makes `_logger`, `_database`, and `_tableName` readonly because they're only assigned in the constructor. The `_retryCount` field remains mutable because it's modified in `UpdateRetryCount`.

### Example 3: Field Cannot Be Made Readonly

**Use Case:** Attempting to make a field readonly when it's modified outside constructors

**Input Code:**
```csharp
public class Counter
{
    private int _count;

    public Counter()
    {
        _count = 0;
    }

    public void Increment()
    {
        _count++;  // Modified outside constructor
    }
}
```

**Tool Call:**
```json
{
  "tool": "make_field_readonly",
  "arguments": {
    "sourceCode": "...",
    "className": "Counter",
    "fieldName": "_count",
    "targetFramework": "net8.0"
  }
}
```

**Result:**
```json
{
  "success": false,
  "message": "Field '_count' is assigned outside of constructors and cannot be made readonly",
  "error": "Field has assignments outside constructors"
}
```

**Explanation:** The refactoring detects that `_count` is modified in the `Increment()` method, so it cannot be made readonly. The `readonly` modifier only allows assignments in constructors or field initializers.

### Example 4: Framework-Independent Behavior

**Use Case:** Readonly modifier works identically across all .NET frameworks

**Input Code:**
```csharp
public class Configuration
{
    private string _apiKey;

    public Configuration(string apiKey)
    {
        _apiKey = apiKey;
    }
}
```

**Works on ALL frameworks:**
```csharp
// .NET 9
var result = await refactoring.ExecuteAsync(code, "Configuration", "_apiKey", "net9.0");

// .NET 8
var result = await refactoring.ExecuteAsync(code, "Configuration", "_apiKey", "net8.0");

// .NET Framework 4.8
var result = await refactoring.ExecuteAsync(code, "Configuration", "_apiKey", "net48");

// .NET Framework 3.5
var result = await refactoring.ExecuteAsync(code, "Configuration", "_apiKey", "net35");
```

**Output (identical across all frameworks):**
```csharp
public class Configuration
{
    private readonly string _apiKey;

    public Configuration(string apiKey)
    {
        _apiKey = apiKey;
    }
}
```

**Explanation:** The `readonly` keyword has been available since C# 1.0, so this refactoring produces identical results across all .NET framework versions.

### MCP Tool Usage

```javascript
// Make a specific field readonly with error handling
try {
  const result = await use_mcp_tool({
    server_name: "refactor-csharp-mcp",
    tool_name: "make_field_readonly",
    arguments: {
      sourceCode: "...",
      className: "MyClass",
      fieldName: "_myField",
      targetFramework: "net8.0"
    }
  });

  if (result.success) {
    console.log("Field made readonly successfully");
    console.log(result.refactoredCode);
  } else {
    console.error("Refactoring failed:", result.message);
    // Handle specific error cases
    if (result.error === "Field has assignments outside constructors") {
      console.log("Field cannot be made readonly - it's modified after construction");
    }
  }
} catch (error) {
  console.error("MCP tool error:", error);
  // Handle network errors, authentication issues, etc.
}

// Analyze all fields in a class
const result = await use_mcp_tool({
  server_name: "refactor-csharp-mcp",
  tool_name: "make_field_readonly",
  arguments: {
    sourceCode: "...",
    className: "MyClass",
    targetFramework: "net8.0"
    // fieldName omitted - analyzes all fields
  }
});
```

### Best Practices

1. **Run on entire classes** - Omit the `fieldName` parameter to analyze all fields at once
2. **Use with constructor injection** - After applying constructor injection, use this refactoring to make injected dependencies readonly
3. **Enforce immutability** - Readonly fields help prevent bugs caused by accidental state mutations
4. **Framework-independent** - Works identically on all .NET frameworks since C# 1.0
5. **Safe refactoring** - The tool validates that fields are only assigned in constructors before adding `readonly`

## Safe Delete Method

The Safe Delete Method refactoring safely removes methods after verifying they have no references within the codebase. This prevents breaking changes by ensuring deleted methods aren't called elsewhere.

### Example 1: Delete Unused Method

**Use Case:** Remove a method that's no longer used after refactoring

**Input Code:**
```csharp
public class OrderService
{
    public void ProcessOrder(Order order)
    {
        ValidateOrder(order);
        SaveOrder(order);
    }

    private void ValidateOrder(Order order)
    {
        if (order == null)
            throw new ArgumentNullException(nameof(order));
    }

    private void SaveOrder(Order order)
    {
        // Save logic
    }

    private void LogOrder(Order order)
    {
        // This method is no longer used
        Console.WriteLine($"Order: {order.Id}");
    }
}
```

**Tool Call:**
```json
{
  "tool": "safe_delete_method",
  "arguments": {
    "sourceCode": "...",
    "className": "OrderService",
    "methodName": "LogOrder",
    "targetFramework": "net8.0"
  }
}
```

**Output Code:**
```csharp
public class OrderService
{
    public void ProcessOrder(Order order)
    {
        ValidateOrder(order);
        SaveOrder(order);
    }

    private void ValidateOrder(Order order)
    {
        if (order == null)
            throw new ArgumentNullException(nameof(order));
    }

    private void SaveOrder(Order order)
    {
        // Save logic
    }
}
```

**Explanation:** The `LogOrder` method has no references within the class, so it's safe to delete. The refactoring removes the entire method declaration.

### Example 2: Cannot Delete - Method Has References

**Use Case:** Attempt to delete a method that's still being called

**Input Code:**
```csharp
public class Calculator
{
    public int Calculate(int a, int b)
    {
        var sum = Add(a, b);
        return sum * 2;
    }

    private int Add(int a, int b)
    {
        return a + b;
    }
}
```

**Tool Call:**
```json
{
  "tool": "safe_delete_method",
  "arguments": {
    "sourceCode": "...",
    "className": "Calculator",
    "methodName": "Add",
    "targetFramework": "net8.0"
  }
}
```

**Result:**
```json
{
  "success": false,
  "message": "Method 'Add' has 1 reference(s) and cannot be safely deleted",
  "error": "Method has references",
  "references": [
    {
      "location": "Line 5, Column 19",
      "context": "var sum = Add(a, b);"
    }
  ]
}
```

**Explanation:** The `Add` method is called on line 5, so it cannot be safely deleted. The refactoring returns an error with details about where the method is referenced.

### Example 3: Delete Overloaded Method

**Use Case:** Delete one overload while keeping others

**Input Code:**
```csharp
public class Logger
{
    public void Log(string message)
    {
        Console.WriteLine(message);
    }

    public void Log(string message, LogLevel level)
    {
        Console.WriteLine($"[{level}] {message}");
    }

    public void Log(string message, LogLevel level, Exception ex)
    {
        // This overload is unused
        Console.WriteLine($"[{level}] {message}: {ex.Message}");
    }

    public void WriteLog()
    {
        Log("Test");
        Log("Warning", LogLevel.Warn);
    }
}
```

**Tool Call:**
```json
{
  "tool": "safe_delete_method",
  "arguments": {
    "sourceCode": "...",
    "className": "Logger",
    "methodName": "Log",
    "targetFramework": "net8.0"
  }
}
```

**Result:**
```json
{
  "success": false,
  "message": "Method 'Log' has 2 reference(s) and cannot be safely deleted",
  "error": "Method has references"
}
```

**Explanation:** When overloaded methods exist, the tool matches by name only and cannot distinguish between overloads. It finds the first method named 'Log' and counts ALL references to any method with that name (2 in this case: both the single-parameter and two-parameter overloads are called). For overloaded methods, you must manually delete the unused overload or rename methods to have unique names before using safe_delete_method.

**Note**: The tool does not currently support deleting specific method overloads by signature. If you have overloaded methods, delete them manually in your IDE.

### Example 4: Delete Private Helper Method

**Use Case:** Clean up unused private helper methods after refactoring

**Input Code:**
```csharp
public class DataProcessor
{
    public string ProcessData(string input)
    {
        var cleaned = CleanData(input);
        return cleaned.ToUpper();
    }

    private string CleanData(string data)
    {
        return data.Trim();
    }

    private string NormalizeData(string data)
    {
        // This helper method was used before refactoring
        return data.Replace("  ", " ");
    }

    private bool ValidateLength(string data)
    {
        // Another unused helper
        return data.Length > 0;
    }
}
```

**Tool Call (Delete first unused method):**
```json
{
  "tool": "safe_delete_method",
  "arguments": {
    "sourceCode": "...",
    "className": "DataProcessor",
    "methodName": "NormalizeData",
    "targetFramework": "net8.0"
  }
}
```

**After first deletion, delete second unused method:**
```json
{
  "tool": "safe_delete_method",
  "arguments": {
    "sourceCode": "...",
    "className": "DataProcessor",
    "methodName": "ValidateLength",
    "targetFramework": "net8.0"
  }
}
```

**Final Output Code:**
```csharp
public class DataProcessor
{
    public string ProcessData(string input)
    {
        var cleaned = CleanData(input);
        return cleaned.ToUpper();
    }

    private string CleanData(string data)
    {
        return data.Trim();
    }
}
```

**Explanation:** Both `NormalizeData` and `ValidateLength` have no references, so they can be safely deleted. The `CleanData` method remains because it's called in `ProcessData`.

### MCP Tool Usage

```javascript
// Delete a method
const result = await use_mcp_tool({
  server_name: "refactor-csharp-mcp",
  tool_name: "safe_delete_method",
  arguments: {
    sourceCode: "...",
    className: "MyClass",
    methodName: "UnusedMethod",
    targetFramework: "net8.0"
  }
});

// Check result
if (result.success) {
  console.log("Method deleted successfully");
} else {
  console.log(`Cannot delete: ${result.message}`);
  if (result.references) {
    console.log("References found at:");
    result.references.forEach(ref => {
      console.log(`  - ${ref.location}: ${ref.context}`);
    });
  }
}
```

### Best Practices

1. **Use after refactoring** - Run this after extract method or inline method to clean up unused methods
2. **Check references first** - The tool automatically validates no references exist before deletion
3. **One method at a time** - Delete methods one at a time to avoid cascading deletions
4. **Review overloads** - Be careful with overloaded methods - the tool may report ambiguity
5. **Framework-independent** - Method deletion works identically across all .NET frameworks
6. **Safe operation** - The tool prevents deletion if any references are found, avoiding breaking changes

## Inline Variable

The Inline Variable refactoring replaces all uses of a local variable with its initialization expression, then removes the variable declaration. This helps simplify code by eliminating unnecessary intermediate variables. Maps to Roslyn diagnostics IDE0059 (unnecessary value assignment) and IDE0058 (expression value never used).

### Example 1: Simple Literal Inlining

**Before:**
```csharp
public class Calculator
{
    public int Calculate()
    {
        var multiplier = 5;
        return GetValue() * multiplier;
    }
}
```

**After (inlining variable at line 5, column 13):**
```csharp
public class Calculator
{
    public int Calculate()
    {
        return GetValue() * 5;
    }
}
```

### Example 2: Method Call Inlining

**Before:**
```csharp
public class DataProcessor
{
    public void ProcessData(string inputPath)
    {
        var data = LoadFromFile(inputPath);
        Transform(data);
        SaveResults(data);
    }
}
```

**After (inlining 'data' at line 5, column 13):**
```csharp
public class DataProcessor
{
    public void ProcessData(string inputPath)
    {
        Transform(LoadFromFile(inputPath));
        SaveResults(LoadFromFile(inputPath));
    }
}
```

**Note:** The refactoring inlines all uses, which may duplicate method calls. Consider performance implications when inlining expensive operations.

### Example 3: Expression with Operator Precedence

**Before:**
```csharp
public class MathOperations
{
    public int Calculate(int a, int b)
    {
        var sum = a + b;
        return sum * 2;
    }
}
```

**After (inlining 'sum' at line 5, column 13):**
```csharp
public class MathOperations
{
    public int Calculate(int a, int b)
    {
        return (a + b) * 2;
    }
}
```

**Note:** The refactoring automatically adds parentheses to preserve operator precedence.

### Example 4: Multiple References

**Before:**
```csharp
public class Logger
{
    public void LogMessage(string message)
    {
        var timestamp = DateTime.Now;
        Console.WriteLine($"[{timestamp}] {message}");
        Console.WriteLine($"[{timestamp}] Logged successfully");
    }
}
```

**After (inlining 'timestamp' at line 5, column 13):**
```csharp
public class Logger
{
    public void LogMessage(string message)
    {
        Console.WriteLine($"[{DateTime.Now}] {message}");
        Console.WriteLine($"[{DateTime.Now}] Logged successfully");
    }
}
```

**Note:** All references are replaced, which may cause behavioral changes if the expression has side effects or changing values (like `DateTime.Now`).

### Limitations

The inline variable refactoring has specific safety checks to prevent incorrect transformations:

1. **Cannot inline uninitialized variables:**
   ```csharp
   int x;
   x = 5;  // Error: Variable has no initializer
   ```

2. **Cannot inline variables with multiple assignments:**
   ```csharp
   var x = 1;
   x = 2;  // Error: Variable assigned after declaration
   ```

3. **Cannot inline variables with increment/decrement operators:**
   ```csharp
   var count = 0;
   count++;  // Error: Variable modified with increment/decrement
   ```

4. **Cannot inline variables captured by lambdas (V1 limitation):**
   ```csharp
   var value = 10;
   Action a = () => Console.WriteLine(value);  // Error: Lambda capture not supported
   ```

5. **Cannot inline parameters or fields:**
   - Only local variables with initializers can be inlined
   - Method parameters and class fields are not supported

### MCP Tool Usage

```javascript
// Inline variable at specific location
const result = await use_mcp_tool({
  server_name: "refactor-csharp-mcp",
  tool_name: "inline_variable",
  arguments: {
    sourceCode: "...",
    lineNumber: 5,      // 1-based line where variable is declared
    columnNumber: 13,   // 1-based column within the line
    targetFramework: "net8.0"
  }
});
```

### Best Practices

1. **Avoid inlining expensive operations** that are used multiple times, as this will duplicate the computation.
2. **Be careful with side effects** - inlining expressions with side effects (like `DateTime.Now`, `Random.Next()`) may change behavior.
3. **Use for single-use variables** that don't add semantic value to the code.
4. **Review operator precedence** - while parentheses are added automatically, verify the result is semantically correct.
5. **Consider readability trade-offs** - sometimes a well-named intermediate variable improves code clarity even if it's technically unnecessary.

## Remove Unused Usings

The Remove Unused Usings refactoring analyzes using directives and removes those that aren't referenced in the code. It detects unused namespaces using Roslyn diagnostics (IDE0005, CS8019) and preserves framework-specific global usings introduced in C# 10.

### Example 1: Remove Simple Unused Usings

**Use Case:** Clean up unused namespace imports after refactoring

**Input Code:**
```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

public class Calculator
{
    public int Add(int a, int b)
    {
        Console.WriteLine($"Adding {a} + {b}");
        return a + b;
    }
}
```

**Tool Call:**
```json
{
  "tool": "remove_unused_usings",
  "arguments": {
    "sourceCode": "...",
    "targetFramework": "net8.0"
  }
}
```

**Output Code:**
```csharp
using System;

public class Calculator
{
    public int Add(int a, int b)
    {
        Console.WriteLine($"Adding {a} + {b}");
        return a + b;
    }
}
```

**Explanation:** Only `System` is used (for `Console.WriteLine`). The refactoring removes `System.Collections.Generic`, `System.Linq`, `System.Text`, and `System.Threading.Tasks` because they're not referenced.

### Example 2: Preserve Required Usings

**Use Case:** Keep only the usings that are actually needed

**Input Code:**
```csharp
using System;
using System.Collections.Generic;
using System.Linq;

public class DataProcessor
{
    public List<int> FilterData(int[] numbers)
    {
        return numbers.Where(n => n > 0).ToList();
    }
}
```

**Tool Call:**
```json
{
  "tool": "remove_unused_usings",
  "arguments": {
    "sourceCode": "...",
    "targetFramework": "net8.0"
  }
}
```

**Output Code:**
```csharp
using System.Collections.Generic;
using System.Linq;

public class DataProcessor
{
    public List<int> FilterData(int[] numbers)
    {
        return numbers.Where(n => n > 0).ToList();
    }
}
```

**Explanation:** The refactoring keeps `System.Collections.Generic` (for `List<T>`) and `System.Linq` (for `Where` and `ToList` extension methods), but removes `System` because it's not used.

### Example 3: Framework-Aware Global Using Preservation (C# 10+)

**Use Case:** Preserve global usings in modern .NET projects

**Input Code (.NET 6+):**
```csharp
// Global usings are implicitly included by the SDK
using System.Collections.Generic;
using System.Text;

public class Service
{
    public void Process()
    {
        Console.WriteLine("Processing");  // Uses implicit global using System
    }
}
```

**Tool Call:**
```json
{
  "tool": "remove_unused_usings",
  "arguments": {
    "sourceCode": "...",
    "targetFramework": "net8.0"
  }
}
```

**Output Code:**
```csharp
public class Service
{
    public void Process()
    {
        Console.WriteLine("Processing");
    }
}
```

**Explanation:** In .NET 6+, the SDK includes global usings for common namespaces like `System`. The refactoring removes explicit usings that are covered by global usings, and removes `System.Collections.Generic` and `System.Text` because they're not used.

### Example 4: Handle Aliases and Static Usings

**Use Case:** Preserve using aliases and static usings when they're used

**Input Code:**
```csharp
using System;
using System.Collections.Generic;
using static System.Math;
using StringList = System.Collections.Generic.List<string>;

public class Calculator
{
    public double CalculateCircle(double radius)
    {
        return PI * Pow(radius, 2);  // Uses static Math members
    }

    public StringList GetNames()
    {
        return new StringList { "Alice", "Bob" };  // Uses alias
    }
}
```

**Tool Call:**
```json
{
  "tool": "remove_unused_usings",
  "arguments": {
    "sourceCode": "...",
    "targetFramework": "net8.0"
  }
}
```

**Output Code:**
```csharp
using static System.Math;
using StringList = System.Collections.Generic.List<string>;

public class Calculator
{
    public double CalculateCircle(double radius)
    {
        return PI * Pow(radius, 2);
    }

    public StringList GetNames()
    {
        return new StringList { "Alice", "Bob" };
    }
}
```

**Explanation:** The refactoring preserves `using static System.Math` (for `PI` and `Pow`) and the `StringList` alias. It removes `System` and `System.Collections.Generic` as regular usings because they're either unused or covered by the alias.

### Example 5: No Unused Usings Detected

**Use Case:** All usings are required

**Input Code:**
```csharp
using System;
using System.Collections.Generic;

public class Logger
{
    private List<string> _messages = new List<string>();

    public void Log(string message)
    {
        _messages.Add($"{DateTime.Now}: {message}");
    }
}
```

**Tool Call:**
```json
{
  "tool": "remove_unused_usings",
  "arguments": {
    "sourceCode": "...",
    "targetFramework": "net8.0"
  }
}
```

**Result:**
```json
{
  "success": false,
  "message": "No unused using directives detected",
  "error": "All usings are required"
}
```

**Explanation:** Both `System` (for `DateTime`) and `System.Collections.Generic` (for `List<T>`) are used, so no usings can be removed. The refactoring returns a failure indicating all usings are necessary.

### Example 6: Framework-Specific Behavior (.NET Framework 4.8)

**Use Case:** Remove unused usings in older .NET Framework projects

**Input Code:**
```csharp
using System;
using System.Collections.Generic;
using System.Linq;

public class Processor
{
    public void Run()
    {
        Console.WriteLine("Running");
    }
}
```

**Tool Call:**
```json
{
  "tool": "remove_unused_usings",
  "arguments": {
    "sourceCode": "...",
    "targetFramework": "net48"
  }
}
```

**Output Code:**
```csharp
using System;

public class Processor
{
    public void Run()
    {
        Console.WriteLine("Running");
    }
}
```

**Explanation:** .NET Framework 4.8 doesn't have global usings, so all namespaces must be explicitly declared. The refactoring removes `System.Collections.Generic` and `System.Linq` but keeps `System` for `Console`.

### MCP Tool Usage

```javascript
// Remove unused usings
const result = await use_mcp_tool({
  server_name: "refactor-csharp-mcp",
  tool_name: "remove_unused_usings",
  arguments: {
    sourceCode: "...",
    targetFramework: "net8.0"
  }
});

// Check result
if (result.success) {
  console.log("Removed unused usings");
  console.log(result.refactoredCode);
} else {
  console.log(`No changes: ${result.message}`);
}
```

### Limitations (Issue #72)

**⚠️ IDE Analyzer Limitations:**

The `remove_unused_usings` refactoring relies on Roslyn compiler APIs rather than full IDE workspace APIs. This means:

1. **May not detect all unused usings** - Some unused directives might not be identified
2. **Best for obvious cases** - Works well for clearly unused namespaces
3. **Use IDE tools for comprehensive cleanup** - Visual Studio, VS Code with C# extension, or Rider have better detection

**Recommended Workflow:**
```csharp
// 1. Use remove_unused_usings for initial cleanup
var result = await refactoring.ExecuteAsync(code, "net8.0");

// 2. Use IDE-based tools for final verification
// - Visual Studio: Right-click → Remove and Sort Usings
// - VS Code: C# extension provides code actions
// - Rider: Code → Optimize Imports

// 3. Enable build-time warnings in .csproj
<PropertyGroup>
  <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
</PropertyGroup>
```

### Best Practices

1. **Run after refactoring** - Use this after extract class, inline method, or other refactorings that may leave unused usings
2. **Framework awareness** - Specify the correct target framework to handle global usings properly
3. **IDE verification** - Use IDE-based tools for final cleanup and verification
4. **Build warnings** - Enable CS8019 warnings in your project to catch unused usings during build
5. **Preserve aliases** - The tool automatically preserves using aliases and static usings when used
6. **Global usings** - On .NET 6+, the tool respects implicit global usings from the SDK

## Extract Class

Extract Class refactoring helps decompose large classes by moving fields and methods into a new class, following the composition pattern. The refactoring automatically updates references within the same class and warns about external references that need manual updates.

**See also:** [Rename Symbol](#rename-symbol) to rename the composition field or extracted class after extraction.

### Basic Usage - Single Field

**Before:**
```csharp
public class UserService
{
    private string _username;
    private string _email;
    private IDatabase _database;

    public void SaveUser()
    {
        _database.Save(_username, _email);
    }
}
```

**After extraction** of `_username` and `_email` into `UserProfile`:
```csharp
public class UserService
{
    private readonly UserProfile _userProfile = new UserProfile();
    private IDatabase _database;

    public void SaveUser()
    {
        // References automatically updated
        _database.Save(_userProfile._username, _userProfile._email);
    }
}

public class UserProfile
{
    private string _username;
    private string _email;
}
```

### Multiple Fields and Methods

**Before:**
```csharp
public class OrderService
{
    private string _street;
    private string _city;
    private string _state;
    private string _zipCode;

    private string FormatAddress()
    {
        return $"{_street}, {_city}, {_state} {_zipCode}";
    }

    private bool ValidateAddress()
    {
        return !string.IsNullOrEmpty(_street) && !string.IsNullOrEmpty(_city);
    }

    public void PrintOrder()
    {
        var address = FormatAddress();
        System.Console.WriteLine($"Shipping to: {address}");
    }
}
```

**After extraction** of address-related fields and methods into `Address`:
```csharp
public class OrderService
{
    private readonly Address _address = new Address();

    public void PrintOrder()
    {
        // Method call automatically updated
        var address = _address.FormatAddress();
        System.Console.WriteLine($"Shipping to: {address}");
    }
}

public class Address
{
    private string _street;
    private string _city;
    private string _state;
    private string _zipCode;

    private string FormatAddress()
    {
        return $"{_street}, {_city}, {_state} {_zipCode}";
    }

    private bool ValidateAddress()
    {
        return !string.IsNullOrEmpty(_street) && !string.IsNullOrEmpty(_city);
    }
}
```

### Automatic Reference Updating

The refactoring uses semantic analysis to automatically update references within the same class:

**Field references:**
- `_city` → `_address._city`
- `this._city` → `_address._city`

**Method calls:**
- `FormatAddress()` → `_address.FormatAddress()`

**Preserved correctly:**
- Local variables with same name remain unchanged
- Method parameters with same name remain unchanged
- Fields in unrelated classes remain unchanged

### External Reference Warnings

When extracted members are referenced from other classes, the refactoring warns you:

**Example:**
```csharp
public class UserService
{
    internal string _city; // Extracted to Address class
}

public class ReportGenerator
{
    private UserService _userService;

    public void PrintReport()
    {
        // ⚠️ External reference - needs manual update
        System.Console.WriteLine(_userService._city);
    }
}
```

**Result message:**
```
Extracted 1 field(s) and 0 method(s) into new class 'Address'.
⚠️ WARNING: Found external references that require manual updates:
ReportGenerator.cs (1 reference(s)).
```

You must manually update external references:
```csharp
System.Console.WriteLine(_userService._address._city);
```

### Service Class Extraction (Methods-Only)

Starting with V1.3, `extract_class` supports extracting methods without any fields - perfect for creating service classes that encapsulate related logic without state.

**Before:**
```csharp
public class InlineMethod
{
    private ILogger _logger;
    private string _sourceCode;

    private MethodInfo? ExtractMethodInfo(string code)
    {
        _logger?.LogInformation("Extracting method info");
        // Complex extraction logic
        return new MethodInfo();
    }

    private ValidationResult CanMethodBeInlined(MethodInfo method)
    {
        _logger?.LogInformation("Validating method");
        // Validation logic
        return new ValidationResult { IsValid = true };
    }

    private bool IsRecursive(MethodInfo method)
    {
        // Recursion check logic
        return false;
    }

    private bool IsSimpleType(string typeName)
    {
        return typeName == "int" || typeName == "string";
    }

    public void InlineTheMethod()
    {
        var methodInfo = ExtractMethodInfo(_sourceCode);
        if (methodInfo != null)
        {
            var validation = CanMethodBeInlined(methodInfo);
            var recursive = IsRecursive(methodInfo);
            var simple = IsSimpleType("int");
        }
    }
}
```

**After extraction** of methods into `MethodResolver` service class:
```csharp
public class InlineMethod
{
    private ILogger _logger;
    private string _sourceCode;
    private readonly MethodResolver _methodResolver = new MethodResolver();

    public void InlineTheMethod()
    {
        var methodInfo = _methodResolver.ExtractMethodInfo(_sourceCode);
        if (methodInfo != null)
        {
            var validation = _methodResolver.CanMethodBeInlined(methodInfo);
            var recursive = _methodResolver.IsRecursive(methodInfo);
            var simple = _methodResolver.IsSimpleType("int");
        }
    }
}

public class MethodResolver
{
    private MethodInfo? ExtractMethodInfo(string code)
    {
        // Complex extraction logic
        return new MethodInfo();
    }

    private ValidationResult CanMethodBeInlined(MethodInfo method)
    {
        // Validation logic
        return new ValidationResult { IsValid = true };
    }

    private bool IsRecursive(MethodInfo method)
    {
        // Recursion check logic
        return false;
    }

    private bool IsSimpleType(string typeName)
    {
        return typeName == "int" || typeName == "string";
    }
}
```

**MCP Tool Usage:**
```javascript
// Extract methods only (no fields)
const result = await use_mcp_tool({
  server_name: "refactor-csharp-mcp",
  tool_name: "extract_class",
  arguments: {
    sourceCode: "...",
    className: "InlineMethod",
    newClassName: "MethodResolver",
    fieldNames: null,  // No fields to extract
    methodNames: "ExtractMethodInfo,CanMethodBeInlined,IsRecursive,IsSimpleType"
  }
});
```

**Key Features:**
- No fields required - extract pure service classes
- Automatic composition field creation
- Method calls automatically updated within same class
- Perfect for decomposing large classes into smaller, focused services

### Encapsulation Through Internal Visibility

Extract Class automatically creates extracted classes and methods with `internal` visibility to enforce proper encapsulation through the composition pattern. This design prevents external code from directly accessing implementation details.

**Design Rationale:**
- **Encapsulation**: Extracted classes are implementation details of the original class
- **Composition Pattern**: Access should always go through the composition field (`_extractedClass._field`)
- **Flexibility**: You can manually change visibility to `public` after extraction if needed

**Example - Internal Class Visibility:**
```csharp
// Before extraction
public class UserService
{
    private string _username;
    private IDatabase _database;

    public void SaveUser()
    {
        _database.Save(_username);
    }
}

// After extracting _username into UserProfile
public class UserService
{
    private readonly UserProfile _userProfile = new UserProfile();
    private IDatabase _database;

    public void SaveUser()
    {
        _database.Save(_userProfile._username);
    }
}

// Extracted class is internal (not public)
internal class UserProfile
{
    internal string _username;
}
```

**Example - Internal Method Accessibility:**
```csharp
// Before extraction
public class OrderService
{
    private decimal _total;

    private decimal CalculateTax()
    {
        return _total * 0.08m;
    }

    private decimal CalculateShipping()
    {
        return _total > 100 ? 0 : 10;
    }

    public decimal GetFinalTotal()
    {
        return _total + CalculateTax() + CalculateShipping();
    }
}

// After extracting calculation methods
public class OrderService
{
    private decimal _total;
    private readonly PricingCalculator _pricingCalculator = new PricingCalculator();

    public decimal GetFinalTotal()
    {
        return _total + _pricingCalculator.CalculateTax() + _pricingCalculator.CalculateShipping();
    }
}

// Extracted methods are internal (not public)
internal class PricingCalculator
{
    internal decimal CalculateTax()
    {
        // Tax calculation logic
    }

    internal decimal CalculateShipping()
    {
        // Shipping calculation logic
    }
}
```

**When to Make Extracted Classes Public:**

You may want to manually change visibility after extraction in these cases:

1. **Shared Services**: When the extracted class should be used by other classes:
   ```csharp
   // Change from internal to public after extraction
   public class ValidationRules
   {
       public bool IsValid(string input) { /* ... */ }
   }
   ```

2. **API Surface**: When the extracted class is part of your public API:
   ```csharp
   // Change from internal to public for API consumers
   public class Configuration
   {
       public string ApiKey { get; set; }
   }
   ```

3. **Testing**: When you need to unit test the extracted class directly:
   ```csharp
   // Change from internal to public for testing
   // Or use InternalsVisibleTo attribute
   public class CalculationEngine { /* ... */ }
   ```

**Note**: The refactoring always generates `internal` visibility as the safe default. You can manually change to `public` after reviewing the extracted class if needed for your specific use case.

### Protected Methods and Inheritance Patterns

**Important**: Protected methods become `internal` during extraction, which breaks inheritance patterns. If you're working with class hierarchies that rely on protected members, carefully consider whether Extract Class is the right refactoring.

**Issue**: When extracting protected methods, they become `internal` in the new class, which prevents derived classes from accessing them:

```csharp
// Before extraction
public class BaseService
{
    protected virtual void ValidateInput(string input)
    {
        if (string.IsNullOrEmpty(input))
            throw new ArgumentException("Invalid input");
    }
}

public class DerivedService : BaseService
{
    public void Process(string data)
    {
        ValidateInput(data);  // Can access protected method
        // Process data
    }
}

// After extracting ValidateInput - BREAKS INHERITANCE
public class BaseService
{
    private readonly Validator _validator = new Validator();
    // ValidateInput no longer accessible to derived classes
}

internal class Validator
{
    internal void ValidateInput(string input)  // Now internal, not protected
    {
        if (string.IsNullOrEmpty(input))
            throw new ArgumentException("Invalid input");
    }
}

public class DerivedService : BaseService
{
    public void Process(string data)
    {
        ValidateInput(data);  // ERROR: Cannot access ValidateInput
        // Process data
    }
}
```

**When to Use Extract Class** (Composition Pattern):
- Original class uses composition, not inheritance
- Extracted functionality doesn't need to be inherited
- Breaking down large classes into focused services
- Creating stateless helper classes

**When NOT to Use Extract Class** (Use Extract Superclass Instead):
- Class hierarchy relies on protected members
- Derived classes need to override or access the extracted methods
- Working with template method patterns
- Maintaining inheritance-based polymorphism

**Alternative**: If you need to extract protected methods while preserving inheritance, consider:
1. **Extract Superclass**: Move protected members to a base class (not yet implemented)
2. **Manual Refactoring**: Create a protected composition field that derived classes can access
3. **Strategy Pattern**: Replace inheritance with composition using interfaces

**Example - Manual Workaround for Protected Access**:
```csharp
// Manually expose extracted class to derived classes
public class BaseService
{
    protected readonly Validator Validator = new Validator();  // Protected field
}

public class Validator  // Make public instead of internal
{
    public virtual void ValidateInput(string input)  // Public for access
    {
        if (string.IsNullOrEmpty(input))
            throw new ArgumentException("Invalid input");
    }
}

public class DerivedService : BaseService
{
    public void Process(string data)
    {
        Validator.ValidateInput(data);  // Can access through protected field
        // Process data
    }
}
```

### Compilation Validation (V1.3+)

Starting with V1.3, Extract Class includes **optional compilation validation** that ensures the extracted code compiles successfully with framework-specific BCL references. **Validation is enabled by default.**

#### Example 1: Validation Enabled by Default (Recommended)

**Before:**
```csharp
public class DataProcessor
{
    private string _data;

    private void ValidateData()
    {
        if (string.IsNullOrEmpty(_data))
            throw new ArgumentException("Invalid data");
    }

    private string FormatData(string input)
    {
        return input.ToUpper();
    }

    public void Process()
    {
        ValidateData();
        var formatted = FormatData(_data);
        Console.WriteLine(formatted);
    }
}
```

**MCP Tool Usage (validation enabled by default):**
```javascript
const result = await use_mcp_tool({
  server_name: "refactor-csharp-mcp",
  tool_name: "extract_class",
  arguments: {
    sourceCode: "...",
    className: "DataProcessor",
    newClassName: "DataValidator",
    fieldNames: "",
    methodNames: "ValidateData,FormatData",
    targetFramework: "net8.0",      // Default: "net8.0"
    validateCompilation: true        // Default: false (explicitly enabled here)
  }
});

// Result includes validation confirmation:
// "Extracted 2 method(s) into new class 'DataValidator'.
//  Compilation validation passed for framework net8.0."
```

**After (with validation confirmation):**
```csharp
public class DataProcessor
{
    private string _data;
    private readonly DataValidator _dataValidator = new DataValidator();

    public void Process()
    {
        _dataValidator.ValidateData();
        var formatted = _dataValidator.FormatData(_data);
        Console.WriteLine(formatted);
    }
}

internal class DataValidator
{
    internal void ValidateData()
    {
        if (string.IsNullOrEmpty(_data))
            throw new ArgumentException("Invalid data");
    }

    internal string FormatData(string input)
    {
        return input.ToUpper();
    }
}
```

#### Example 2: Disabling Validation for Custom Types

When extracting code that uses types not available in the BCL (e.g., custom classes, third-party libraries), you may need to disable validation:

**Before:**
```csharp
public class UserService
{
    private ILogger _logger;          // Custom interface
    private IDatabase _database;      // Custom interface
    private string _username;

    public void SaveUser()
    {
        _logger.LogInformation("Saving user");
        _database.Save(_username);
    }
}
```

**MCP Tool Usage (validation disabled for custom types):**
```javascript
const result = await use_mcp_tool({
  server_name: "refactor-csharp-mcp",
  tool_name: "extract_class",
  arguments: {
    sourceCode: "...",
    className: "UserService",
    newClassName: "UserContext",
    fieldNames: "_username",
    validateCompilation: false  // Disable - uses custom ILogger/IDatabase
  }
});

// Result without validation:
// "Extracted 1 field(s) into new class 'UserContext'."
```

#### Example 3: Framework-Specific Validation

Different target frameworks support different language features. Validation ensures compatibility:

**Code with Modern C# 12 Syntax:**
```csharp
public class Calculator
{
    private int[] _numbers = [1, 2, 3];  // Collection expression (C# 12)

    public int Sum()
    {
        return _numbers.Sum();
    }
}
```

**Validation with net8.0 (C# 12 supported):**
```javascript
const result = await use_mcp_tool({
  server_name: "refactor-csharp-mcp",
  tool_name: "extract_class",
  arguments: {
    sourceCode: "...",
    className: "Calculator",
    newClassName: "NumberStore",
    fieldNames: "_numbers",
    targetFramework: "net8.0",      // Supports C# 12
    validateCompilation: true
  }
});

// ✅ Success - C# 12 syntax supported in net8.0
```

**Validation with net48 (C# 7.3 only):**
```javascript
const result = await use_mcp_tool({
  server_name: "refactor-csharp-mcp",
  tool_name: "extract_class",
  arguments: {
    sourceCode: "...",
    className: "Calculator",
    newClassName: "NumberStore",
    fieldNames: "_numbers",
    targetFramework: "net48",       // Only supports C# 7.3
    validateCompilation: true
  }
});

// ❌ Failure - Collection expressions not supported in net48
// Error: "Generated code has compilation errors for framework net48"
// Solution: Either use net8.0 or change collection expression to:
//   private int[] _numbers = new int[] { 1, 2, 3 };
```

#### Example 4: Validation with Complex Refactoring

For complex refactorings with multiple fields and methods, validation provides confidence:

```csharp
public class OrderProcessor
{
    private string _street;
    private string _city;
    private string _state;
    private decimal _taxRate;

    private decimal CalculateTax(decimal amount)
    {
        return amount * _taxRate;
    }

    private string FormatAddress()
    {
        return $"{_street}, {_city}, {_state}";
    }

    public decimal ProcessOrder(decimal orderAmount)
    {
        var tax = CalculateTax(orderAmount);
        var address = FormatAddress();
        Console.WriteLine($"Shipping to: {address}");
        return orderAmount + tax;
    }
}
```

**MCP Tool Usage (validates both fields and methods):**
```javascript
const result = await use_mcp_tool({
  server_name: "refactor-csharp-mcp",
  tool_name: "extract_class",
  arguments: {
    sourceCode: "...",
    className: "OrderProcessor",
    newClassName: "OrderContext",
    fieldNames: "_street,_city,_state,_taxRate",
    methodNames: "CalculateTax,FormatAddress",
    targetFramework: "net8.0",
    validateCompilation: true  // Validates entire refactored code
  }
});

// ✅ Success with validation confirmation:
// "Extracted 4 field(s) and 2 method(s) into new class 'OrderContext'.
//  Compilation validation passed for framework net8.0."
```

#### When to Disable Validation

**Disable validation when:**
1. **Custom Types**: Code uses custom classes, interfaces, or third-party libraries not in BCL
2. **External Dependencies**: Code references types from NuGet packages or project references
3. **Performance Critical**: Very large files where validation overhead is not acceptable
4. **Known Compatibility**: You're confident the extracted code is valid for your framework

**Example - Disabling for Custom Types:**
```javascript
// Code uses ILogger, IDatabase, IEmailService (custom interfaces)
const result = await use_mcp_tool({
  server_name: "refactor-csharp-mcp",
  tool_name: "extract_class",
  arguments: {
    sourceCode: "...",
    className: "Service",
    newClassName: "ServiceContext",
    fieldNames: "_logger,_database,_emailService",
    validateCompilation: false  // Skip validation - custom types
  }
});
```

#### Validation Error Messages

When validation fails, you receive detailed error information:

```javascript
// Example validation failure
{
  "success": false,
  "message": "Generated code has compilation errors for framework net8.0: (5,15): error CS0246: The type or namespace name 'ICustomService' could not be found",
  "error": "Compilation validation failed"
}
```

**Common Validation Errors:**
- **CS0246**: Type or namespace not found → Disable validation for custom types
- **CS1061**: Type doesn't contain definition → Check framework compatibility
- **CS0103**: Name doesn't exist in context → Check field/method references

#### Best Practices for Validation

1. **Enable validation for BCL-only code** - Use `validateCompilation: true` to catch compilation errors with BCL types
2. **Validation disabled by default** - Provides better experience when testing with custom types
3. **Match your project's framework** - Use same `targetFramework` as your .csproj
4. **Review validation errors** - Error messages help identify compatibility issues
5. **Test after refactoring** - Validation doesn't replace unit tests

### Best Practices

1. **Group related fields and methods** - Extract cohesive groups that represent a single concept (e.g., all address-related members).
2. **Use descriptive class names** - The new class name should clearly describe what it represents (`Address`, `Configuration`, `Credentials`, `MethodResolver`).
3. **Service Class Pattern** - Extract methods-only to create stateless service classes that encapsulate related logic.
4. **Handle external references** - Always review and update external references after the refactoring completes.
5. **Test after refactoring** - Run your tests to ensure all references were updated correctly.
6. **Consider partial classes** - References in all parts of a partial class are automatically updated.
7. **Encapsulation** - After extraction, consider making extracted fields private and adding public properties/methods as needed.
8. **Use compilation validation** - Set `validateCompilation: true` for BCL-only code to catch compilation errors early.

## Combined Refactoring Workflow

### Starting Code
```csharp
public class ReportGenerator
{
    public void GenerateReport(ILogger logger, IDatabase db, string reportType)
    {
        logger.Log("Starting report generation");

        // Data retrieval - complex logic
        var query = BuildQuery(reportType);
        var data = db.ExecuteQuery(query);
        var filteredData = data.Where(x => x.IsActive).ToList();

        // Report formatting - complex logic
        var report = new StringBuilder();
        report.AppendLine($"Report Type: {reportType}");
        report.AppendLine($"Generated: {DateTime.Now}");
        foreach (var item in filteredData)
        {
            report.AppendLine($"{item.Name}: {item.Value}");
        }

        SaveReport(report.ToString());
        logger.Log("Report generation complete");
    }
}
```

### Step 1: Extract Method for Data Retrieval
```csharp
public class ReportGenerator
{
    public void GenerateReport(ILogger logger, IDatabase db, string reportType)
    {
        logger.Log("Starting report generation");

        var filteredData = RetrieveAndFilterData(db, reportType);

        // Report formatting - complex logic
        var report = new StringBuilder();
        report.AppendLine($"Report Type: {reportType}");
        report.AppendLine($"Generated: {DateTime.Now}");
        foreach (var item in filteredData)
        {
            report.AppendLine($"{item.Name}: {item.Value}");
        }

        SaveReport(report.ToString());
        logger.Log("Report generation complete");
    }

    private List<DataItem> RetrieveAndFilterData(IDatabase db, string reportType)
    {
        var query = BuildQuery(reportType);
        var data = db.ExecuteQuery(query);
        var filteredData = data.Where(x => x.IsActive).ToList();
        return filteredData;
    }
}
```

### Step 2: Extract Method for Report Formatting
```csharp
public class ReportGenerator
{
    public void GenerateReport(ILogger logger, IDatabase db, string reportType)
    {
        logger.Log("Starting report generation");

        var filteredData = RetrieveAndFilterData(db, reportType);
        var reportContent = FormatReport(reportType, filteredData);

        SaveReport(reportContent);
        logger.Log("Report generation complete");
    }

    private List<DataItem> RetrieveAndFilterData(IDatabase db, string reportType)
    {
        var query = BuildQuery(reportType);
        var data = db.ExecuteQuery(query);
        var filteredData = data.Where(x => x.IsActive).ToList();
        return filteredData;
    }

    private string FormatReport(string reportType, List<DataItem> filteredData)
    {
        var report = new StringBuilder();
        report.AppendLine($"Report Type: {reportType}");
        report.AppendLine($"Generated: {DateTime.Now}");
        foreach (var item in filteredData)
        {
            report.AppendLine($"{item.Name}: {item.Value}");
        }
        return report.ToString();
    }
}
```

### Step 3: Constructor Injection for Dependencies
```csharp
public class ReportGenerator
{
    private readonly ILogger _logger;
    private readonly IDatabase _db;

    public ReportGenerator(ILogger logger, IDatabase db)
    {
        _logger = logger;
        _db = db;
    }

    public void GenerateReport(string reportType)
    {
        _logger.Log("Starting report generation");

        var filteredData = RetrieveAndFilterData(reportType);
        var reportContent = FormatReport(reportType, filteredData);

        SaveReport(reportContent);
        _logger.Log("Report generation complete");
    }

    private List<DataItem> RetrieveAndFilterData(string reportType)
    {
        var query = BuildQuery(reportType);
        var data = _db.ExecuteQuery(query);
        var filteredData = data.Where(x => x.IsActive).ToList();
        return filteredData;
    }

    private string FormatReport(string reportType, List<DataItem> filteredData)
    {
        var report = new StringBuilder();
        report.AppendLine($"Report Type: {reportType}");
        report.AppendLine($"Generated: {DateTime.Now}");
        foreach (var item in filteredData)
        {
            report.AppendLine($"{item.Name}: {item.Value}");
        }
        return report.ToString();
    }
}
```

## Usage Tips

1. **Extract Method**: Use when you have complex logic that can be isolated into a cohesive unit
2. **Constructor Injection**: Apply to dependencies (services, loggers, configs) that are used throughout the class
3. **Combined Refactoring**: Start with Extract Method to simplify logic, then apply Constructor Injection for dependencies
4. **Test Coverage**: Always run tests after refactoring to ensure behavior is preserved

## MCP Tool Invocation Examples

### Via Claude Code

```
User: "Please extract lines 10-15 from UserService.cs into a method called ValidateUser"

Claude Code will invoke:
- Tool: ExtractMethod
- Parameters: sourceCode, startLine=10, endLine=15, newMethodName="ValidateUser"
```

```
User: "Convert the logger and config parameters in CreateUser method to constructor injection"

Claude Code will invoke:
- Tool: ConstructorInjection
- Parameters: sourceCode, className="UserService", methodName="CreateUser",
              parameterNames="logger,config", useProperties=false
```

## Docker MCP Toolkit Integration Examples

### Setup with Docker MCP Gateway

**Step 1: Build and Register**
```bash
# Build the Docker image
docker build -t refactor-csharp-mcp:latest .

# Register with Docker MCP Gateway (Windows)
pwsh ./scripts/register-mcp-gateway.ps1

# Or for Linux/macOS
./scripts/register-mcp-gateway.sh
```

**Step 2: Verify Registration**
```bash
# Check server is in catalog
docker mcp catalog show local-dev

# Verify server is enabled
docker mcp server ls
# Output should include: refactor-csharp-mcp
```

**Step 3: Configure Claude Desktop**

Create or update `%APPDATA%\Claude\claude_desktop_config.json` (Windows) or `~/Library/Application Support/Claude/claude_desktop_config.json` (macOS):

```json
{
  "mcpServers": {
    "refactor-csharp-mcp": {
      "command": "docker",
      "args": ["mcp", "gateway", "run"]
    }
  }
}
```

**Step 4: Use in Claude Desktop**
```
User: "I have a C# class with several fields. Can you help me identify which fields can be made readonly?"

Claude will use the make_field_readonly tool through the Docker MCP Gateway to analyze and refactor your code.
```

### Management Examples

**Enable/Disable Server**
```bash
# Disable the server temporarily
docker mcp server disable refactor-csharp-mcp

# Re-enable when needed
docker mcp server enable refactor-csharp-mcp
```

**Inspect Server Configuration**
```bash
# View detailed server information
docker mcp server inspect refactor-csharp-mcp

# Shows:
# - Available tools
# - Resource limits
# - Transport type (stdio)
# - Container image
```

**Update to New Version**
```bash
# Build new version
docker build -t refactor-csharp-mcp:1.1.0 .

# Update catalog registration
pwsh ./scripts/register-mcp-gateway.ps1 -Version 1.1.0

# Gateway will use the new version on next invocation
```

### Direct Docker Integration (Without Gateway)

**Configure Claude Desktop for Direct Docker:**
```json
{
  "mcpServers": {
    "refactor-csharp-mcp": {
      "command": "docker",
      "args": ["run", "--rm", "-i", "refactor-csharp-mcp:latest"]
    }
  }
}
```

**Benefits:**
- No gateway dependency
- Direct container control
- Simpler debugging

**Use Case:** Development and testing environments where centralized management isn't required.

### VS Code Integration with Docker Gateway

**Configure VS Code settings.json:**
```json
{
  "mcp.servers": {
    "refactor-csharp-mcp": {
      "command": "docker",
      "args": ["mcp", "gateway", "run"],
      "type": "stdio"
    }
  }
}
```

### Troubleshooting Docker Integration

**Server Not Found**
```bash
# Verify image exists
docker images | grep refactor-csharp-mcp

# If missing, rebuild
docker build -t refactor-csharp-mcp:latest .
```

**Gateway Not Starting**
```bash
# Check Docker Desktop is running
docker version

# Verify MCP Gateway is available
docker mcp --help

# Re-register the server
pwsh ./scripts/register-mcp-gateway.ps1 -Validate
```

**Container Won't Start**
```bash
# Test container manually
docker run --rm -i refactor-csharp-mcp:latest

# Check container logs
docker logs <container-id>

# Verify health
docker inspect --format='{{.State.Health.Status}}' <container-id>
```

For more detailed troubleshooting, see [docs/DOCKER-MCP-TOOLKIT.md](docs/DOCKER-MCP-TOOLKIT.md).

## Best Practices

1. **Extract Method**:
   - Create methods with single responsibility
   - Use descriptive method names
   - Keep extracted methods private unless needed publicly

2. **Constructor Injection**:
   - Inject interfaces rather than concrete implementations
   - Use readonly fields to prevent modification
   - Consider property injection for optional dependencies

3. **Code Quality**:
   - Run tests after each refactoring
   - Maintain consistent naming conventions
   - Keep methods focused and concise

4. **Docker Deployment**:
   - Use Docker MCP Gateway for centralized management
   - Enable resource limits for production deployments
   - Monitor container health and resource usage
   - Keep Docker images updated with security patches

## Rename Symbol

The Rename Symbol refactoring renames local variables, parameters, private fields, or private methods at a specific position and updates all references within the same file. It uses position-based resolution for precise symbol identification.

**⚠️ LIMITATION:** Single-file scope only. Cannot rename public/protected members or symbols used across multiple files.

### Example 1: Rename Local Variable

**Use Case:** Rename a poorly named variable to follow naming conventions

**Input Code:**
```csharp
public class Calculator
{
    public int Calculate(int x, int y)
    {
        var temp = x + y;
        var result = temp * 2;
        return result;
    }
}
```

**Tool Call:**
```json
{
  "tool": "rename_symbol",
  "arguments": {
    "sourceCode": "...",
    "lineNumber": 5,
    "columnNumber": 13,
    "newName": "sum",
    "targetFramework": "net8.0"
  }
}
```

**Output Code:**
```csharp
public class Calculator
{
    public int Calculate(int x, int y)
    {
        var sum = x + y;
        var result = sum * 2;
        return result;
    }
}
```

**Explanation:** The variable `temp` at line 5, column 13 is renamed to `sum`. All references to `temp` within the method are automatically updated.

### Example 2: Rename Method Parameter

**Use Case:** Improve parameter name clarity

**Input Code:**
```csharp
public class UserService
{
    public void CreateUser(string n, string e, int a)
    {
        Console.WriteLine($"Creating user: {n}");
        Console.WriteLine($"Email: {e}");
        Console.WriteLine($"Age: {a}");
    }
}
```

**Tool Call (Rename first parameter):**
```json
{
  "tool": "rename_symbol",
  "arguments": {
    "sourceCode": "...",
    "lineNumber": 3,
    "columnNumber": 32,
    "newName": "name",
    "targetFramework": "net8.0"
  }
}
```

**Output Code:**
```csharp
public class UserService
{
    public void CreateUser(string name, string e, int a)
    {
        Console.WriteLine($"Creating user: {name}");
        Console.WriteLine($"Email: {e}");
        Console.WriteLine($"Age: {a}");
    }
}
```

**Explanation:** The parameter `n` is renamed to `name`, and all references within the method body are updated.

### Example 3: Rename Private Field

**Use Case:** Standardize field naming conventions

**Input Code:**
```csharp
public class EmailService
{
    private string smtp;
    private int port;

    public EmailService(string server, int portNumber)
    {
        smtp = server;
        port = portNumber;
    }

    public void Send(string message)
    {
        Console.WriteLine($"Sending via {smtp}:{port}");
    }
}
```

**Tool Call:**
```json
{
  "tool": "rename_symbol",
  "arguments": {
    "sourceCode": "...",
    "lineNumber": 3,
    "columnNumber": 20,
    "newName": "_smtpServer",
    "targetFramework": "net8.0"
  }
}
```

**Output Code:**
```csharp
public class EmailService
{
    private string _smtpServer;
    private int port;

    public EmailService(string server, int portNumber)
    {
        _smtpServer = server;
        port = portNumber;
    }

    public void Send(string message)
    {
        Console.WriteLine($"Sending via {_smtpServer}:{port}");
    }
}
```

**Explanation:** The field `smtp` is renamed to `_smtpServer` following C# naming conventions. All references within the class are updated automatically.

### Example 4: Rename Private Method

**Use Case:** Improve method name clarity

**Input Code:**
```csharp
public class DataProcessor
{
    public void Process(string data)
    {
        var cleaned = Clean(data);
        Save(cleaned);
    }

    private string Clean(string input)
    {
        return input.Trim().ToUpper();
    }

    private void Save(string data)
    {
        Console.WriteLine($"Saving: {data}");
    }
}
```

**Tool Call:**
```json
{
  "tool": "rename_symbol",
  "arguments": {
    "sourceCode": "...",
    "lineNumber": 10,
    "columnNumber": 20,
    "newName": "SanitizeInput",
    "targetFramework": "net8.0"
  }
}
```

**Output Code:**
```csharp
public class DataProcessor
{
    public void Process(string data)
    {
        var cleaned = SanitizeInput(data);
        Save(cleaned);
    }

    private string SanitizeInput(string input)
    {
        return input.Trim().ToUpper();
    }

    private void Save(string data)
    {
        Console.WriteLine($"Saving: {data}");
    }
}
```

**Explanation:** The method `Clean` is renamed to `SanitizeInput`, and the method call on line 5 is automatically updated.

### Example 5: Cannot Rename - Symbol Not Found

**Use Case:** Attempting to rename at an invalid position

**Input Code:**
```csharp
public class Test
{
    public void Method()
    {
        var x = 5;
    }
}
```

**Tool Call (Invalid position):**
```json
{
  "tool": "rename_symbol",
  "arguments": {
    "sourceCode": "...",
    "lineNumber": 4,
    "columnNumber": 1,
    "newName": "newName",
    "targetFramework": "net8.0"
  }
}
```

**Result:**
```json
{
  "success": false,
  "message": "No symbol found at line 4, column 1",
  "error": "Symbol not found at specified position"
}
```

**Explanation:** There's no symbol at the specified position (it's whitespace or a keyword), so the refactoring cannot proceed.

### Example 6: Cannot Rename - Public Member (Limitation)

**Use Case:** Attempting to rename a public method (not supported in V1)

**Input Code:**
```csharp
public class Calculator
{
    public int Add(int a, int b)
    {
        return a + b;
    }
}
```

**Tool Call:**
```json
{
  "tool": "rename_symbol",
  "arguments": {
    "sourceCode": "...",
    "lineNumber": 3,
    "columnNumber": 16,
    "newName": "Sum",
    "targetFramework": "net8.0"
  }
}
```

**Result:**
```json
{
  "success": false,
  "message": "Cannot rename public method 'Add'. Only local variables, parameters, and private members can be renamed (single-file scope limitation)",
  "error": "Public member rename not supported"
}
```

**Explanation:** The `rename_symbol` tool only supports renaming symbols within a single file scope. Public and protected members may be used in other files, so they cannot be safely renamed without cross-file analysis.

### MCP Tool Usage

```javascript
// Rename a symbol at specific position
const result = await use_mcp_tool({
  server_name: "refactor-csharp-mcp",
  tool_name: "rename_symbol",
  arguments: {
    sourceCode: "...",
    lineNumber: 5,        // 1-based line number
    columnNumber: 13,     // 1-based column number
    newName: "newName",   // New identifier (must be valid C# identifier)
    targetFramework: "net8.0"
  }
});

// Check result
if (result.success) {
  console.log("Symbol renamed successfully");
  console.log(result.refactoredCode);
} else {
  console.log(`Rename failed: ${result.message}`);
}
```

### Position-Based Resolution

The `rename_symbol` tool uses position-based resolution to identify the symbol to rename. This means you specify the exact line and column where the symbol appears, and the tool:

1. **Resolves the symbol** at that position using Roslyn semantic analysis
2. **Validates the symbol type** (local variable, parameter, private field, private method)
3. **Finds all references** to that symbol within the same file
4. **Updates all references** with the new name
5. **Preserves formatting** and code structure

**Position Calculation:**
- Line numbers are 1-based (first line is 1, not 0)
- Column numbers are 1-based (first character is 1, not 0)
- Position should point to the symbol identifier, not whitespace or keywords

**Example Position Calculation:**
```csharp
     1  public class Test
     2  {
     3      public void Method()
     4      {
     5          var myVariable = 5;
     6          //  ^^^^^^^^^^
     7          //  Line: 5, Column: 13 (start of 'myVariable')
     8      }
     9  }
```

**Note**: Line numbers are relative to the entire file (1-based), not relative to the method or block.

### Supported Symbol Types

| Symbol Type | Scope | Example | Supported |
|------------|-------|---------|-----------|
| Local Variable | Method | `var x = 5;` | ✅ Yes |
| Method Parameter | Method | `void M(int x)` | ✅ Yes |
| Private Field | Class | `private int _x;` | ✅ Yes |
| Private Method | Class | `private void M()` | ✅ Yes |
| Public Field | Class | `public int X;` | ❌ No (V1) |
| Public Method | Class | `public void M()` | ❌ No (V1) |
| Protected Member | Class | `protected int X;` | ❌ No (V1) |
| Internal Member | Class | `internal int X;` | ❌ No (V1) |

### Limitations

**Single-File Scope (V1):**
- Only renames symbols within the current file
- Cannot rename public/protected/internal members
- Cannot rename symbols used across multiple files
- For cross-file renames, use IDE tools (Visual Studio, VS Code, Rider)

**Valid Identifiers Only:**
- New name must be a valid C# identifier
- Cannot use C# keywords (e.g., `int`, `class`, `void`)
- Must follow C# naming rules (alphanumeric + underscore, cannot start with digit)

**Position Accuracy:**
- Must specify exact position of symbol identifier
- Whitespace, keywords, or operators will result in "symbol not found" error

### Best Practices

1. **Use IDE tools for public members** - Visual Studio, VS Code, and Rider support cross-file renaming
2. **Verify position** - Ensure line and column numbers point to the symbol identifier
3. **Follow naming conventions** - Use consistent naming patterns (e.g., `_camelCase` for private fields)
4. **Single file refactoring** - Only use this tool when symbols are confined to a single file
5. **Test after rename** - Run tests to ensure all references were updated correctly
6. **Framework-independent** - Renaming works identically across all .NET frameworks

### Example Workflow: Batch Rename Parameters

**Input Code:**
```csharp
public class UserService
{
    public void CreateUser(string n, string e, int a)
    {
        Console.WriteLine($"Name: {n}, Email: {e}, Age: {a}");
    }
}
```

**Step 1: Rename first parameter (`n` → `name`):**
```javascript
const step1 = await use_mcp_tool({
  server_name: "refactor-csharp-mcp",
  tool_name: "rename_symbol",
  arguments: {
    sourceCode: originalCode,
    lineNumber: 3,
    columnNumber: 32,
    newName: "name",
    targetFramework: "net8.0"
  }
});
```

**Step 2: Rename second parameter (`e` → `email`):**
```javascript
const step2 = await use_mcp_tool({
  server_name: "refactor-csharp-mcp",
  tool_name: "rename_symbol",
  arguments: {
    sourceCode: step1.refactoredCode,  // Use output from step 1
    lineNumber: 3,
    columnNumber: 48,  // Position may have shifted
    newName: "email",
    targetFramework: "net8.0"
  }
});
```

**Step 3: Rename third parameter (`a` → `age`):**
```javascript
const step3 = await use_mcp_tool({
  server_name: "refactor-csharp-mcp",
  tool_name: "rename_symbol",
  arguments: {
    sourceCode: step2.refactoredCode,
    lineNumber: 3,
    columnNumber: 61,
    newName: "age",
    targetFramework: "net8.0"
  }
});
```

**Final Output:**
```csharp
public class UserService
{
    public void CreateUser(string name, string email, int age)
    {
        Console.WriteLine($"Name: {name}, Email: {email}, Age: {age}");
    }
}
```

## Fix Diagnostic

The Fix Diagnostic refactoring automatically fixes specific Roslyn diagnostics by applying the appropriate refactoring. It supports common code quality issues like unused usings (IDE0005/CS8019) and readonly fields (IDE0044). The tool is framework-aware and applies fixes according to target framework capabilities.

**See also:** [Analyze Code](#analyze-code) to discover all diagnostics in your code before fixing them.

### Example 1: Fix Unused Using Directive

**Use Case:** Automatically remove an unused using directive detected by the compiler

**Input Code:**
```csharp
using System;
using System.Collections.Generic;
using System.Linq;

public class Calculator
{
    public int Add(int a, int b)
    {
        Console.WriteLine($"Adding {a} + {b}");
        return a + b;
    }
}
```

**Tool Call:**
```json
{
  "tool": "fix_diagnostic",
  "arguments": {
    "sourceCode": "...",
    "diagnosticId": "IDE0005",
    "line": 2,
    "column": 1,
    "targetFramework": "net8.0"
  }
}
```

**Output Code:**
```csharp
using System;
using System.Linq;

public class Calculator
{
    public int Add(int a, int b)
    {
        Console.WriteLine($"Adding {a} + {b}");
        return a + b;
    }
}
```

**Explanation:** The diagnostic IDE0005 indicates that the `using System.Collections.Generic;` directive is unnecessary. The fix removes this specific using while preserving others.

### Example 2: Fix Readonly Field (IDE0044)

**Use Case:** Automatically add readonly modifier to a field that's only assigned in constructor

**Input Code:**
```csharp
public class EmailService
{
    private string _smtpServer;
    private int _port;

    public EmailService(string server, int port)
    {
        _smtpServer = server;
        _port = port;
    }

    public void Send(string message)
    {
        Console.WriteLine($"Sending via {_smtpServer}:{_port}");
    }
}
```

**Tool Call:**
```json
{
  "tool": "fix_diagnostic",
  "arguments": {
    "sourceCode": "...",
    "diagnosticId": "IDE0044",
    "line": 3,
    "column": 20,
    "targetFramework": "net8.0"
  }
}
```

**Output Code:**
```csharp
public class EmailService
{
    private readonly string _smtpServer;
    private int _port;

    public EmailService(string server, int port)
    {
        _smtpServer = server;
        _port = port;
    }

    public void Send(string message)
    {
        Console.WriteLine($"Sending via {_smtpServer}:{_port}");
    }
}
```

**Explanation:** The diagnostic IDE0044 indicates that `_smtpServer` can be made readonly. The fix adds the `readonly` modifier to prevent accidental modifications after construction.

### Example 3: Fix Compiler Warning CS8019

**Use Case:** Remove unused using directive flagged by compiler warning

**Input Code:**
```csharp
using System;
using System.Text;
using System.Threading.Tasks;

public class Logger
{
    public void Log(string message)
    {
        Console.WriteLine($"[{DateTime.Now}] {message}");
    }
}
```

**Tool Call:**
```json
{
  "tool": "fix_diagnostic",
  "arguments": {
    "sourceCode": "...",
    "diagnosticId": "CS8019",
    "line": 3,
    "column": 1,
    "targetFramework": "net8.0"
  }
}
```

**Output Code:**
```csharp
using System;
using System.Threading.Tasks;

public class Logger
{
    public void Log(string message)
    {
        Console.WriteLine($"[{DateTime.Now}] {message}");
    }
}
```

**Explanation:** CS8019 is the compiler warning for unnecessary using directives. The fix removes `using System.Text;` which is not referenced in the code.

### Example 4: Unsupported Diagnostic

**Use Case:** Attempting to fix a diagnostic that's not supported

**Input Code:**
```csharp
public class Test
{
    public void Method()
    {
        var x = 5;
        x = 10;  // IDE0059: Unnecessary assignment
    }
}
```

**Tool Call:**
```json
{
  "tool": "fix_diagnostic",
  "arguments": {
    "sourceCode": "...",
    "diagnosticId": "IDE0059",
    "line": 6,
    "column": 9,
    "targetFramework": "net8.0"
  }
}
```

**Result:**
```json
{
  "success": false,
  "message": "Diagnostic IDE0059 is not currently supported for automatic fixing",
  "error": "Unsupported diagnostic",
  "supportedDiagnostics": ["IDE0005", "CS8019", "IDE0044"]
}
```

**Explanation:** Not all diagnostics can be automatically fixed. The tool only supports specific diagnostics where the fix is unambiguous and safe.

### Example 5: Framework-Specific Fix

**Use Case:** Fixing unused usings with framework-aware global using handling

**Input Code (.NET 8):**
```csharp
using System;
using System.Collections.Generic;

public class Service
{
    public void Process()
    {
        Console.WriteLine("Processing");  // Uses implicit global using System
    }
}
```

**Tool Call:**
```json
{
  "tool": "fix_diagnostic",
  "arguments": {
    "sourceCode": "...",
    "diagnosticId": "IDE0005",
    "line": 1,
    "column": 1,
    "targetFramework": "net8.0"
  }
}
```

**Output Code:**
```csharp
using System.Collections.Generic;

public class Service
{
    public void Process()
    {
        Console.WriteLine("Processing");
    }
}
```

**Explanation:** On .NET 8+, the SDK includes global usings for common namespaces like `System`. The fix removes the explicit `using System;` because it's redundant with the global using.

### Example 6: Diagnostic Not Found at Location

**Use Case:** Attempting to fix a diagnostic at an incorrect location

**Input Code:**
```csharp
using System;

public class Test
{
    public void Method()
    {
        Console.WriteLine("Hello");
    }
}
```

**Tool Call:**
```json
{
  "tool": "fix_diagnostic",
  "arguments": {
    "sourceCode": "...",
    "diagnosticId": "IDE0005",
    "line": 5,
    "column": 1,
    "targetFramework": "net8.0"
  }
}
```

**Result:**
```json
{
  "success": false,
  "message": "No IDE0005 diagnostic found at line 5, column 1",
  "error": "Diagnostic not found at specified location"
}
```

**Explanation:** The diagnostic must exist at the exact location specified. If the line/column doesn't match where the diagnostic actually occurs, the fix cannot be applied.

### MCP Tool Usage

```javascript
// Fix a specific diagnostic
const result = await use_mcp_tool({
  server_name: "refactor-csharp-mcp",
  tool_name: "fix_diagnostic",
  arguments: {
    sourceCode: "...",
    diagnosticId: "IDE0005",
    line: 2,
    column: 1,
    targetFramework: "net8.0"
  }
});

// Check result
if (result.success) {
  console.log("Diagnostic fixed successfully");
  console.log(result.refactoredCode);
} else {
  console.log(`Fix failed: ${result.message}`);
  if (result.supportedDiagnostics) {
    console.log("Supported diagnostics:", result.supportedDiagnostics);
  }
}
```

### Supported Diagnostics

| Diagnostic ID | Description | Fix Applied |
|--------------|-------------|-------------|
| IDE0005 | Using directive is unnecessary | Remove unused using |
| CS8019 | Unnecessary using directive | Remove unused using |
| IDE0044 | Add readonly modifier | Add `readonly` to field |

**Note**: The list of supported diagnostics may expand in future versions.

### Best Practices

1. **Use with analyze_code** - Run `analyze_code` first to discover all diagnostics, then fix them one by one
2. **Verify location** - Ensure line and column numbers match where the diagnostic actually occurs
3. **Framework awareness** - Specify correct target framework for framework-specific fixes
4. **Check support** - Not all diagnostics can be automatically fixed - check supported list
5. **Batch processing** - Fix diagnostics sequentially, as fixing one may affect others
6. **Manual verification** - Always review automatic fixes before committing

### Workflow Integration

The `fix_diagnostic` tool is designed to work with `analyze_code`:

```javascript
// Step 1: Analyze code to find diagnostics
const analysis = await use_mcp_tool({
  server_name: "refactor-csharp-mcp",
  tool_name: "analyze_code",
  arguments: {
    sourceCode: code,
    targetFramework: "net8.0",
    minSeverity: "Warning"
  }
});

// Step 2: Fix each supported diagnostic
for (const diagnostic of analysis.diagnostics) {
  if (["IDE0005", "CS8019", "IDE0044"].includes(diagnostic.id)) {
    const fix = await use_mcp_tool({
      server_name: "refactor-csharp-mcp",
      tool_name: "fix_diagnostic",
      arguments: {
        sourceCode: code,
        diagnosticId: diagnostic.id,
        line: diagnostic.location.line,
        column: diagnostic.location.column,
        targetFramework: "net8.0"
      }
    });

    if (fix.success) {
      code = fix.refactoredCode;  // Update code with fix
    }
  }
}
```

## Analyze Code

The Analyze Code tool performs comprehensive code analysis using Roslyn diagnostics with full IDE analyzer support (IDE0001-IDE9999). It detects compiler warnings, style violations, and code quality issues, returning detailed information about each finding including location, severity, and applicable refactorings. The tool is framework-aware and analyzes code according to target framework capabilities.

**See also:** [Fix Diagnostic](#fix-diagnostic) to automatically fix issues discovered by analysis, and [Make Field Readonly](#make-field-readonly) to address IDE0044 diagnostics.

### Example 1: Basic Code Analysis

**Use Case:** Analyze code for common issues and style violations

**Input Code:**
```csharp
using System;
using System.Collections.Generic;
using System.Linq;

public class UserService
{
    private string _name;
    private int _age;

    public UserService()
    {
        _name = "Unknown";
        _age = 0;
    }

    public void PrintInfo()
    {
        Console.WriteLine($"Name: {_name}, Age: {_age}");
    }
}
```

**Tool Call:**
```json
{
  "tool": "analyze_code",
  "arguments": {
    "sourceCode": "...",
    "targetFramework": "net8.0",
    "minSeverity": "Info"
  }
}
```

**Output:**
```json
{
  "success": true,
  "diagnostics": [
    {
      "id": "IDE0005",
      "severity": "Info",
      "message": "Using directive is unnecessary",
      "location": {
        "line": 2,
        "column": 1,
        "file": "source"
      },
      "category": "Style",
      "applicableRefactorings": ["remove_unused_usings", "fix_diagnostic"]
    },
    {
      "id": "IDE0005",
      "severity": "Info",
      "message": "Using directive is unnecessary",
      "location": {
        "line": 3,
        "column": 1,
        "file": "source"
      },
      "category": "Style",
      "applicableRefactorings": ["remove_unused_usings", "fix_diagnostic"]
    },
    {
      "id": "IDE0044",
      "severity": "Info",
      "message": "Add readonly modifier",
      "location": {
        "line": 7,
        "column": 20,
        "file": "source"
      },
      "category": "Style",
      "applicableRefactorings": ["make_field_readonly", "fix_diagnostic"]
    },
    {
      "id": "IDE0044",
      "severity": "Info",
      "message": "Add readonly modifier",
      "location": {
        "line": 8,
        "column": 17,
        "file": "source"
      },
      "category": "Style",
      "applicableRefactorings": ["make_field_readonly", "fix_diagnostic"]
    }
  ],
  "summary": {
    "totalDiagnostics": 4,
    "errorCount": 0,
    "warningCount": 0,
    "infoCount": 4,
    "hiddenCount": 0
  }
}
```

**Explanation:** The analysis found 4 style issues: 2 unused using directives and 2 fields that can be made readonly. Each diagnostic includes its location and suggests applicable refactorings.

### Example 2: Severity Filtering

**Use Case:** Analyze code for warnings and errors only, ignoring info-level diagnostics

**Input Code:**
```csharp
using System;

public class Calculator
{
    public int Divide(int a, int b)
    {
        return a / b;  // No null/zero check - potential runtime error
    }
}
```

**Tool Call:**
```json
{
  "tool": "analyze_code",
  "arguments": {
    "sourceCode": "...",
    "targetFramework": "net8.0",
    "minSeverity": "Warning"
  }
}
```

**Output:**
```json
{
  "success": true,
  "diagnostics": [],
  "summary": {
    "totalDiagnostics": 0,
    "errorCount": 0,
    "warningCount": 0,
    "infoCount": 0,
    "hiddenCount": 0
  }
}
```

**Explanation:** With `minSeverity: "Warning"`, only warnings and errors are reported. Info-level suggestions are filtered out. In this case, the potential division by zero isn't flagged by Roslyn as a warning in this simple context.

### Example 3: Framework-Specific Analysis

**Use Case:** Analyze code with framework-specific language features

**Input Code:**
```csharp
using System;

public class Example
{
    public void Method()
    {
        var numbers = [1, 2, 3];  // Collection expression (C# 12)
        Console.WriteLine(numbers.Length);
    }
}
```

**Tool Call (net8.0 - Supports C# 12):**
```json
{
  "tool": "analyze_code",
  "arguments": {
    "sourceCode": "...",
    "targetFramework": "net8.0",
    "minSeverity": "Error"
  }
}
```

**Output:**
```json
{
  "success": true,
  "diagnostics": [],
  "summary": {
    "totalDiagnostics": 0,
    "errorCount": 0,
    "warningCount": 0
  }
}
```

**Tool Call (net48 - Only C# 7.3):**
```json
{
  "tool": "analyze_code",
  "arguments": {
    "sourceCode": "...",
    "targetFramework": "net48",
    "minSeverity": "Error"
  }
}
```

**Output:**
```json
{
  "success": true,
  "diagnostics": [
    {
      "id": "CS1525",
      "severity": "Error",
      "message": "Invalid expression term '['",
      "location": {
        "line": 7,
        "column": 23,
        "file": "source"
      },
      "category": "Compiler Error",
      "applicableRefactorings": []
    }
  ],
  "summary": {
    "totalDiagnostics": 1,
    "errorCount": 1,
    "warningCount": 0
  }
}
```

**Explanation:** The same code produces different analysis results based on target framework. Collection expressions are valid in net8.0 (C# 12) but cause compiler errors in net48 (C# 7.3).

### Example 4: Complete Code Quality Check

**Use Case:** Comprehensive analysis including all severity levels

**Input Code:**
```csharp
using System;
using System.Collections.Generic;

public class DataProcessor
{
    private List<string> data;

    public DataProcessor()
    {
        data = new List<string>();
    }

    public void Process()
    {
        foreach (var item in data)
        {
            Console.WriteLine(item);
        }
    }
}
```

**Tool Call:**
```json
{
  "tool": "analyze_code",
  "arguments": {
    "sourceCode": "...",
    "targetFramework": "net8.0",
    "minSeverity": "Hidden"
  }
}
```

**Output:**
```json
{
  "success": true,
  "diagnostics": [
    {
      "id": "IDE0044",
      "severity": "Info",
      "message": "Add readonly modifier",
      "location": {
        "line": 6,
        "column": 29,
        "file": "source"
      },
      "category": "Style",
      "applicableRefactorings": ["make_field_readonly", "fix_diagnostic"]
    },
    {
      "id": "IDE0090",
      "severity": "Hidden",
      "message": "Use 'new(...)' for object creation",
      "location": {
        "line": 10,
        "column": 15,
        "file": "source"
      },
      "category": "Style",
      "applicableRefactorings": []
    }
  ],
  "summary": {
    "totalDiagnostics": 2,
    "errorCount": 0,
    "warningCount": 0,
    "infoCount": 1,
    "hiddenCount": 1
  }
}
```

**Explanation:** With `minSeverity: "Hidden"`, all diagnostics are returned including hidden style suggestions like using implicit object creation.

### Example 5: No Issues Found

**Use Case:** Analyzing clean, well-written code

**Input Code:**
```csharp
using System;

public class Calculator
{
    public int Add(int a, int b)
    {
        return a + b;
    }

    public int Multiply(int a, int b)
    {
        return a * b;
    }
}
```

**Tool Call:**
```json
{
  "tool": "analyze_code",
  "arguments": {
    "sourceCode": "...",
    "targetFramework": "net8.0",
    "minSeverity": "Info"
  }
}
```

**Output:**
```json
{
  "success": true,
  "diagnostics": [],
  "summary": {
    "totalDiagnostics": 0,
    "errorCount": 0,
    "warningCount": 0,
    "infoCount": 0,
    "hiddenCount": 0
  }
}
```

**Explanation:** No diagnostics found - the code follows best practices and has no style violations or issues.

### Example 6: Syntax Errors

**Use Case:** Analyzing code with syntax errors

**Input Code:**
```csharp
using System;

public class Test
{
    public void Method()
    {
        var x = 5
        Console.WriteLine(x);  // Missing semicolon above
    }
}
```

**Tool Call:**
```json
{
  "tool": "analyze_code",
  "arguments": {
    "sourceCode": "...",
    "targetFramework": "net8.0",
    "minSeverity": "Error"
  }
}
```

**Output:**
```json
{
  "success": true,
  "diagnostics": [
    {
      "id": "CS1002",
      "severity": "Error",
      "message": "; expected",
      "location": {
        "line": 7,
        "column": 19,
        "file": "source"
      },
      "category": "Compiler Error",
      "applicableRefactorings": []
    }
  ],
  "summary": {
    "totalDiagnostics": 1,
    "errorCount": 1,
    "warningCount": 0
  }
}
```

**Explanation:** Syntax errors are reported as compiler errors with specific locations and descriptions.

### MCP Tool Usage

```javascript
// Analyze code for all issues
const result = await use_mcp_tool({
  server_name: "refactor-csharp-mcp",
  tool_name: "analyze_code",
  arguments: {
    sourceCode: "...",
    targetFramework: "net8.0",
    minSeverity: "Info"  // Info, Warning, Error, or Hidden
  }
});

// Process results
if (result.success) {
  console.log(`Found ${result.summary.totalDiagnostics} issues`);
  console.log(`  Errors: ${result.summary.errorCount}`);
  console.log(`  Warnings: ${result.summary.warningCount}`);
  console.log(`  Info: ${result.summary.infoCount}`);

  // Group by severity
  const errors = result.diagnostics.filter(d => d.severity === "Error");
  const warnings = result.diagnostics.filter(d => d.severity === "Warning");
  const info = result.diagnostics.filter(d => d.severity === "Info");

  // Show applicable refactorings
  result.diagnostics.forEach(diagnostic => {
    if (diagnostic.applicableRefactorings.length > 0) {
      console.log(`${diagnostic.id}: Can fix with ${diagnostic.applicableRefactorings.join(", ")}`);
    }
  });
}
```

### Diagnostic Categories

| Category | Description | Examples |
|----------|-------------|----------|
| Compiler Error | Syntax or semantic errors that prevent compilation | CS1002, CS0246 |
| Compiler Warning | Potential issues that don't prevent compilation | CS0168, CS8019 |
| Style | Code style and formatting suggestions | IDE0005, IDE0044 |
| Design | Design pattern and architecture recommendations | CA1000, CA1001 |
| Performance | Performance optimization suggestions | CA1806, CA1810 |
| Security | Security vulnerabilities and best practices | CA2100, CA3001 |

### Severity Levels

| Level | Description | Use Case |
|-------|-------------|----------|
| Error | Prevents compilation | Must fix before build |
| Warning | Potential issues | Should review and fix |
| Info | Style suggestions | Optional improvements |
| Hidden | IDE-only hints | Typically for refactoring suggestions |

### Best Practices

1. **Start with errors** - Set `minSeverity: "Error"` to find critical issues first
2. **Incremental cleanup** - Address errors, then warnings, then info-level issues
3. **Framework matching** - Use the same framework as your project's target
4. **Combine with fixes** - Use `analyze_code` to find issues, then `fix_diagnostic` to apply fixes
5. **Regular analysis** - Run analysis frequently during development
6. **Review suggestions** - Not all diagnostics need to be fixed - use judgment

### Workflow: Analysis → Fix Loop

```javascript
// Complete code quality improvement workflow
let code = originalCode;
let iteration = 0;
const maxIterations = 10;

while (iteration < maxIterations) {
  // Analyze current code
  const analysis = await use_mcp_tool({
    server_name: "refactor-csharp-mcp",
    tool_name: "analyze_code",
    arguments: {
      sourceCode: code,
      targetFramework: "net8.0",
      minSeverity: "Info"
    }
  });

  // Exit if no more fixable diagnostics
  const fixable = analysis.diagnostics.filter(d =>
    d.applicableRefactorings && d.applicableRefactorings.length > 0
  );

  if (fixable.length === 0) {
    console.log("No more fixable diagnostics");
    break;
  }

  // Fix first diagnostic
  const diagnostic = fixable[0];
  console.log(`Fixing ${diagnostic.id} at line ${diagnostic.location.line}`);

  const fix = await use_mcp_tool({
    server_name: "refactor-csharp-mcp",
    tool_name: "fix_diagnostic",
    arguments: {
      sourceCode: code,
      diagnosticId: diagnostic.id,
      line: diagnostic.location.line,
      column: diagnostic.location.column,
      targetFramework: "net8.0"
    }
  });

  if (fix.success) {
    code = fix.refactoredCode;
    iteration++;
  } else {
    console.log(`Could not fix ${diagnostic.id}: ${fix.message}`);
    break;
  }
}

console.log(`Code quality improved after ${iteration} fixes`);
```

## Framework-Aware Validation

RefactorCsharpMCP includes framework-aware validation to ensure refactored code is compatible with your target .NET framework. All refactorings validate both input and output code against the specified framework's C# language version.

### Example 1: Successful Validation (net8.0)

**Input Code:**
```csharp
public class Example
{
    public void Method()
    {
        var numbers = new[] { 1, 2, 3 };
        Console.WriteLine(numbers.Length);
    }
}
```

**Refactoring with async API:**
```csharp
var refactoring = new ExtractMethod();
var result = await refactoring.ExecuteAsync(
    sourceCode: code,
    startLine: 3,
    endLine: 4,
    newMethodName: "PrintCount",
    targetFramework: "net8.0"  // Modern .NET supports this syntax
);

// result.IsSuccess == true
// result.RefactoredCode contains valid C# 12 code
```

### Example 2: Input Syntax Mismatch (net48)

**Input Code with C# 12 Syntax:**
```csharp
public class Example
{
    public void Method()
    {
        int[] numbers = [1, 2, 3];  // Collection expressions (C# 12)
        Console.WriteLine(numbers.Length);
    }
}
```

**Refactoring Attempt:**
```csharp
var refactoring = new ExtractMethod();
var result = await refactoring.ExecuteAsync(
    sourceCode: code,
    startLine: 3,
    endLine: 4,
    newMethodName: "PrintCount",
    targetFramework: "net48"  // .NET Framework 4.8 only supports C# 7.3
);

// result.IsSuccess == false
// result.ErrorCode == ErrorCode.INPUT_SYNTAX_MISMATCH
// result.ErrorMessage:
//   "Input code uses collection expressions (C# 12), but target framework net48 supports C# 7.3."
// result.SuggestedAction:
//   "Either update targetFramework to a version supporting C# 12 or modify input code to use compatible syntax."
```

### Example 3: Framework Syntax Mismatch Detection

The validation framework also prevents generating code that uses features unavailable in the target framework:

```csharp
var validation = new SyntaxValidator();
var result = await validation.ValidateOutputAsync(
    refactoredCode,
    targetFramework: "net35"  // .NET Framework 3.5 supports C# 3.0
);

// If refactored code uses features like:
// - Tuples (C# 7.0+)
// - Nullable reference types (C# 8.0+)
// - Record types (C# 9.0+)
// - Primary constructors (C# 12+)
//
// result.IsValid == false
// result.ErrorCode == ErrorCode.FRAMEWORK_SYNTAX_MISMATCH
```

### Supported Frameworks and Language Versions

| Framework | C# Version | Features Available |
|-----------|------------|-------------------|
| net9.0 | C# 13 | Latest features |
| net8.0 | C# 12 | Collection expressions, primary constructors |
| net48/net481 | C# 7.3 | Tuples, pattern matching basics |
| net462-net472 | C# 7.3 | Same as net48 |
| net35 | C# 3.0 | LINQ, lambdas, var |
| netstandard2.1 | C# 8.0 | Nullable reference types, ranges |
| netstandard2.0 | C# 7.3 | Same as net48 |

### Using Validation APIs

**Async Validation-Aware APIs (Recommended):**
```csharp
// Extract Method with validation
var result = await extractMethod.ExecuteAsync(code, 1, 5, "NewMethod", "net8.0");

// Constructor Injection with validation
var result = await ctorInjection.ExecuteAsync(code, "MyClass", "Method",
    new[] {"logger"}, "net48");

// Make Field Readonly with validation
var result = await makeReadonly.ExecuteAsync(code, "MyClass", "_field", "net8.0");

// Safe Delete with validation
var result = await safeDelete.ExecuteAsync(code, "MyClass", "UnusedMethod", "net48");

// Extract Class with validation
var result = await extractClass.ExecuteAsync(code, "MyClass", "NewClass",
    "field1,field2", "net8.0");
```

**Legacy Sync APIs (No Validation):**
```csharp
// Still available for backward compatibility
var result = extractMethod.Execute(code, 1, 5, "NewMethod");
// No framework validation - use with caution
```

### Validation Error Codes

| Error Code | Description | Resolution |
|------------|-------------|------------|
| INPUT_SYNTAX_MISMATCH | Input code uses features not supported by target framework | Update framework or modify input code |
| FRAMEWORK_SYNTAX_MISMATCH | Refactored code would use unsupported features | Update target framework or use manual refactoring |
| SYNTAX_ERROR | Code contains syntax errors | Fix syntax errors before refactoring |
| UNKNOWN_FRAMEWORK | Framework moniker not recognized | Use supported framework (net8.0, net48, etc.) |

### Best Practices for Framework Validation

1. **Always Specify Target Framework**: Use ExecuteAsync methods with targetFramework parameter
2. **Match Your Project's TFM**: Use the same framework moniker as your .csproj file
3. **Handle Validation Failures**: Check `result.IsValid` and `result.ValidationResult` before using refactored code
4. **Read Error Messages**: ValidationResult includes detailed error messages and suggested actions
5. **Multi-Target Projects**: Run refactorings separately for each target framework

## Framework Limitations and Workarounds

RefactorCsharpMCP has known limitations when working with certain frameworks. This section provides practical examples and workarounds.

### IDE Analyzer Limitations (Issue #72)

The `remove_unused_usings` refactoring may not detect all unused using directives because it relies on Roslyn compiler APIs rather than full IDE workspace APIs.

**Example - Unused Using Not Detected:**
```csharp
using System;
using System.Linq;  // May not be detected as unused
using System.Collections.Generic;  // May not be detected as unused

public class Example
{
    public void Method()
    {
        Console.WriteLine("Hello");  // Only System is used
    }
}

// Running remove_unused_usings
var refactoring = new RemoveUnusedUsings();
var result = await refactoring.ExecuteAsync(sourceCode, "net8.0");

// result.IsSuccess may be true, but some unused usings remain
// or result.IsSuccess may be false with error:
//   "No unused using directives detected (IDE analyzer limitation)"
```

**Workarounds:**

1. **Use IDE-Based Tools**:
```csharp
// Use Visual Studio, VS Code with C# extension, or Rider
// Tools → Remove and Sort Usings
// These have full workspace context
```

2. **Manual Review**:
```csharp
// Manually check each using directive
// Remove directives whose types aren't referenced in the file
```

3. **Build-Time Validation**:
```bash
# Enable TreatWarningsAsErrors in .csproj
<PropertyGroup>
  <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
</PropertyGroup>

# Build will fail on CS8019 (unused using) if detected
dotnet build
```

### .NET Framework Reference Assembly Limitations (Issue #75)

Refactorings targeting .NET Framework 4.8 and earlier may fail due to unavailable reference assemblies.

**Example - net48 Reference Assembly Error:**
```csharp
public class Calculator
{
    public int Add(int a, int b)
    {
        int result = a + b;
        return result;
    }
}

// Attempting refactoring with net48
var refactoring = new InlineVariable();
var result = await refactoring.ExecuteAsync(
    sourceCode,
    lineNumber: 5,
    columnNumber: 13,
    targetFramework: "net48"
);

// May fail with:
// result.IsSuccess == false
// result.ErrorMessage:
//   "Code references types or members not available in net48"
//   "Failed to load reference assemblies for net48"
```

**Workarounds:**

1. **Use Modern Frameworks (Recommended)**:
```csharp
// Use net8.0 or net9.0 for best reliability
var result = await refactoring.ExecuteAsync(
    sourceCode,
    lineNumber: 5,
    columnNumber: 13,
    targetFramework: "net8.0"  // Fully supported
);

// If code must target net48, refactor with net8.0 first
// Then manually verify compatibility with net48 project
```

2. **Pre-warm Cache Strategy**:
```csharp
// Run refactorings on modern frameworks first
// This populates the reference assembly cache
var modernResult = await refactoring.ExecuteAsync(code, 1, 1, "net8.0");

// Then try net48 (may work if assemblies are cached)
var legacyResult = await refactoring.ExecuteAsync(code, 1, 1, "net48");
```

3. **Check and Clear Cache**:
```powershell
# Windows PowerShell - Check cache
Get-ChildItem "$env:USERPROFILE\.refactor-csharp-mcp\reference-assemblies\net48"

# If corrupted, clear and retry
Remove-Item -Recurse "$env:USERPROFILE\.refactor-csharp-mcp\reference-assemblies\net48"

# Linux/Mac
ls ~/.refactor-csharp-mcp/reference-assemblies/net48
rm -rf ~/.refactor-csharp-mcp/reference-assemblies/net48
```

4. **Install Reference Assemblies Manually**:
```bash
# Add NuGet package to your project
dotnet add package Microsoft.NETFramework.ReferenceAssemblies
```

### Framework Support Matrix with Examples

**Fully Supported Frameworks (Recommended)**:
```csharp
// net8.0 - Best support, C# 12 features
var result = await refactoring.ExecuteAsync(code, "net8.0");
// Expected: High reliability, fast execution

// net9.0 - Latest features, C# 13
var result = await refactoring.ExecuteAsync(code, "net9.0");
// Expected: High reliability, latest language support

// netstandard2.0 - Cross-platform compatibility
var result = await refactoring.ExecuteAsync(code, "netstandard2.0");
// Expected: Good reliability, C# 7.3 features
```

**Limited Support Frameworks (Use with Caution)**:
```csharp
// net48 - May fail due to reference assembly issues
var result = await refactoring.ExecuteAsync(code, "net48");
// Expected: May succeed or fail depending on environment
// Recommendation: Use net8.0 instead if possible

// net35 - Legacy framework, limited features
var result = await refactoring.ExecuteAsync(code, "net35");
// Expected: C# 3.0 features only, may have assembly issues
// Recommendation: Upgrade to modern .NET if possible
```

### Handling Framework Errors Gracefully

**Example - Robust Error Handling**:
```csharp
public async Task<string> RefactorWithFallback(
    string sourceCode,
    int lineNumber,
    int columnNumber)
{
    // Try modern framework first
    var refactoring = new InlineVariable();
    var result = await refactoring.ExecuteAsync(
        sourceCode, lineNumber, columnNumber, "net8.0");

    if (result.IsSuccess)
    {
        return result.RefactoredCode!;
    }

    // Log the error but don't fail
    Console.WriteLine($"Warning: Refactoring failed: {result.ErrorMessage}");
    Console.WriteLine("Returning original code");

    // Return original code if refactoring fails
    return sourceCode;
}
```

**Example - Framework Detection and Selection**:
```csharp
public string SelectBestFramework(string projectFramework)
{
    // Map project frameworks to best refactoring framework
    return projectFramework switch
    {
        "net9.0" => "net9.0",           // Use exact match
        "net8.0" => "net8.0",           // Use exact match
        "net48" or "net481" => "net8.0",  // Use modern framework
        "net472" or "net471" => "net8.0", // Use modern framework
        "netstandard2.0" => "netstandard2.0", // Use exact match
        "netstandard2.1" => "netstandard2.1", // Use exact match
        _ => "net8.0"                   // Default to net8.0
    };
}

// Usage
var targetFramework = SelectBestFramework("net48");
// Returns "net8.0" - refactor with modern framework
```

### Best Practices for Framework Compatibility

1. **Prefer Modern Frameworks**: Use net8.0 or net9.0 for most reliable refactorings
2. **Test Framework Support**: Before batch operations, test one refactoring with target framework
3. **Have Fallback Strategy**: Be prepared to return original code if refactoring fails
4. **Cache Management**: Keep modern framework caches (net8.0, net9.0) always populated
5. **Log Failures**: Track framework-specific failures to identify patterns
6. **Document Limitations**: Inform users about framework-specific limitations in your application

## Inline Method (Part 1)

The Inline Method refactoring replaces a method's single invocation with its body, then removes the method declaration. Part 1 supports void methods with simple parameters (primitives, string) and single caller only.

**See also:** [Extract Method](#extract-method) for the inverse operation - extracting code into a new method.

### Example 1: Simple Method Inlining

**Before:**
```csharp
public class Logger
{
    public void ProcessLog(string message)
    {
        WriteToConsole(message);
    }

    private void WriteToConsole(string msg)
    {
        Console.WriteLine(msg);
    }
}
```

**After:**
```csharp
public class Logger
{
    public void ProcessLog(string message)
    {
        Console.WriteLine(message);
    }
}
```

### Example 2: Method with Parameters

**Before:**
```csharp
public class Calculator
{
    public void DisplayResult()
    {
        PrintSum(10, 20);
    }

    private void PrintSum(int a, int b)
    {
        var result = a + b;
        Console.WriteLine($"Sum: {result}");
    }
}
```

**After:**
```csharp
public class Calculator
{
    public void DisplayResult()
    {
        var result = 10 + 20;
        Console.WriteLine($"Sum: {result}");
    }
}
```

### Example 3: Comment Preservation

**Before:**
```csharp
public class Service
{
    public void Run()
    {
        // Initialize system
        Initialize();
    }

    /// <summary>
    /// Performs system initialization
    /// </summary>
    private void Initialize()
    {
        // Load configuration
        LoadConfig();
    }
}
```

**After:**
```csharp
public class Service
{
    public void Run()
    {
        // Initialize system
        // Load configuration
        LoadConfig();
    }
}
```

### MCP Tool Usage

**inline_method Tool**
```json
{
  "tool": "inline_method",
  "parameters": {
    "sourceCode": "public class Test { public void Caller() { Helper(); } private void Helper() { Console.WriteLine(\"Work\"); } }",
    "lineNumber": 1,
    "columnNumber": 65,
    "targetFramework": "net8.0"
  }
}
```

### Part 1 Limitations

- **Void methods only**: Methods with return values not supported
- **Single caller required**: Method must be called exactly once
- **Simple parameters only**: Primitives (int, string, bool, etc.) and string supported; no ref/out, no complex types
- **No recursive methods**: Recursive methods cannot be inlined
- **No virtual/abstract methods**: Virtual, abstract, and override methods cannot be inlined

### Framework-Aware Validation

The Inline Method refactoring uses framework-aware validation to ensure the refactored code is compatible with your target framework:

```csharp
var inliner = new InlineMethod();

// .NET 8 - Modern C# features supported
var result = await inliner.ExecuteAsync(sourceCode, lineNumber, columnNumber, "net8.0");

// .NET Framework 4.8 - Validates against C# 7.3 features
var result48 = await inliner.ExecuteAsync(sourceCode, lineNumber, columnNumber, "net48");
```

## Diagnostic Integration (V1.5)

RefactorCsharpMCP provides diagnostic analysis capabilities that enable AI agents to detect code issues and automatically apply fixes using the **analyze → suggest → fix** workflow.

### Example 1: Detect and Fix Unused Usings

**Step 1: Analyze Code**
```csharp
var analyzer = new DiagnosticAnalyzer();
var sourceCode = @"
using System;
using System.Linq;  // Unused
using System.Collections.Generic;  // Unused

public class Calculator
{
    public int Add(int a, int b)
    {
        Console.WriteLine($""Adding {a} + {b}"");
        return a + b;
    }
}";

var result = await analyzer.AnalyzeCodeAsync(sourceCode, "net8.0");

// result.Success == true
// result.Diagnostics contains diagnostic information
// result.Summary.TotalDiagnostics > 0
```

**Step 2: Review Diagnostics**
```csharp
foreach (var diagnostic in result.Diagnostics)
{
    Console.WriteLine($"{diagnostic.Id}: {diagnostic.Message}");
    Console.WriteLine($"Location: Line {diagnostic.Location.Line}, Column {diagnostic.Location.Column}");
    Console.WriteLine($"Category: {diagnostic.Category}");
    Console.WriteLine($"Applicable Refactorings: {string.Join(", ", diagnostic.ApplicableRefactorings)}");
}

// Output:
// IDE0005: Using directive is unnecessary
// Location: Line 3, Column 1
// Category: Style
// Applicable Refactorings: remove_unused_usings
```

**Step 3: Apply Fix**
```csharp
var fixResult = await new RemoveUnusedUsings().ExecuteAsync(sourceCode, "net8.0");

// fixResult.IsSuccess == true
// fixResult.RefactoredCode == @"
// using System;
//
// public class Calculator
// {
//     public int Add(int a, int b)
//     {
//         Console.WriteLine($""Adding {a} + {b}"");
//         return a + b;
//     }
// }";
```

### Example 2: Detect and Fix Readonly Fields

**Step 1: Analyze Code**
```csharp
var analyzer = new DiagnosticAnalyzer();
var sourceCode = @"
public class Service
{
    private string _apiKey;
    private int _timeout;

    public Service()
    {
        _apiKey = ""abc123"";
        _timeout = 30;
    }

    public void CallApi()
    {
        Console.WriteLine($""Calling API with key: {_apiKey}"");
    }
}";

var result = await analyzer.AnalyzeCodeAsync(sourceCode, "net8.0", DiagnosticSeverity.Info);
```

**Step 2: Filter for Readonly Suggestions**
```csharp
var readonlyDiagnostics = result.Diagnostics
    .Where(d => d.Id == "IDE0044")
    .ToList();

foreach (var diagnostic in readonlyDiagnostics)
{
    Console.WriteLine($"Field at line {diagnostic.Location.Line} can be made readonly");
    Console.WriteLine($"Suggested fix: {string.Join(", ", diagnostic.ApplicableRefactorings)}");
}
```

**Step 3: Apply Fixes**
```csharp
var refactoring = new MakeFieldReadonly();
var step1 = await refactoring.ExecuteAsync(sourceCode, "Service", "_apiKey", "net8.0");
var step2 = await refactoring.ExecuteAsync(step1.RefactoredCode!, "Service", "_timeout", "net8.0");

// step2.RefactoredCode contains:
// private readonly string _apiKey;
// private readonly int _timeout;
```

### Example 3: Complete Analyze → Fix Workflow

**AI Agent Workflow**
```csharp
// 1. AI Agent analyzes code
var analyzer = new DiagnosticAnalyzer();
var analysisResult = await analyzer.AnalyzeCodeAsync(userCode, "net8.0");

// 2. AI Agent presents findings to user
Console.WriteLine($"Found {analysisResult.Summary.TotalDiagnostics} issues:");
Console.WriteLine($"  - {analysisResult.Summary.ErrorCount} errors");
Console.WriteLine($"  - {analysisResult.Summary.WarningCount} warnings");
Console.WriteLine($"  - {analysisResult.Summary.InfoCount} suggestions");

// 3. AI Agent suggests fixes
foreach (var diagnostic in analysisResult.Diagnostics)
{
    if (diagnostic.ApplicableRefactorings.Any())
    {
        Console.WriteLine($"\n{diagnostic.Id}: {diagnostic.Message}");
        Console.WriteLine($"I can fix this using: {string.Join(", ", diagnostic.ApplicableRefactorings)}");
    }
}

// 4. User approves: "Yes, fix them all"

// 5. AI Agent applies fixes
var fixedCode = userCode;
foreach (var diagnostic in analysisResult.Diagnostics.Where(d => d.ApplicableRefactorings.Any()))
{
    if (diagnostic.Id == "IDE0005" || diagnostic.Id == "CS8019")
    {
        var result = await new RemoveUnusedUsings().ExecuteAsync(fixedCode, "net8.0");
        if (result.IsSuccess)
            fixedCode = result.RefactoredCode!;
    }
    else if (diagnostic.Id == "IDE0044")
    {
        // Extract field information and apply fix
        // (implementation details omitted for brevity)
    }
}

// 6. AI Agent reports results
Console.WriteLine("✅ Applied all fixes successfully!");
Console.WriteLine($"Code improved: {originalLineCount} → {newLineCount} lines");
```

### Example 4: Framework-Specific Analysis

Different frameworks report different diagnostics based on their C# language version support:

```csharp
var analyzer = new DiagnosticAnalyzer();
var code = @"
using System;

public class Test
{
    public void Method()
    {
        Console.WriteLine(""Test"");
    }
}";

// .NET 8 (C# 12) - May suggest modern language features
var net8Result = await analyzer.AnalyzeCodeAsync(code, "net8.0");

// .NET Framework 4.8 (C# 7.3) - Different diagnostic rules
var net48Result = await analyzer.AnalyzeCodeAsync(code, "net48");

// .NET Framework 3.5 (C# 3.0) - Older language rules
var net35Result = await analyzer.AnalyzeCodeAsync(code, "net35");
```

### Supported Diagnostic IDs

| Diagnostic ID | Description | Applicable Refactoring | Framework |
|--------------|-------------|----------------------|-----------|
| IDE0005 | Using directive is unnecessary | remove_unused_usings | All |
| CS8019 | Unnecessary using directive | remove_unused_usings | All |
| IDE0044 | Add readonly modifier | make_field_readonly | All |
| IDE0058 | Expression value never used | inline_variable (future) | All |
| IDE0059 | Unnecessary value assignment | inline_variable (future) | All |
| IDE0022 | Use expression body for method | inline_method (future) | C# 6.0+ |

### MCP Tool Usage

When using RefactorCsharpMCP through the MCP protocol:

**analyze_code Tool**
```json
{
  "tool": "analyze_code",
  "parameters": {
    "sourceCode": "using System.Linq;\npublic class Test { }",
    "targetFramework": "net8.0",
    "minSeverity": "Info"
  }
}
```

**fix_diagnostic Tool**
```json
{
  "tool": "fix_diagnostic",
  "parameters": {
    "sourceCode": "using System.Linq;\npublic class Test { }",
    "diagnosticId": "IDE0005",
    "line": 1,
    "column": 1,
    "targetFramework": "net8.0"
  }
}
```

### Best Practices for Diagnostic Integration

1. **Analyze First**: Always run analyze_code before attempting fixes
2. **Filter by Severity**: Use minSeverity to focus on important issues
3. **Check Applicable Refactorings**: Not all diagnostics have automated fixes
4. **Framework Awareness**: Different frameworks may report different diagnostics
5. **Batch Fixes Carefully**: Apply one fix at a time and re-analyze to avoid conflicts
6. **Handle Errors Gracefully**: Some diagnostics may not be fixable automatically
