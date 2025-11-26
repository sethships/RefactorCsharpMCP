# Contributing to RefactorCsharpMCP

Thank you for your interest in contributing to RefactorCsharpMCP! This document provides guidelines and instructions for contributing.

## Code of Conduct

Please be respectful and constructive in all interactions. We welcome contributors of all experience levels.

## Getting Started

### Prerequisites

- .NET 8.0 SDK
- Git
- A code editor (VS Code, Visual Studio, Rider, etc.)

### Setting Up Your Development Environment

1. **Fork the repository** on GitHub

2. **Clone your fork**:
   ```bash
   git clone https://github.com/YOUR-USERNAME/RefactorCsharpMCP.git
   cd RefactorCsharpMCP
   ```

3. **Build the project**:
   ```bash
   dotnet build
   ```

4. **Run tests** to verify everything works:
   ```bash
   dotnet test
   ```

## How to Contribute

### Reporting Bugs

1. Check [existing issues](https://github.com/sethb75/RefactorCsharpMCP/issues) to avoid duplicates
2. Create a new issue with:
   - Clear, descriptive title
   - Steps to reproduce
   - Expected vs actual behavior
   - .NET version and OS
   - Sample code (if applicable)

### Suggesting Features

1. Open an issue describing the feature
2. Explain the use case and benefits
3. Be open to discussion about implementation approaches

### Submitting Code Changes

#### Workflow

1. **Create a feature branch** from `master`:
   ```bash
   git checkout -b feature/your-feature-name
   # or
   git checkout -b fix/issue-description
   ```

2. **Make your changes** following our coding standards (see below)

3. **Write tests** for new functionality or bug fixes

4. **Run the full test suite**:
   ```bash
   dotnet test
   ```

5. **Commit your changes** with clear messages:
   ```bash
   git commit -m "Add feature: description of what was added"
   ```

6. **Push to your fork**:
   ```bash
   git push origin feature/your-feature-name
   ```

7. **Open a Pull Request** against `master`

#### Pull Request Guidelines

- **Keep PRs focused**: One feature or fix per PR
- **Size limit**: Aim for <400 lines of changes; larger PRs should be split
- **Include tests**: New features and bug fixes should have test coverage
- **Update documentation**: If your change affects user-facing behavior
- **Follow the template**: Fill out all sections of the PR template

#### PR Title Format

Use clear, descriptive titles:
- `feat: Add support for record types in ExtractClass`
- `fix: Handle null parameters in InlineMethod`
- `docs: Update README with new examples`
- `test: Add edge case tests for RenameSymbol`

## Coding Standards

### General Guidelines

- Follow existing code patterns and style
- Use meaningful variable and method names
- Keep methods focused and reasonably sized
- Add XML documentation for public APIs

### Architecture

- **RefactoringBase**: New refactorings should inherit from `RefactoringBase`
- **SymbolResolutionHelper**: Use for Roslyn symbol operations
- **RefactoringResult**: Return this type from refactoring operations

### Testing Requirements

- **Minimum 90% coverage** for new code
- Include both success and failure test cases
- Test edge cases and error conditions
- Use descriptive test method names:
  ```csharp
  public void MethodName_Scenario_ExpectedBehavior()
  ```

### Roslyn Best Practices

- Preserve trivia (whitespace, comments) during transformations
- Use `SyntaxFactory` for code generation
- Validate syntax before and after refactorings
- Maintain `SyntaxTree` identity for semantic operations

See [CLAUDE.md](CLAUDE.md) for detailed architectural guidance.

## Project Structure

```
RefactorCsharpMCP/
├── src/
│   ├── RefactorCsharpMCP.Server/    # MCP server and tools
│   ├── RefactorCsharpMCP.Core/      # Refactoring logic
│   └── RefactorCsharpMCP.Tests/     # Test suite
├── docs/                             # Documentation
└── scripts/                          # Build and deployment scripts
```

## Review Process

1. All PRs require at least one approval
2. CI must pass (builds and tests)
3. Address all review comments
4. Maintainer will merge when ready

### What We Look For

- Code correctness and quality
- Test coverage
- Documentation updates
- Performance considerations
- Security implications

## Questions?

- Open a [GitHub Issue](https://github.com/sethb75/RefactorCsharpMCP/issues) with the `question` label for general questions
- Check [TROUBLESHOOTING.md](TROUBLESHOOTING.md) for common issues
- Review [CLAUDE.md](CLAUDE.md) for architecture details

## License

By contributing, you agree that your contributions will be licensed under the [MIT License](LICENSE).

---

Thank you for contributing to RefactorCsharpMCP!
