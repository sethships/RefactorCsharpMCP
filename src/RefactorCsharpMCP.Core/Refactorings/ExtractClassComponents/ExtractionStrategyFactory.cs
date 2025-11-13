using RefactorCsharpMCP.Core.Refactorings.ExtractClassComponents.Strategies;

namespace RefactorCsharpMCP.Core.Refactorings.ExtractClassComponents;

/// <summary>
/// Factory for selecting appropriate extraction modifier strategies based on context and mode.
/// Provides centralized strategy selection logic with support for explicit and automatic strategy selection.
/// </summary>
public class ExtractionStrategyFactory
{
    private readonly IReadOnlyList<IExtractionModifierStrategy> _strategies;
    private readonly IExtractionModifierStrategy _defaultStrategy;

    /// <summary>
    /// Initializes a new instance of the <see cref="ExtractionStrategyFactory"/> class.
    /// </summary>
    public ExtractionStrategyFactory()
    {
        _strategies = new List<IExtractionModifierStrategy>
        {
            new InternalCompositionStrategy(),
            new PublicApiStrategy()
        }.AsReadOnly();

        _defaultStrategy = _strategies.First(s => s is InternalCompositionStrategy);
    }

    /// <summary>
    /// Selects the appropriate strategy based on the extraction context.
    /// </summary>
    /// <param name="context">The extraction context containing mode and source class information.</param>
    /// <returns>The selected strategy, or default strategy if no match is found.</returns>
    /// <remarks>
    /// Selection logic:
    /// <list type="number">
    /// <item><description>For explicit modes (non-Default, non-Automatic), finds strategy that can handle the mode</description></item>
    /// <item><description>For automatic mode, evaluates strategies in priority order using heuristics</description></item>
    /// <item><description>Falls back to default (InternalComposition) for backward compatibility</description></item>
    /// </list>
    /// </remarks>
    public IExtractionModifierStrategy SelectStrategy(ExtractionContext context)
    {
        // For explicit modes, find matching strategy
        if (context.Mode != ExtractionMode.Automatic && context.Mode != ExtractionMode.Default)
        {
            var explicitStrategy = _strategies.FirstOrDefault(s => s.CanHandle(context));
            if (explicitStrategy != null)
                return explicitStrategy;
        }

        // For automatic mode, use heuristics
        if (context.Mode == ExtractionMode.Automatic)
        {
            // Try each strategy in priority order (excluding default)
            foreach (var strategy in _strategies)
            {
                if (strategy != _defaultStrategy && strategy.CanHandle(context))
                    return strategy;
            }
        }

        // Fall back to default (InternalComposition) for backward compatibility
        return _defaultStrategy;
    }

    /// <summary>
    /// Gets a specific strategy by name (for testing and explicit selection).
    /// </summary>
    /// <param name="strategyName">The name of the strategy to retrieve.</param>
    /// <returns>The strategy with the matching name, or null if not found.</returns>
    public IExtractionModifierStrategy? GetStrategyByName(string strategyName)
    {
        return _strategies.FirstOrDefault(s =>
            s.StrategyName.Equals(strategyName, StringComparison.OrdinalIgnoreCase));
    }
}
