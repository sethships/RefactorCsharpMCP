# Claude Code Sandbox Development Guide

This guide explains how to work with custom dependencies in the Claude Code cloud sandbox environment.

## Available Package Managers

The Claude Code sandbox comes with several package managers pre-installed:

### ✅ Python (pip/uv)
```bash
pip3 install <package>
# Or use uv (faster)
uv pip install <package>
```

### ✅ Node.js (npm)
```bash
npm install -g <package>
# Or local install
npm install <package>
```

### ✅ Rust (cargo)
```bash
cargo install <package>
```

### ✅ Go
```bash
go install <package>@latest
```

### ✅ Build Tools
```bash
# gcc, make, cmake are available
gcc --version
make --version
cmake --version
```

## Current Limitations for .NET Development

### ❌ .NET SDK Installation Blocked

The sandbox has network restrictions that prevent installing .NET SDK via:
- Direct downloads (403 Forbidden)
- apt/package managers (sudo permission issues)
- Manual binary installation (download blocked)

### What This Means for RefactorCsharpMCP

**You CANNOT in this sandbox:**
- Build the C# solution (`dotnet build`)
- Run tests (`dotnet test`)
- Run the MCP server (`dotnet run`)
- Restore NuGet packages
- Use Roslyn APIs directly

**You CAN still:**
- ✅ Read and analyze all C# source files
- ✅ Review code structure and architecture
- ✅ Create/modify documentation
- ✅ Write new code (just can't compile it)
- ✅ Git operations (commit, push, PR)
- ✅ Create scripts and tools
- ✅ Plan and design work
- ✅ Code reviews
- ✅ Create GitHub issues
- ✅ Analyze test files
- ✅ Update project files (.csproj, .sln)

## Workarounds for .NET Development

### Option 1: Analysis-Only Mode (Current)

Use the sandbox for code review and analysis without compilation:

```bash
# Search for patterns in code
grep -r "RefactoringBase" src/

# Analyze code structure
find src/ -name "*.cs" | wc -l

# Review test coverage
grep -r "\[Fact\]" src/RefactorCsharpMCP.Tests/ | wc -l

# Check TODO comments
grep -rn "TODO" src/
```

### Option 2: Local Development

For actual compilation and testing:

1. Clone the repository locally
2. Run the setup scripts we created:
   ```bash
   ./scripts/setup-dev-environment.sh
   ```
3. Use your local IDE for development
4. Use the sandbox for code review and documentation

### Option 3: Docker-based Development (Future)

If Docker becomes available in the sandbox, we could:
```bash
docker run -it mcr.microsoft.com/dotnet/sdk:8.0 bash
```

## Installing Other Dependencies

### Python Tools

```bash
# Install Python linters/formatters
pip3 install pylint black mypy

# Already installed in sandbox:
# - black, mypy, flake8, pytest, poetry, ruff
```

### Node.js Tools

```bash
# Install Node.js tools
npm install -g typescript
npm install -g eslint
npm install -g prettier
```

### Rust Tools

```bash
# Install Rust-based tools
cargo install ripgrep  # Already available as 'rg'
cargo install fd-find
cargo install bat
```

### Build from Source

If you need a custom tool:

```bash
# Clone and build
git clone https://github.com/user/tool.git
cd tool
make install PREFIX=$HOME/.local
```

## File System Access

### Writable Locations

```bash
$HOME (/root)           # Your home directory
~/.local/bin            # User-local binaries
~/.local/share          # User-local data
~/.cache                # Cache directory
/tmp                    # Temporary files
```

### Read-Only Locations

```bash
/usr                    # System binaries
/etc                    # System configuration
```

## Recommended Workflow for RefactorCsharpMCP

### In the Sandbox (Claude Code Cloud)

1. **Documentation Work**
   ```bash
   # Update docs
   vim docs/SDD-Framework-Version-Awareness.md

   # Create new guides
   vim CONTRIBUTING.md
   ```

2. **Code Analysis**
   ```bash
   # Find refactoring implementations
   find src/RefactorCsharpMCP.Core/Refactorings -name "*.cs"

   # Analyze test coverage
   grep -r "public.*Execute" src/RefactorCsharpMCP.Core/
   ```

3. **Planning and Design**
   ```bash
   # Create architectural documents
   vim docs/SDD-Diagnostic-Integration.md

   # Draft GitHub issues
   vim .github/ISSUE_TEMPLATE/feature.md
   ```

4. **Git Operations**
   ```bash
   git status
   git add .
   git commit -m "docs: Update architecture"
   git push
   ```

### On Your Local Machine

1. **Build and Test**
   ```bash
   dotnet build
   dotnet test
   ```

2. **Run the Server**
   ```bash
   cd src/RefactorCsharpMCP.Server
   dotnet run
   ```

3. **Debug with IDE**
   - Visual Studio Code
   - Visual Studio 2022
   - JetBrains Rider

## Example: Installing a Python Code Analyzer

If you wanted to analyze C# code using Python tools:

```bash
# Install Python C# parser (if one exists)
pip3 install tree-sitter
pip3 install tree-sitter-c-sharp

# Use it in a script
python3 analyze_csharp.py src/
```

## Checking Available Tools

```bash
# List installed npm packages
npm list -g --depth=0

# List installed pip packages
pip3 list

# List cargo-installed binaries
ls ~/.cargo/bin/

# Check PATH
echo $PATH
```

## Environment Variables

```bash
# See current environment
env | sort

# Add to PATH
export PATH=$HOME/.local/bin:$PATH

# Make permanent (add to ~/.bashrc)
echo 'export PATH=$HOME/.local/bin:$PATH' >> ~/.bashrc
```

## Summary

**For RefactorCsharpMCP Development:**

| Task | Sandbox | Local Machine |
|------|---------|---------------|
| Read/analyze code | ✅ | ✅ |
| Build solution | ❌ | ✅ |
| Run tests | ❌ | ✅ |
| Write documentation | ✅ | ✅ |
| Git operations | ✅ | ✅ |
| Create issues/PRs | ✅ | ✅ |
| Code review | ✅ | ✅ |
| Debug refactorings | ❌ | ✅ |
| Run MCP server | ❌ | ✅ |

**Recommendation:** Use the sandbox for planning, documentation, and analysis. Use your local machine for compilation, testing, and debugging.

## Need Help?

- Check available tools: `which <command>`
- Check installed packages: `apt list --installed`
- Test network access: `wget --spider https://google.com`
- File permission issues: Work in `$HOME` directory
