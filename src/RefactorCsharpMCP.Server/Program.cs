using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using RefactorCsharpMCP.Core.Validation.Handlers;
using RefactorCsharpMCP.Server.Configuration;
using RefactorCsharpMCP.Server.Formatting;
using RefactorCsharpMCP.Toon;

var builder = Host.CreateApplicationBuilder(args);

// Load output format configuration (env var + CLI args)
// Supports: REFACTOR_CSHARP_OUTPUT_FORMAT=toon or --output-format toon
var outputFormatOptions = OutputFormatConfiguration.Load(args);

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

// Register diagnostic handlers for SyntaxValidator (Strategy Pattern)
// Handlers are registered as singletons for performance (stateless, reusable)
builder.Services.AddSingleton<IParseDiagnosticHandler, ParseDiagnosticHandler>();
builder.Services.AddSingleton<ISemanticDiagnosticHandler, SemanticDiagnosticHandler>();

// Register TOON encoder and response formatter (Issue #145)
// Default: JSON format (pass-through), TOON format requires explicit opt-in
builder.Services.AddSingleton(outputFormatOptions);
builder.Services.AddSingleton<IToonEncoder, ToonEncoder>();
if (outputFormatOptions.IsToonEnabled)
{
    builder.Services.AddSingleton<IResponseFormatter, ToonResponseFormatter>();
}
else
{
    builder.Services.AddSingleton<IResponseFormatter, JsonResponseFormatter>();
}

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
