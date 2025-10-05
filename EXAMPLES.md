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
