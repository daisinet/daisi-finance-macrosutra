using MacroSutra.Core.Models;
using MacroSutra.Data;

namespace MacroSutra.Services;

/// <summary>
/// Strategy CRUD with validation. Name is required and at least one symbol must be specified.
/// </summary>
public class StrategyService(MacroSutraCosmo cosmo)
{
    public virtual async Task<TradingStrategy> CreateStrategyAsync(TradingStrategy strategy)
    {
        ValidateStrategy(strategy);
        return await cosmo.CreateStrategyAsync(strategy);
    }

    public virtual async Task<TradingStrategy?> GetStrategyAsync(string id, string accountId)
    {
        return await cosmo.GetStrategyAsync(id, accountId);
    }

    public virtual async Task<List<TradingStrategy>> GetStrategiesAsync(string accountId, string? userId = null)
    {
        return await cosmo.GetStrategiesByUserAsync(accountId, userId);
    }

    public virtual async Task<TradingStrategy> UpdateStrategyAsync(TradingStrategy strategy)
    {
        ValidateStrategy(strategy);
        return await cosmo.UpdateStrategyAsync(strategy);
    }

    public virtual async Task<TradingStrategy> ActivateStrategyAsync(string id, string accountId)
    {
        var strategy = await cosmo.GetStrategyAsync(id, accountId)
            ?? throw new InvalidOperationException("Strategy not found.");

        strategy.IsActive = true;
        return await cosmo.UpdateStrategyAsync(strategy);
    }

    public virtual async Task<TradingStrategy> DeactivateStrategyAsync(string id, string accountId)
    {
        var strategy = await cosmo.GetStrategyAsync(id, accountId)
            ?? throw new InvalidOperationException("Strategy not found.");

        strategy.IsActive = false;
        return await cosmo.UpdateStrategyAsync(strategy);
    }

    public virtual async Task DeleteStrategyAsync(string id, string accountId)
    {
        await cosmo.DeleteStrategyAsync(id, accountId);
    }

    internal static void ValidateStrategy(TradingStrategy strategy)
    {
        if (string.IsNullOrWhiteSpace(strategy.Name))
            throw new InvalidOperationException("Strategy name is required.");

        if (strategy.Symbols == null || strategy.Symbols.Count == 0)
            throw new InvalidOperationException("At least one symbol is required.");
    }
}
