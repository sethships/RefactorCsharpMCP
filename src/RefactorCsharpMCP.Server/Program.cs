using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

var builder = Host.CreateApplicationBuilder(args);

// Configure logging
// NOTE: Console logging is disabled for stdio transport to prevent corrupting JSON-RPC messages
// MCP stdio transport requires stdout exclusively for JSON-RPC; logs would corrupt the message stream
builder.Logging.ClearProviders();
// Set minimum level to Warning to capture errors while avoiding verbose Info/Debug noise
builder.Logging.SetMinimumLevel(LogLevel.Warning);

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
