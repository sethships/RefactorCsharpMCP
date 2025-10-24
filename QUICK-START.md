# Quick Start Guide

Get up and running with RefactorCsharpMCP in 5 minutes.

## Prerequisites

- .NET 8 SDK: https://dotnet.microsoft.com/download/dotnet/8.0

## One-Command Setup

### Linux/macOS
```bash
./scripts/setup-dev-environment.sh
```

### Windows
```powershell
.\scripts\setup-dev-environment.ps1
```

## Manual Setup

### 1. Install .NET 8 SDK
```bash
dotnet --version  # Verify you have 8.0+
```

### 2. Restore & Build
```bash
dotnet restore
dotnet build
```

### 3. Run Tests
```bash
dotnet test
```

## Common Commands

```bash
# Build solution
dotnet build

# Build in Release mode
dotnet build -c Release

# Run all tests
dotnet test

# Run specific test class
dotnet test --filter "FullyQualifiedName~RenameSymbolTests"

# Run with code coverage
dotnet test --collect:"XPlat Code Coverage"

# Run the MCP server
cd src/RefactorCsharpMCP.Server
dotnet run

# Clean build artifacts
dotnet clean
```

## Project Structure

```
RefactorCsharpMCP/
├── src/
│   ├── RefactorCsharpMCP.Core/     # 🧠 Core refactoring logic
│   ├── RefactorCsharpMCP.Server/   # 🚀 MCP server
│   └── RefactorCsharpMCP.Tests/    # ✅ Test suite (452 tests)
├── docs/                            # 📚 Documentation
└── scripts/                         # 🔧 Build scripts
```

## Key Files

- **CLAUDE.md** - Project guidance (READ THIS FIRST for development)
- **README.md** - User documentation
- **SETUP-DEVELOPMENT.md** - Detailed setup instructions
- **TROUBLESHOOTING.md** - Common issues and solutions
- **docs/PRD-V1-Refactoring-Capabilities.md** - Product roadmap

## Available Refactorings

1. **Extract Method** - Extract code into new method
2. **Constructor Injection** - Convert parameters to injected dependencies
3. **Make Field Readonly** - Make fields readonly where safe
4. **Safe Delete** - Delete methods/classes with reference checking
5. **Extract Class** - Extract fields/methods into new class
6. **Remove Unused Usings** - Remove unused using directives
7. **Rename Symbol** - Rename variables, parameters, fields, methods

## Development Workflow

```bash
# 1. Create feature branch
git checkout -b feature/my-feature

# 2. Make changes and test frequently
dotnet test

# 3. Build in Release mode before committing
dotnet build -c Release

# 4. Commit and push
git add .
git commit -m "feat: Add new feature"
git push origin feature/my-feature
```

## First-Time Setup Notes

- **First test run takes 2-5 minutes** - Downloads reference assemblies (~550MB)
- **Reference assemblies cached** in `~/.refactor-csharp-mcp/reference-assemblies/`
- **Test count**: 452 tests (424 unit + 20 component + 8 integration)
- **Code coverage**: ~87% lines, ~83% branches

## Need Help?

- **Detailed setup**: See `SETUP-DEVELOPMENT.md`
- **Troubleshooting**: See `TROUBLESHOOTING.md`
- **Architecture**: See `CLAUDE.md`
- **Examples**: See `EXAMPLES.md`
- **Issues**: https://github.com/sethb75/RefactorCsharpMCP/issues

## Next Steps

1. ✅ Run `dotnet test` to verify setup
2. 📖 Read `CLAUDE.md` for development guidance
3. 🔍 Explore the codebase starting with `src/RefactorCsharpMCP.Core`
4. 🚀 Check `docs/PRD-V1-Refactoring-Capabilities.md` for roadmap

Happy coding! 🎉
