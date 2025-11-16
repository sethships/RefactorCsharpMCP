# Development Environment Setup

This guide will help you set up your development environment for RefactorCsharpMCP.

## Prerequisites

### Required Software

1. **.NET 8 SDK** (Required)
   - Download: https://dotnet.microsoft.com/download/dotnet/8.0
   - Verify installation: `dotnet --version` (should show 8.0.x)

2. **Git** (Required)
   - Already installed if you cloned this repository

3. **IDE/Editor** (Recommended)
   - **Visual Studio 2022** (Windows) - Full IDE with debugging support
   - **Visual Studio Code** with C# extension (Cross-platform)
   - **JetBrains Rider** (Cross-platform)

## Quick Start (Linux/macOS)

### 1. Install .NET 8 SDK

**Ubuntu/Debian:**
```bash
wget https://packages.microsoft.com/config/ubuntu/24.04/packages-microsoft-prod.deb -O packages-microsoft-prod.deb
sudo dpkg -i packages-microsoft-prod.deb
rm packages-microsoft-prod.deb

sudo apt-get update
sudo apt-get install -y dotnet-sdk-8.0
```

**macOS (Homebrew):**
```bash
brew install dotnet-sdk
```

**Manual Installation (All platforms):**
```bash
# Download and run the official install script
wget https://dot.net/v1/dotnet-install.sh -O dotnet-install.sh
chmod +x dotnet-install.sh
./dotnet-install.sh --channel 8.0
```

### 2. Verify Installation

```bash
dotnet --version
# Should output: 8.0.xxx
```

### 3. Restore NuGet Packages

```bash
cd /path/to/RefactorCsharpMCP
dotnet restore
```

### 4. Build the Solution

```bash
# Build all projects
dotnet build

# Or build in Release mode
dotnet build -c Release
```

### 5. Run Tests

```bash
# Run all tests
dotnet test

# Run with detailed output
dotnet test --logger "console;verbosity=detailed"

# Run with code coverage
dotnet test --collect:"XPlat Code Coverage"
```

## Quick Start (Windows)

### 1. Install .NET 8 SDK

Download and install from: https://dotnet.microsoft.com/download/dotnet/8.0

Or use `winget`:
```powershell
winget install Microsoft.DotNet.SDK.8
```

### 2. Verify Installation

```powershell
dotnet --version
# Should output: 8.0.xxx
```

### 3. Restore, Build, and Test

```powershell
cd C:\path\to\RefactorCsharpMCP

# Restore packages
dotnet restore

# Build
dotnet build

# Run tests
dotnet test
```

## Project Structure

```
RefactorCsharpMCP/
├── src/
│   ├── RefactorCsharpMCP.Core/        # Core refactoring logic
│   ├── RefactorCsharpMCP.Server/      # MCP server
│   └── RefactorCsharpMCP.Tests/       # Test suite
├── docs/                               # Documentation
├── scripts/                            # Build and deployment scripts
├── CLAUDE.md                           # Claude Code project guidance
└── RefactorCsharpMCP.sln              # Solution file
```

## Key Dependencies

The project uses these major NuGet packages (automatically restored):

- **Microsoft.CodeAnalysis.CSharp 4.14.0** - Roslyn compiler platform
- **Microsoft.CodeAnalysis.CSharp.Workspaces 4.14.0** - Roslyn workspaces
- **Microsoft.Extensions.Logging.Abstractions 9.0.0** - Logging
- **NuGet.Protocol 6.11.0** - NuGet package handling
- **Microsoft.NETFramework.ReferenceAssemblies 1.0.3** - Framework reference assemblies
- **xUnit 2.9.2** - Testing framework
- **NSubstitute 5.3.0** - Mocking framework

## Development Workflow

### 1. Create a Feature Branch

```bash
git checkout -b feature/my-new-feature
```

### 2. Make Changes

Edit code using your preferred IDE/editor.

### 3. Run Tests Frequently

```bash
# Quick test run
dotnet test

# Test a specific project
dotnet test src/RefactorCsharpMCP.Tests

# Test a specific test class
dotnet test --filter "FullyQualifiedName~RenameSymbolTests"
```

### 4. Check Code Coverage

```bash
dotnet test --collect:"XPlat Code Coverage"

# Coverage reports are in:
# src/RefactorCsharpMCP.Tests/TestResults/*/coverage.cobertura.xml
```

### 5. Build in Release Mode

```bash
dotnet build -c Release
```

### 6. Commit and Push

```bash
git add .
git commit -m "feat: Add new refactoring feature"
git push origin feature/my-new-feature
```

## Running the MCP Server Locally

### From Source

```bash
cd src/RefactorCsharpMCP.Server
dotnet run
```

### From Published Build

```bash
dotnet publish src/RefactorCsharpMCP.Server -c Release -o ./publish
cd publish
./RefactorCsharpMCP.Server
```

## Docker Development

### Build Docker Image

```bash
docker build -t refactor-csharp-mcp .
```

### Run in Docker

```bash
docker run -i refactor-csharp-mcp
```

## IDE-Specific Setup

### Visual Studio Code

1. Install extensions:
   ```bash
   code --install-extension ms-dotnettools.csharp
   code --install-extension ms-dotnettools.csdevkit
   ```

2. Open the workspace:
   ```bash
   code RefactorCsharpMCP.sln
   ```

3. Use F5 to debug the server project

### Visual Studio 2022

1. Open `RefactorCsharpMCP.sln`
2. Set `RefactorCsharpMCP.Server` as the startup project
3. Press F5 to debug

### JetBrains Rider

1. Open `RefactorCsharpMCP.sln`
2. Right-click on `RefactorCsharpMCP.Server` → Run/Debug

## Troubleshooting

### Issue: `dotnet` command not found

**Solution:** Add .NET to your PATH:

**Linux/macOS:**
```bash
echo 'export PATH="$PATH:$HOME/.dotnet"' >> ~/.bashrc
source ~/.bashrc
```

**Windows:**
Add `C:\Program Files\dotnet` to your PATH environment variable.

### Issue: NuGet restore fails

**Solution:**
```bash
# Clear NuGet cache
dotnet nuget locals all --clear

# Restore again
dotnet restore
```

### Issue: Tests fail with "reference assembly not found"

**Solution:** The first test run downloads reference assemblies (~550MB). This is normal and happens once:

```bash
# Wait for initial download (may take 2-5 minutes)
dotnet test
```

Reference assemblies are cached in `~/.refactor-csharp-mcp/reference-assemblies/`

### Issue: Build fails with CS0246 errors

**Solution:** Clean and rebuild:

```bash
dotnet clean
dotnet restore
dotnet build
```

## Performance Optimization

### Enable NuGet Package Caching

NuGet packages are cached by default in:
- **Linux/macOS:** `~/.nuget/packages`
- **Windows:** `%userprofile%\.nuget\packages`

### Parallel Builds

```bash
dotnet build -m:4  # Use 4 parallel processes
```

### Skip Tests During Build

```bash
dotnet build --no-restore /p:SkipTests=true
```

## Additional Resources

- **Documentation:** See `/docs` folder for detailed specs
- **Examples:** See [EXAMPLES.md](EXAMPLES.md) for refactoring examples
- **Troubleshooting:** See [TROUBLESHOOTING.md](TROUBLESHOOTING.md) for common issues and solutions
- **Testing:** See [E2E-TESTING.md](E2E-TESTING.md) for integration tests
- **Claude Code:** See [CLAUDE.md](CLAUDE.md) for AI-assisted development guidance

## Getting Help

1. Check existing documentation in `/docs`
2. Review test files for usage examples
3. Open an issue on GitHub: https://github.com/sethb75/RefactorCsharpMCP/issues

## Next Steps

After setup:

1. Run `dotnet test` to verify everything works (may take 2-5 min on first run)
2. Explore the codebase starting with `CLAUDE.md`
3. Review `docs/PRD-V1-Refactoring-Capabilities.md` for project roadmap
4. Check existing issues for contribution opportunities
