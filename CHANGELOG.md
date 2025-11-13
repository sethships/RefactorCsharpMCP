# Changelog

All notable changes to RefactorCsharpMCP will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Changed
- **BREAKING**: Changed `validateCompilation` parameter default from `true` to `false` in ExtractClassTool (#119)
  - Improves usability for testing and dogfooding scenarios with isolated code snippets
  - Users can explicitly enable compilation validation when working with BCL-only code
  - Existing usage requiring validation will need to add `validateCompilation: true` explicitly

### Added
- Created shared `ToolInputValidator` utility class consolidating input validation across all 11 MCP tools (#92)
  - Reduced duplicated validation code from ~456 lines to ~105 lines (77% reduction)
  - Provides 6 standardized validation methods: `ValidateSourceCode`, `ValidateSourceCodeSize`, `ValidateIdentifier`, `ValidateLineNumber`, `ValidateColumnNumber`, `ValidateTargetFramework`
  - All tools now use consistent validation logic and error messages
  - Improved maintainability and consistency across the MCP tool surface

### Fixed
- Updated test assertions in `RenameSymbolToolTests` to match new validation error messages from `ToolInputValidator`

## [Previous Releases]

See git history for changes before this changelog was established.
