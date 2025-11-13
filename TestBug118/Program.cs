using System;
using System.IO;
using System.Linq;
using RefactorCsharpMCP.Core.Refactorings;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

// Test nested type extraction (failing test scenario)
var sourceCode = @"public class Container
{
    public class Config
    {
        public string Setting { get; set; }
    }

    private Config _config;

    public void Initialize()
    {
        _config = new Config();
    }
}";

Console.WriteLine($"Testing nested type extraction...");

var refactoring = new ExtractClass();
var result = refactoring.Execute(
    sourceCode,
    className: "Container",
    newClassName: "Configuration",
    fieldNames: null,
    methodNames: null,
    nestedTypeNames: "Config");

Console.WriteLine($"\n=== Result ===");
Console.WriteLine($"Success: {result.IsSuccess}");
Console.WriteLine($"Message: {result.Message}");

if (!result.IsSuccess)
{
    Console.WriteLine($"Error: {result.ErrorMessage}");
    Environment.Exit(1);
}

Console.WriteLine($"\n=== SUCCESS ===");
Console.WriteLine(result.RefactoredCode);
Environment.Exit(0);
