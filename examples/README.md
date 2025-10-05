# RefactorCsharpMCP Examples

This directory contains comprehensive examples demonstrating all refactoring capabilities of RefactorCsharpMCP.

## Available Refactorings

1. [Extract Method](#extract-method) - Extract code blocks into new methods
2. [Constructor Injection](#constructor-injection) - Convert method parameters to constructor-injected dependencies
3. [Make Field Readonly](#make-field-readonly) - Convert fields to readonly when only assigned in constructors
4. [Safe Delete](#safe-delete) - Safely delete methods after verifying no references
5. [Extract Class](#extract-class) - Extract fields and methods into a new class

## Extract Method

Extract a block of code into a new private method.

### Before:
```csharp
public class OrderProcessor
{
    public decimal CalculateTotal(Order order)
    {
        decimal subtotal = 0;
        foreach (var item in order.Items)
        {
            subtotal += item.Price * item.Quantity;
        }

        decimal tax = subtotal * 0.08m;
        decimal shipping = subtotal > 100 ? 0 : 10;

        return subtotal + tax + shipping;
    }
}
```

### MCP Tool Usage:
```json
{
  "sourceCode": "...",
  "startLine": 6,
  "endLine": 9,
  "newMethodName": "CalculateSubtotal"
}
```

### After:
```csharp
public class OrderProcessor
{
    public decimal CalculateTotal(Order order)
    {
        decimal subtotal = CalculateSubtotal(order);

        decimal tax = subtotal * 0.08m;
        decimal shipping = subtotal > 100 ? 0 : 10;

        return subtotal + tax + shipping;
    }

    private decimal CalculateSubtotal(Order order)
    {
        decimal subtotal = 0;
        foreach (var item in order.Items)
        {
            subtotal += item.Price * item.Quantity;
        }
        return subtotal;
    }
}
```

## Constructor Injection

Convert method parameters to constructor-injected fields or properties.

### Before:
```csharp
public class UserService
{
    public void ProcessUser(ILogger logger, IDatabase database, string userId)
    {
        logger.Log($"Processing user {userId}");
        var user = database.GetUser(userId);
        // ... process user
    }
}
```

### MCP Tool Usage:
```json
{
  "sourceCode": "...",
  "className": "UserService",
  "methodName": "ProcessUser",
  "parameterNames": "logger, database",
  "useProperties": false
}
```

### After:
```csharp
public class UserService
{
    private readonly ILogger _logger;
    private readonly IDatabase _database;

    public UserService(ILogger logger, IDatabase database)
    {
        _logger = logger;
        _database = database;
    }

    public void ProcessUser(string userId)
    {
        _logger.Log($"Processing user {userId}");
        var user = _database.GetUser(userId);
        // ... process user
    }
}
```

## Make Field Readonly

Convert fields to readonly when they're only assigned in constructors.

### Before:
```csharp
public class ConfigurationManager
{
    private string _connectionString;
    private int _timeout;

    public ConfigurationManager(string connectionString, int timeout)
    {
        _connectionString = connectionString;
        _timeout = timeout;
    }

    public string GetConnectionString() => _connectionString;
}
```

### MCP Tool Usage:
```json
{
  "sourceCode": "...",
  "className": "ConfigurationManager",
  "fieldName": "_connectionString"
}
```

### After:
```csharp
public class ConfigurationManager
{
    private readonly string _connectionString;
    private int _timeout;

    public ConfigurationManager(string connectionString, int timeout)
    {
        _connectionString = connectionString;
        _timeout = timeout;
    }

    public string GetConnectionString() => _connectionString;
}
```

## Safe Delete

Safely delete methods after verifying they have no references.

### Before:
```csharp
public class MathHelper
{
    public int Add(int a, int b) => a + b;

    public int Subtract(int a, int b) => a - b;

    // This method is never used
    private int ObsoleteCalculation(int x) => x * 2 + 1;
}
```

### MCP Tool Usage:
```json
{
  "sourceCode": "...",
  "className": "MathHelper",
  "methodName": "ObsoleteCalculation"
}
```

### After:
```csharp
public class MathHelper
{
    public int Add(int a, int b) => a + b;

    public int Subtract(int a, int b) => a - b;
}
```

**Note:** Safe Delete will fail if the method has any references within the same file.

## Extract Class

Extract fields and methods into a new class using composition.

### Before:
```csharp
public class Employee
{
    private string _name;
    private string _email;
    private string _street;
    private string _city;
    private string _zipCode;

    public string GetFullAddress()
    {
        return $"{_street}, {_city} {_zipCode}";
    }

    public void UpdateAddress(string street, string city, string zipCode)
    {
        _street = street;
        _city = city;
        _zipCode = zipCode;
    }
}
```

### MCP Tool Usage:
```json
{
  "sourceCode": "...",
  "className": "Employee",
  "newClassName": "Address",
  "fieldNames": "_street, _city, _zipCode",
  "methodNames": "GetFullAddress, UpdateAddress"
}
```

### After:
```csharp
public class Employee
{
    private string _name;
    private string _email;
    private Address _address;
}

public class Address
{
    private string _street;
    private string _city;
    private string _zipCode;

    public string GetFullAddress()
    {
        return $"{_street}, {_city} {_zipCode}";
    }

    public void UpdateAddress(string street, string city, string zipCode)
    {
        _street = street;
        _city = city;
        _zipCode = zipCode;
    }
}
```

**Important:** You must manually update all references to extracted members to use the new class instance.

## Testing Examples

All examples have been tested with RefactorCsharpMCP. See the [integration tests](../src/RefactorCsharpMCP.Tests/Integration/) for automated validation against real DevTools projects.

## Best Practices

1. **Start Small**: Test refactorings on small code sections first
2. **Version Control**: Always commit before refactoring
3. **Run Tests**: Verify functionality after each refactoring
4. **Review Changes**: Inspect the refactored code before accepting
5. **Incremental**: Apply refactorings one at a time for easier rollback

## Common Use Cases

### Code Cleanup
- Use **Safe Delete** to remove unused methods
- Use **Make Field Readonly** to improve immutability

### Dependency Injection
- Use **Constructor Injection** to implement DI pattern
- Combine with **Extract Class** for better separation of concerns

### Code Organization
- Use **Extract Method** to reduce method complexity
- Use **Extract Class** to split large classes

## Troubleshooting

### "Method is referenced" error
- **Safe Delete** detects method usage - remove all references first
- Note: Only checks single-file references

### "Field is assigned outside constructor" error
- **Make Field Readonly** requires fields only assigned in constructors
- Move assignments to constructor or keep field mutable

### "Manual updates required" warning
- **Extract Class** creates new class but doesn't update references
- Update all usages manually to use the new class instance

## Additional Resources

- [Project Documentation](../README.md)
- [API Reference](../docs/api-reference.md) (coming soon)
- [Troubleshooting Guide](../docs/TROUBLESHOOTING.md)
