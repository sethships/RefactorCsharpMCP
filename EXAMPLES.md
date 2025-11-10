# RefactorCsharpMCP Examples

This document provides practical examples of using RefactorCsharpMCP's refactoring capabilities.

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

## 4. Extract Class

Extract Class refactoring helps decompose large classes by moving fields and methods into a new class, following the composition pattern. The refactoring automatically updates references within the same class and warns about external references that need manual updates.

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

### Best Practices

1. **Group related fields and methods** - Extract cohesive groups that represent a single concept (e.g., all address-related members).
2. **Use descriptive class names** - The new class name should clearly describe what it represents (`Address`, `Configuration`, `Credentials`, `MethodResolver`).
3. **Service Class Pattern** - Extract methods-only to create stateless service classes that encapsulate related logic.
4. **Handle external references** - Always review and update external references after the refactoring completes.
5. **Test after refactoring** - Run your tests to ensure all references were updated correctly.
6. **Consider partial classes** - References in all parts of a partial class are automatically updated.
7. **Encapsulation** - After extraction, consider making extracted fields private and adding public properties/methods as needed.

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
