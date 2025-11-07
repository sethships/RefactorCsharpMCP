using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

var builder = Host.CreateApplicationBuilder(args);

// Configure logging
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.SetMinimumLevel(LogLevel.Information);

// Get server version from assembly for metadata
var version = Assembly.GetExecutingAssembly()
    .GetName()
    .Version?
    .ToString() ?? "1.0.0";

// Configure MCP server with stdio transport
// Server name: refactor-csharp-mcp
// Server version: extracted from assembly
// Capabilities: Tools (11 refactoring tools available via WithToolsFromAssembly)
//               Resources: Not supported
//               Prompts: Not supported
builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithToolsFromAssembly();

var app = builder.Build();

// Run the MCP server
await app.RunAsync();
