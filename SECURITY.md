# Security Policy

## Supported Versions

| Version | Supported          |
| ------- | ------------------ |
| 1.x     | :white_check_mark: |
| < 1.0   | :x:                |

## Reporting a Vulnerability

We take security vulnerabilities seriously. If you discover a security issue, please report it responsibly.

### How to Report

**Please use [GitHub Security Advisories](https://github.com/sethb75/RefactorCsharpMCP/security/advisories/new)** to report vulnerabilities privately.

This allows us to:
- Discuss the issue privately before public disclosure
- Coordinate a fix and release timeline
- Credit you for the discovery (if desired)

### What to Include

When reporting a vulnerability, please include:

1. **Description**: A clear description of the vulnerability
2. **Steps to Reproduce**: Detailed steps to reproduce the issue
3. **Impact**: What an attacker could achieve by exploiting this
4. **Affected Versions**: Which versions are affected (if known)
5. **Suggested Fix**: Any ideas for remediation (optional)

### Response Timeline

- **Acknowledgment**: Within 48-72 hours
- **Initial Assessment**: Within 1 week
- **Fix Timeline**: Depends on severity, typically 2-4 weeks for critical issues

### What to Expect

1. We will acknowledge receipt of your report
2. We will investigate and validate the issue
3. We will work on a fix and coordinate disclosure timing with you
4. We will credit you in the security advisory (unless you prefer anonymity)
5. For confirmed vulnerabilities, we will request a CVE ID when appropriate

### Scope

This security policy covers:

- The RefactorCsharpMCP server and core library
- Docker images published from this repository
- Official documentation and examples

### Out of Scope

- Third-party dependencies (report to their maintainers)
- Issues in forked repositories
- Social engineering attacks

## Security Best Practices

When using RefactorCsharpMCP:

1. **Keep Updated**: Use the latest version to benefit from security fixes
2. **Review Output**: Always review refactored code before committing
3. **Sandbox Execution**: Run in isolated environments when processing untrusted code
4. **Access Control**: Limit who can invoke MCP tools in your environment

## Past Security Advisories

None reported yet. This section will be updated if security issues are discovered and fixed.
