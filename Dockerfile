# Multi-stage Dockerfile for RefactorCsharpMCP Server
# Optimized for Model Context Protocol (MCP) with stdio transport
#
# Security: SHA256 pinning ensures image integrity. Consider implementing Docker Content Trust (DCT)
# for signature verification in production environments: export DOCKER_CONTENT_TRUST=1

# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:8.0@sha256:ff8311847c54c04d1a14c488362807997d59b61372da5095a95f89cbcda7f9b7 AS build
WORKDIR /src

# Copy solution and project files
COPY RefactorCsharpMCP.sln ./
COPY src/RefactorCsharpMCP.Server/RefactorCsharpMCP.Server.csproj ./src/RefactorCsharpMCP.Server/
COPY src/RefactorCsharpMCP.Core/RefactorCsharpMCP.Core.csproj ./src/RefactorCsharpMCP.Core/
COPY src/RefactorCsharpMCP.Tests/RefactorCsharpMCP.Tests.csproj ./src/RefactorCsharpMCP.Tests/
COPY src/RefactorCsharpMCP.Benchmarks/RefactorCsharpMCP.Benchmarks.csproj ./src/RefactorCsharpMCP.Benchmarks/

# Restore dependencies
RUN dotnet restore

# Copy source code
COPY src/ ./src/

# Build and publish
WORKDIR /src/src/RefactorCsharpMCP.Server
RUN dotnet publish -c Release -o /app/publish --no-restore

# Stage 2: Runtime
FROM mcr.microsoft.com/dotnet/runtime:8.0@sha256:e9aabde56bb3e55d416d4e926032af75fed831da70bb428d556120ee2649f8b0 AS runtime
WORKDIR /app

# Security: Add non-root user
RUN adduser --disabled-password --gecos '' --uid 1000 mcpuser && \
    chown -R mcpuser:mcpuser /app

# Copy published application with proper ownership
COPY --from=build --chown=mcpuser:mcpuser /app/publish .

# MCP servers use stdio transport - no ports needed
# The container communicates via stdin/stdout

# Security labels
LABEL security.scan="required" \
      security.compliance="docker-mcp-toolkit" \
      org.opencontainers.image.title="RefactorCsharp MCP Server" \
      org.opencontainers.image.description="Roslyn-based C# refactoring for AI clients" \
      org.opencontainers.image.version="1.0.0" \
      org.opencontainers.image.source="https://github.com/sethb75/RefactorCsharpMCP" \
      org.opencontainers.image.vendor="RefactorCsharpMCP" \
      org.opencontainers.image.licenses="Apache-2.0"

# Set environment for MCP operation
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1 \
    DOTNET_RUNNING_IN_CONTAINER=true

# Basic health check - verifies process is running
HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=3 \
  CMD test -d /proc/1 || exit 1

# Run as non-root user
USER mcpuser

# Run the MCP server
ENTRYPOINT ["dotnet", "RefactorCsharpMCP.Server.dll"]
