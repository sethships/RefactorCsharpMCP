using Microsoft.CodeAnalysis.CSharp;

namespace RefactorCsharpMCP.Core.Framework;

/// <summary>
/// Fluent builder for constructing FrameworkInfo instances with validation.
/// </summary>
public class FrameworkInfoBuilder
{
    private string? _tfm;
    private string? _displayName;
    private LanguageVersion? _languageVersion;
    private FrameworkFamily _family = FrameworkFamily.Unknown;
    private string? _supportStatus;
    private DateTime? _releaseDate;
    private DateTime? _endOfSupport;

    /// <summary>
    /// Sets the Target Framework Moniker.
    /// </summary>
    public FrameworkInfoBuilder WithTfm(string tfm)
    {
        _tfm = tfm ?? throw new ArgumentNullException(nameof(tfm));
        return this;
    }

    /// <summary>
    /// Sets the human-readable display name.
    /// </summary>
    public FrameworkInfoBuilder WithDisplayName(string displayName)
    {
        _displayName = displayName ?? throw new ArgumentNullException(nameof(displayName));
        return this;
    }

    /// <summary>
    /// Sets the C# language version.
    /// </summary>
    public FrameworkInfoBuilder WithLanguageVersion(LanguageVersion languageVersion)
    {
        _languageVersion = languageVersion;
        return this;
    }

    /// <summary>
    /// Sets the framework family categorization.
    /// </summary>
    public FrameworkInfoBuilder WithFamily(FrameworkFamily family)
    {
        _family = family;
        return this;
    }

    /// <summary>
    /// Sets the support status description.
    /// </summary>
    public FrameworkInfoBuilder WithSupportStatus(string supportStatus)
    {
        _supportStatus = supportStatus ?? throw new ArgumentNullException(nameof(supportStatus));
        return this;
    }

    /// <summary>
    /// Sets the framework release date.
    /// </summary>
    public FrameworkInfoBuilder WithReleaseDate(DateTime releaseDate)
    {
        _releaseDate = releaseDate;
        return this;
    }

    /// <summary>
    /// Sets the end of support date.
    /// </summary>
    public FrameworkInfoBuilder WithEndOfSupport(DateTime endOfSupport)
    {
        _endOfSupport = endOfSupport;
        return this;
    }

    /// <summary>
    /// Builds the FrameworkInfo instance after validating all required fields.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown if required fields are missing.</exception>
    public FrameworkInfo Build()
    {
        if (string.IsNullOrWhiteSpace(_tfm))
            throw new InvalidOperationException("TFM is required");
        if (string.IsNullOrWhiteSpace(_displayName))
            throw new InvalidOperationException("DisplayName is required");
        if (!_languageVersion.HasValue)
            throw new InvalidOperationException("LanguageVersion is required");
        if (_family == FrameworkFamily.Unknown)
            throw new InvalidOperationException("Family is required");
        if (string.IsNullOrWhiteSpace(_supportStatus))
            throw new InvalidOperationException("SupportStatus is required");

        return new FrameworkInfo(
            _tfm,
            _displayName,
            _languageVersion.Value,
            _family,
            _supportStatus,
            _releaseDate,
            _endOfSupport);
    }
}
