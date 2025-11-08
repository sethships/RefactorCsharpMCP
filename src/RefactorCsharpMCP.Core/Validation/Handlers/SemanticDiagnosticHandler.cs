using Microsoft.CodeAnalysis;

namespace RefactorCsharpMCP.Core.Validation.Handlers;

/// <summary>
/// Handles semantic-time diagnostic errors (compilation errors after parsing succeeds).
/// Implements Strategy Pattern for diagnostic handling.
/// </summary>
/// <remarks>
/// Processes Roslyn diagnostics from semantic analysis and classifies them as:
/// - Framework API unavailability (types/members not in target framework)
/// - User typos (misspelled identifiers)
/// - Other semantic errors
///
/// Uses heuristics to distinguish between framework compatibility issues and code errors.
/// </remarks>
public class SemanticDiagnosticHandler : ISemanticDiagnosticHandler
{
    /// <summary>
    /// Known BCL and Microsoft framework namespace prefixes.
    /// Static readonly for efficient reuse across calls.
    /// All entries have trailing dots for precise matching.
    /// </summary>
    private static readonly string[] KnownBclPrefixes = new[]
    {
        // Core BCL namespaces
        "System.",
        "Microsoft.",
        "Windows.",
        "Internal.",

        // Common System.* sub-namespaces
        "System.Collections.",
        "System.Collections.Concurrent.",
        "System.Collections.Generic.",
        "System.Collections.Immutable.",
        "System.Threading.",
        "System.Threading.Tasks.",
        "System.Linq.",
        "System.Text.",
        "System.Text.Json.",
        "System.Text.RegularExpressions.",
        "System.IO.",
        "System.Net.",
        "System.Net.Http.",
        "System.Diagnostics.",
        "System.Reflection.",
        "System.Runtime.",
        "System.Runtime.CompilerServices.",
        "System.Runtime.InteropServices.",
        "System.Security.",
        "System.Security.Cryptography.",
        "System.Data.",
        "System.Xml.",
        "System.Xml.Linq.",
        "System.ComponentModel.",
        "System.ComponentModel.DataAnnotations.",
        "System.Drawing.",
        "System.Web.",

        // Additional framework namespaces
        "NuGet.",
        "FSharp."
    };

    /// <summary>
    /// Handles semantic diagnostics and returns appropriate validation result.
    /// </summary>
    /// <param name="diagnostics">Semantic-time diagnostics from compilation.</param>
    /// <param name="targetFramework">Target framework moniker for error context.</param>
    /// <param name="syntaxTree">Syntax tree for identifier extraction.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>ValidationResult indicating success or describing the error.</returns>
    public Task<ValidationResult> HandleAsync(
        IEnumerable<Diagnostic> diagnostics,
        string targetFramework,
        SyntaxTree syntaxTree,
        CancellationToken cancellationToken = default)
    {
        var semanticDiagnostics = diagnostics
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .ToList();

        if (!semanticDiagnostics.Any())
        {
            return Task.FromResult(ValidationResult.Success());
        }

        // Check if these are API availability errors (type/member not found in framework)
        var apiErrors = semanticDiagnostics.Where(d =>
            d.Id == "CS0246" || // Type not found
            d.Id == "CS0103" || // Name does not exist
            d.Id == "CS0234" || // Type/namespace not found
            d.Id == "CS1061").ToList(); // Member not found

        if (apiErrors.Any())
        {
            // Distinguish between typos and framework API unavailability using heuristics
            var (frameworkErrors, likelyTypos) = ClassifyApiErrors(apiErrors, syntaxTree);

            if (frameworkErrors.Any())
            {
                var apiErrorDetails = string.Join(", ", frameworkErrors.Select(d => d.GetMessage()).Take(3));
                if (frameworkErrors.Count > 3)
                {
                    apiErrorDetails += $" (and {frameworkErrors.Count - 3} more)";
                }

                return Task.FromResult(ValidationResult.Failure(
                    ErrorCode.FRAMEWORK_API_UNAVAILABLE,
                    $"Code references types or members not available in {targetFramework}: {apiErrorDetails}",
                    "Either target a newer framework version or replace with APIs available in the target framework. " +
                    "Run validation against multiple frameworks to confirm compatibility."));
            }

            if (likelyTypos.Any())
            {
                var typoDetails = string.Join(", ", likelyTypos.Select(d => d.GetMessage()).Take(3));
                if (likelyTypos.Count > 3)
                {
                    typoDetails += $" (and {likelyTypos.Count - 3} more)";
                }

                return Task.FromResult(ValidationResult.SyntaxError(typoDetails));
            }
        }

        // Other semantic errors (not API availability)
        var semanticErrorDetails = string.Join(", ", semanticDiagnostics.Select(d => d.GetMessage()).Take(3));
        if (semanticDiagnostics.Count > 3)
        {
            semanticErrorDetails += $" (and {semanticDiagnostics.Count - 3} more)";
        }
        return Task.FromResult(ValidationResult.SyntaxError(semanticErrorDetails));
    }

    /// <summary>
    /// Classifies API errors (CS0246, CS0103, CS0234, CS1061) as either framework API unavailability or likely typos.
    /// Uses three-stage heuristic approach to distinguish between legitimate framework compatibility issues
    /// and user typos in code.
    /// </summary>
    /// <remarks>
    /// <para><b>Classification Strategy:</b></para>
    /// <list type="number">
    /// <item>
    /// <term>BCL Namespace Detection</term>
    /// <description>Identifiers starting with System.*, Microsoft.*, Windows.*, etc. are classified as framework API issues.</description>
    /// </item>
    /// <item>
    /// <term>Typo Pattern Detection</term>
    /// <description>Identifiers with obvious typo indicators (triple chars, all lowercase, mixed case anomalies) are classified as likely typos.</description>
    /// </item>
    /// <item>
    /// <term>Conservative Default</term>
    /// <description>Ambiguous cases default to framework API issue (safer for framework-aware validation context).</description>
    /// </item>
    /// </list>
    /// <para><b>Design Rationale:</b></para>
    /// <para>
    /// This method operates in a framework-aware validation context where users have explicitly specified a target framework.
    /// The conservative default (ambiguous → framework API) is appropriate because:
    /// - User typos would typically be caught by IDE/compiler before reaching this tool
    /// - False negatives (typos classified as framework issues) are safer than false positives
    /// - Error messages include the specific identifier, allowing users to recognize typos
    /// - Users can test against multiple frameworks to confirm actual compatibility issues
    /// </para>
    /// </remarks>
    /// <param name="apiErrors">List of diagnostic errors with IDs CS0246, CS0103, CS0234, or CS1061 (type/member not found).</param>
    /// <param name="syntaxTree">The syntax tree being validated, used for locale-independent identifier extraction.</param>
    /// <returns>
    /// A tuple containing:
    /// - frameworkErrors: Diagnostics classified as framework API unavailability
    /// - likelyTypos: Diagnostics classified as probable user typos
    /// </returns>
    private static (List<Diagnostic> frameworkErrors, List<Diagnostic> likelyTypos) ClassifyApiErrors(
        List<Diagnostic> apiErrors,
        SyntaxTree syntaxTree)
    {
        var frameworkErrors = new List<Diagnostic>();
        var likelyTypos = new List<Diagnostic>();

        foreach (var error in apiErrors)
        {
            var message = error.GetMessage();
            var identifier = ExtractIdentifierFromError(error, syntaxTree);

            if (string.IsNullOrEmpty(identifier))
            {
                // Cannot extract identifier - default to framework error (safer assumption)
                frameworkErrors.Add(error);
                continue;
            }

            // Heuristic 1: Check for known BCL namespace prefixes
            if (IsKnownBclNamespace(identifier))
            {
                frameworkErrors.Add(error);
                continue;
            }

            // Heuristic 2: Check for improper naming conventions (likely typos)
            if (HasObviousTypo(identifier))
            {
                likelyTypos.Add(error);
                continue;
            }

            // Heuristic 3: Check for very short identifiers (often typos)
            if (identifier.Length <= 2)
            {
                likelyTypos.Add(error);
                continue;
            }

            // Default: Classify as framework error (conservative approach)
            // If uncertain, treat as framework issue since validation is framework-specific
            frameworkErrors.Add(error);
        }

        return (frameworkErrors, likelyTypos);
    }

    /// <summary>
    /// Extracts the identifier name from a diagnostic error using syntax tree location (locale-independent)
    /// with fallback to message parsing for edge cases.
    /// </summary>
    /// <param name="diagnostic">The diagnostic containing the error.</param>
    /// <param name="syntaxTree">The syntax tree to extract identifier from.</param>
    /// <returns>The extracted identifier, or empty string if extraction fails.</returns>
    private static string ExtractIdentifierFromError(Diagnostic diagnostic, SyntaxTree syntaxTree)
    {
        // Strategy 1 (PRIMARY): Use diagnostic location to extract identifier from syntax tree
        // This is locale-independent and version-resilient
        if (diagnostic.Location.IsInSource)
        {
            try
            {
                var node = syntaxTree.GetRoot().FindNode(diagnostic.Location.SourceSpan);
                if (node != null)
                {
                    var identifierText = node.ToString().Trim();
                    if (!string.IsNullOrWhiteSpace(identifierText))
                    {
                        return identifierText;
                    }
                }
            }
            catch (ArgumentOutOfRangeException)
            {
                // Location span is invalid, fall through to message parsing
            }
        }

        // Strategy 2 (FALLBACK): Extract from error message (locale-dependent)
        // Support multiple quote characters for different locales
        var message = diagnostic.GetMessage();

        // Try various quote characters: single quote (English), double quote, Unicode quotes
        foreach (var quoteChar in new[] { '\'', '"', '\u2018', '\u2019', '\u201C', '\u201D' })
        {
            var startQuote = message.IndexOf(quoteChar);
            if (startQuote >= 0)
            {
                var endQuote = message.IndexOf(quoteChar, startQuote + 1);
                if (endQuote > startQuote)
                {
                    var extracted = message.Substring(startQuote + 1, endQuote - startQuote - 1);
                    if (!string.IsNullOrWhiteSpace(extracted))
                    {
                        return extracted;
                    }
                }
            }
        }

        return string.Empty;
    }

    /// <summary>
    /// Checks if an identifier belongs to a known BCL namespace.
    /// </summary>
    /// <param name="identifier">The identifier to check.</param>
    /// <returns>True if the identifier starts with a known BCL namespace prefix.</returns>
    private static bool IsKnownBclNamespace(string identifier)
    {
        return KnownBclPrefixes.Any(prefix => identifier.StartsWith(prefix, StringComparison.Ordinal));
    }

    /// <summary>
    /// Checks if an identifier has obvious typo indicators using multiple heuristics.
    /// Allows common legitimate patterns like triple 's' in "ProcessSucceeded" or acronyms.
    /// </summary>
    /// <param name="identifier">The identifier to check for typo indicators.</param>
    /// <returns>True if the identifier likely contains a typo; false otherwise.</returns>
    private static bool HasObviousTypo(string identifier)
    {
        // Check for consecutive repeated characters (often typos, but allow common patterns)
        for (int i = 0; i < identifier.Length - 2; i++)
        {
            if (identifier[i] == identifier[i + 1] && identifier[i] == identifier[i + 2])
            {
                var repeatedChar = identifier[i];

                // Allow triple lowercase 's' (common in English: Process, Success, Address, etc.)
                // Allow triple uppercase letters (acronyms: XMLLLMProvider, HTTPSSL, etc.)
                if ((repeatedChar == 's' && char.IsLower(repeatedChar)) ||
                    char.IsUpper(repeatedChar))
                {
                    continue;
                }

                // Other triple characters are likely typos (e.g., "Striiing", "Boook")
                return true;
            }
        }

        // Check for all lowercase (uncommon for types in C#, but common for variables)
        // Since we're classifying types/namespaces, flag identifiers > 3 chars that are all lowercase
        if (identifier.Length > 3 && identifier.All(char.IsLower))
        {
            return true;
        }

        // Check for mixed case inconsistency (e.g., "sYstem" instead of "System")
        // Starts lowercase but has uppercase later - unusual for type names
        if (identifier.Length > 1 && char.IsLower(identifier[0]) && identifier.Skip(1).Any(char.IsUpper))
        {
            return true;
        }

        return false;
    }
}
