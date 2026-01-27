# Real-World RefactorCsharpMCP Examples

This document shows real examples of how RefactorCsharpMCP tools can be used to improve C# codebases through automated refactoring.

## Example Project: Console Application Refactoring

The following examples demonstrate common refactoring patterns applied to a typical console application codebase.

**Impact**: Reduced ~200+ lines through method extraction, improved maintainability

## Refactoring #1: MenuController.BuildCurrentOptions()

### Problem
Large method with 55+ lines of repetitive code for getting menu items, type casting, and defensive assertions.

### Before (Simplified)
```csharp
private Options BuildCurrentOptions()
{
    // Extract values from menu items by label
    var lengthItem = GetItemByLabel("Length") as NumericMenuItem;
    var countItem = GetItemByLabel("Count") as NumericMenuItem;
    var uppersItem = GetItemByLabel("Uppercase (A-Z)") as CheckboxMenuItem;
    // ... 10 more item extractions ...

    // Defensive assertions for each item
    Debug.Assert(GetItemByLabel("Length") == null || lengthItem != null,
        "Length item exists but is not NumericMenuItem");
    Debug.Assert(GetItemByLabel("Count") == null || countItem != null,
        "Count item exists but is not NumericMenuItem");
    // ... 10 more assertions ...

    return new Options
    {
        Length = lengthItem?.Value ?? 12,
        Count = countItem?.Value ?? 1,
        NoUppers = !(uppersItem?.IsChecked ?? true),
        // ... more properties ...
    };
}
```

### After (Using extract_method)
```csharp
private Options BuildCurrentOptions()
{
    return new Options
    {
        Length = GetNumericValue("Length", 12),
        Count = GetNumericValue("Count", 1),
        NoUppers = !GetCheckboxValue("Uppercase (A-Z)", true),
        NoLowers = !GetCheckboxValue("Lowercase (a-z)", true),
        NoNumbers = !GetCheckboxValue("Numbers (0-9)", true),
        NoSpecials = !GetCheckboxValue("Special characters", true),
        MinUppercase = GetNumericValue("Min Uppercase", 1),
        MinLowercase = GetNumericValue("Min Lowercase", 1),
        MinDigits = GetNumericValue("Min Digits", 1),
        MinSpecials = GetNumericValue("Min Specials", 1),
        AllowedSpecials = GetStringValue("Allowed Specials")
    };
}

private int GetNumericValue(string label, int defaultValue)
{
    var item = GetItemByLabel(label) as NumericMenuItem;
    Debug.Assert(GetItemByLabel(label) == null || item != null,
        $"{label} item exists but is not NumericMenuItem");
    return item?.Value ?? defaultValue;
}

private bool GetCheckboxValue(string label, bool defaultValue)
{
    var item = GetItemByLabel(label) as CheckboxMenuItem;
    Debug.Assert(GetItemByLabel(label) == null || item != null,
        $"{label} item exists but is not CheckboxMenuItem");
    return item?.IsChecked ?? defaultValue;
}

private string GetStringValue(string label)
{
    var item = GetItemByLabel(label) as StringMenuItem;
    return string.IsNullOrWhiteSpace(item?.Value) ? null : item.Value.Trim();
}
```

### Results
- **Before**: 55 lines
- **After**: 15 lines + 3 reusable helper methods
- **Benefit**: Eliminated repetitive GetItemByLabel() calls and type casting
- **Pattern**: Extract repeated code into parameterized helper methods

---

## Refactoring #2: Program.DisplayEnhancedHelp()

### Problem
Monolithic 84-line method displaying help text, mixing multiple concerns.

### Before (Simplified)
```csharp
static void DisplayEnhancedHelp()
{
    Console.WriteLine("USAGE:");
    Console.WriteLine("  passgen [options]");
    Console.WriteLine();
    Console.WriteLine("OPTIONS:");
    Console.WriteLine("  -l, --length <n>     Password length...");
    // ... 20+ lines of options ...
    Console.WriteLine();
    Console.WriteLine("EXAMPLES:");
    Console.WriteLine("  passgen                # Generate default...");
    // ... 10+ lines of examples ...
    Console.WriteLine("PASSPHRASE EXAMPLES:");
    // ... 15+ lines of passphrase examples ...
    Console.WriteLine("COMPLIANCE EXAMPLES:");
    // ... 10+ lines of compliance examples ...
    Console.WriteLine("CHARACTER SETS:");
    // ... 8+ lines of character sets ...
    Console.WriteLine("SECURITY:");
    // ... 15+ lines of security info ...
}
```

### After (Using extract_method)
```csharp
static void DisplayEnhancedHelp()
{
    Console.WriteLine("USAGE:");
    Console.WriteLine("  passgen [options]");
    Console.WriteLine();
    DisplayOptionsSection();
    DisplayExamplesSection();
    DisplayPassphraseExamplesSection();
    DisplayComplianceExamplesSection();
    DisplayCharacterSetsSection();
    DisplaySecuritySection();
}

static void DisplayOptionsSection()
{
    Console.WriteLine("OPTIONS:");
    Console.WriteLine("  -l, --length <n>     Password length...");
    // ... options content ...
}

static void DisplayExamplesSection()
{
    Console.WriteLine("EXAMPLES:");
    Console.WriteLine("  passgen                # Generate default...");
    // ... examples content ...
}

// ... 4 more section methods ...
```

### Results
- **Before**: 84 lines in one method
- **After**: 11 lines + 6 focused section methods
- **Benefit**: Improved testability and readability, clear separation of concerns
- **Pattern**: Extract logical sections into separate methods

---

## Refactoring #3: OptionsValidator.Validate()

### Problem
~100-line validation method mixing different validation concerns.

### Before (Simplified)
```csharp
public static ValidationResult Validate(Options options)
{
    List<string> errors = new();

    // Basic validation
    if (options.Length < 1) { errors.Add("..."); }
    if (options.Count < 1) { errors.Add("..."); }

    // Character type conflict validation
    if (options.NoUppers && options.MinUppercase > 0) { errors.Add("..."); }
    if (options.NoLowers && options.MinLowercase > 0) { errors.Add("..."); }
    // ... more conflict checks ...

    // Minimum requirements validation
    int minRequired = 0;
    if (!options.NoUppers) minRequired += options.MinUppercase;
    // ... calculate minimum ...
    if (options.Length < minRequired) { errors.Add("..."); }

    // Passphrase validation
    if (options.Passphrase)
    {
        if (options.WordCount < 1) { errors.Add("..."); }
        // ... 30+ lines of passphrase validation ...
    }

    return errors.Count > 0 ? ValidationResult.Error(errors) : ValidationResult.Success();
}
```

### After (Using extract_method)
```csharp
public static ValidationResult Validate(Options options)
{
    List<string> errors = new();

    // Basic validation
    if (options.Length < 1) { errors.Add("..."); }
    if (options.Count < 1) { errors.Add("..."); }

    // Validate character type conflicts and minimum requirements
    ValidateCharacterTypeConflicts(options, errors);
    ValidateMinimumRequirements(options, errors);

    // Validate passphrase mode if applicable
    if (options.Passphrase)
    {
        ValidatePassphraseOptions(options, errors);
    }

    return errors.Count > 0 ? ValidationResult.Error(errors) : ValidationResult.Success();
}

private static void ValidateCharacterTypeConflicts(Options options, List<string> errors)
{
    if (options.NoUppers && options.MinUppercase > 0)
        errors.Add("Cannot set --min-uppers when --no-uppers is enabled.");
    // ... conflict validation logic ...
}

private static void ValidateMinimumRequirements(Options options, List<string> errors)
{
    int minRequired = 0;
    if (!options.NoUppers) minRequired += options.MinUppercase;
    // ... minimum calculation and validation ...
}

private static void ValidatePassphraseOptions(Options options, List<string> errors)
{
    if (options.WordCount < 1)
        errors.Add("Word count must be at least 1 for passphrase mode.");
    // ... passphrase validation logic ...
}
```

### Results
- **Before**: ~100 lines in one method
- **After**: ~40 lines + 3 focused validation methods
- **Benefit**: Better separation of validation concerns, easier to test individual validations
- **Pattern**: Extract validation logic by concern/category

---

## Refactoring #4: PasswordGenerator Constructor

### Problem
Constructor with complex logic for determining special characters and validating requirements.

### Solution
- Extracted `DetermineSpecialCharacters()` method
- Extracted `ValidateMinimumRequirements()` method
- Simplified constructor logic
- Enhanced security-critical code readability

---

## Key Takeaways

### When to Use extract_method

1. **Repetitive Patterns**: MenuController had the same pattern repeated 10+ times
2. **Long Methods**: DisplayEnhancedHelp was 84 lines doing multiple things
3. **Mixed Concerns**: OptionsValidator mixed different validation types
4. **Complex Logic**: PasswordGenerator constructor had multiple responsibilities

### Best Practices Demonstrated

1. **Create Helper Methods**: Extract repeated patterns into parameterized helpers
2. **Separate Concerns**: Split methods that do multiple things into focused methods
3. **Improve Testability**: Smaller methods are easier to unit test
4. **Preserve Behavior**: All 207 tests still pass after refactoring
5. **Use Meaningful Names**: Method names clearly describe what they do

### Tool Usage Pattern

The `extract_method` tool was used to:
1. Identify code blocks to extract (by line range)
2. Generate method signature with appropriate parameters
3. Replace original code with method call
4. Create the extracted method with proper scope (private)

### Impact Metrics

- **Total Lines Reduced**: ~200+ lines
- **Methods Created**: 12 new helper methods
- **Code Duplication**: Significantly reduced
- **Test Status**: All 207 tests passing ✓
- **Maintainability**: Significantly improved

---

## How to Apply These Patterns

### Using the MCP Tools

In your Claude Code session:

```
Please extract lines 50-75 of MyClass.cs into a method called ValidateInputs
```

Claude will use the `mcp__MCP_DOCKER__extract_method` tool to:
1. Analyze the code block
2. Determine parameters needed
3. Detect return type
4. Generate the extraction
5. Replace the original code with a call

### Example Request Patterns

**Pattern 1 - Repetitive Code**:
```
This method has the same pattern repeated 10 times. Can you extract
the common logic into a helper method?
```

**Pattern 2 - Long Method**:
```
This 100-line method does too much. Can you break it down into
smaller focused methods?
```

**Pattern 3 - Mixed Concerns**:
```
This validation method mixes different concerns. Can you extract
separate validation methods for each concern?
```

---

## References

- **RefactorCsharpMCP Repository**: https://github.com/sethb75/RefactorCsharpMCP
- **MCP Specification**: https://modelcontextprotocol.io/
- **Roslyn Documentation**: https://docs.microsoft.com/en-us/dotnet/csharp/roslyn-sdk/
