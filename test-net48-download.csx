#!/usr/bin/env dotnet-script
#r "src/RefactorCsharpMCP.Core/bin/Debug/net8.0/RefactorCsharpMCP.Core.dll"
#r "nuget: Microsoft.Extensions.Logging.Console, 9.0.9"

using RefactorCsharpMCP.Core.Infrastructure.FrameworkSupport;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

// Create logger to see what's happening
using var loggerFactory = LoggerFactory.Create(builder =>
{
    builder.AddConsole();
    builder.SetMinimumLevel(LogLevel.Information);
});

var logger = loggerFactory.CreateLogger("Test");

Console.WriteLine("Testing net48 reference assembly download...\n");

try
{
    var resolver = new ReferenceAssemblyResolver(logger);
    Console.WriteLine("ReferenceAssemblyResolver created successfully");

    Console.WriteLine("Attempting to download net48 assemblies...");
    var references = await resolver.GetReferenceAssembliesAsync("net48");

    Console.WriteLine($"\n✅ SUCCESS! Downloaded {references.Count} reference assemblies for net48");
    resolver.Dispose();
}
catch (Exception ex)
{
    Console.WriteLine($"\n❌ FAILED: {ex.GetType().Name}");
    Console.WriteLine($"Message: {ex.Message}");
    if (ex.InnerException != null)
    {
        Console.WriteLine($"Inner Exception: {ex.InnerException.Message}");
    }
    Console.WriteLine($"\nStack Trace:\n{ex.StackTrace}");
}
