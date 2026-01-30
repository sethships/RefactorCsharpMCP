# Security Policy

## Supported Versions

| Version | Supported          |
| ------- | ------------------ |
| 1.x     | :white_check_mark: |
| < 1.0   | :x:                |

## Reporting a Vulnerability

We take security vulnerabilities seriously. If you discover a security issue, please report it responsibly.

### How to Report

**Please do NOT report security vulnerabilities through public GitHub issues.**

Instead, use one of these methods:

1. **GitHub Security Advisory** (Preferred): Use GitHub's [private vulnerability reporting](https://github.com/sethb75/RefactorCsharpMCP/security/advisories/new) feature to submit a report directly.

2. **Private Issue**: Create a private security report via GitHub's security tab.

### What to Include

When reporting a vulnerability, please include:

- Description of the vulnerability
- Steps to reproduce the issue
- Potential impact assessment
- Any suggested fixes (if available)

### Response Timeline

- **Initial Response**: Within 48 hours of report submission
- **Status Update**: Within 7 days with assessment and remediation plan
- **Resolution**: Depends on severity and complexity

### Severity Levels

| Severity | Description | Target Resolution |
|----------|-------------|-------------------|
| Critical | Remote code execution, data breach | 24-48 hours |
| High | Authentication bypass, privilege escalation | 7 days |
| Medium | Information disclosure, denial of service | 30 days |
| Low | Minor issues with limited impact | Next release |

## Security Best Practices

When using RefactorCsharpMCP:

1. **Keep dependencies updated** - Run `dotnet restore` regularly
2. **Review refactored code** - Always review AI-generated refactorings before committing
3. **Use in isolated environments** - Run refactoring tools in development environments, not production
4. **Validate inputs** - The MCP server processes source code; ensure inputs are from trusted sources

## Acknowledgments

We appreciate security researchers who help keep RefactorCsharpMCP secure. Contributors who report valid security issues will be acknowledged in our release notes (unless they prefer to remain anonymous).
