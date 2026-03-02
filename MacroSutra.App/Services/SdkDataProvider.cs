using MacroSutra.Core.Enums;
using MacroSutra.Core.Models;
using MacroSutra.SDK;
using MacroSutra.UI.Services;

namespace MacroSutra.App.Services;

/// <summary>
/// IDataProvider that creates MacroSutraClient with the user's clientKey
/// from MauiAuthProvider, then delegates to SDK sub-clients.
/// </summary>
public class SdkDataProvider(MacroSutraClientFactory clientFactory, MauiAuthProvider authProvider) : IDataProvider
{
    private async Task<MacroSutraClient> GetClientAsync()
    {
        var clientKey = await authProvider.GetClientKeyAsync()
            ?? throw new InvalidOperationException("Not authenticated.");
        return clientFactory.Create(clientKey);
    }

    // Users — SDK endpoints not fully exposed for mutation in Phase 1
    public Task<MacroSutraUser> CreateUserAsync(MacroSutraUser user) =>
        throw new NotSupportedException("User creation is server-side only.");

    public Task<MacroSutraUser?> GetUserByDaisinetIdAsync(string daisinetUserId, string accountId) =>
        throw new NotSupportedException("Use GetCurrentUser via SDK.");

    public async Task<List<MacroSutraUser>> GetUsersAsync(string accountId, bool activeOnly = false)
    {
        using var client = await GetClientAsync();
        var sdkUsers = await client.Users.GetUsersAsync();
        return sdkUsers.Select(u => new MacroSutraUser
        {
            id = u.Id, AccountId = u.AccountId, Name = u.Name, Email = u.Email,
            DaisinetUserId = u.DaisinetUserId, IsActive = u.IsActive
        }).ToList();
    }

    public Task<MacroSutraUser> UpdateUserAsync(MacroSutraUser user) =>
        throw new NotSupportedException("User updates are server-side only.");

    public Task<MacroSutraUser> DeactivateUserAsync(string id, string accountId) =>
        throw new NotSupportedException("User management is server-side only.");

    public Task<MacroSutraUser> ReactivateUserAsync(string id, string accountId) =>
        throw new NotSupportedException("User management is server-side only.");

    // Strategies
    public async Task<TradingStrategy> CreateStrategyAsync(TradingStrategy strategy)
    {
        using var client = await GetClientAsync();
        var sdkResult = await client.Strategies.CreateStrategyAsync(new SDK.Models.TradingStrategy
        {
            Name = strategy.Name, Description = strategy.Description
        });
        strategy.id = sdkResult?.Id ?? "";
        return strategy;
    }

    public async Task<TradingStrategy?> GetStrategyAsync(string id, string accountId)
    {
        using var client = await GetClientAsync();
        var sdk = await client.Strategies.GetStrategyAsync(id);
        if (sdk == null) return null;
        return new TradingStrategy
        {
            id = sdk.Id, AccountId = sdk.AccountId, UserId = sdk.UserId,
            Name = sdk.Name, Description = sdk.Description,
            Symbols = sdk.Symbols, IsActive = sdk.IsActive, IsPublic = sdk.IsPublic
        };
    }

    public async Task<List<TradingStrategy>> GetStrategiesAsync(string accountId, string? userId = null)
    {
        using var client = await GetClientAsync();
        var sdkStrategies = await client.Strategies.GetStrategiesAsync();
        return sdkStrategies.Select(s => new TradingStrategy
        {
            id = s.Id, AccountId = s.AccountId, UserId = s.UserId,
            Name = s.Name, Description = s.Description,
            Symbols = s.Symbols, IsActive = s.IsActive, IsPublic = s.IsPublic,
            CreatedUtc = s.CreatedUtc
        }).ToList();
    }

    public async Task<TradingStrategy> UpdateStrategyAsync(TradingStrategy strategy)
    {
        using var client = await GetClientAsync();
        await client.Strategies.UpdateStrategyAsync(strategy.id, new SDK.Models.TradingStrategy
        {
            Id = strategy.id, Name = strategy.Name, Description = strategy.Description
        });
        return strategy;
    }

    public Task<TradingStrategy> ActivateStrategyAsync(string id, string accountId) =>
        throw new NotSupportedException("Activate via API not yet exposed in SDK.");

    public Task<TradingStrategy> DeactivateStrategyAsync(string id, string accountId) =>
        throw new NotSupportedException("Deactivate via API not yet exposed in SDK.");

    public async Task DeleteStrategyAsync(string id, string accountId)
    {
        using var client = await GetClientAsync();
        await client.Strategies.DeleteStrategyAsync(id);
    }

    // Trades
    public async Task<List<Trade>> GetTradesAsync(string accountId, string? symbol = null, TradeStatus? status = null, string? strategyId = null)
    {
        using var client = await GetClientAsync();
        var sdkTrades = await client.Trades.GetTradesAsync(symbol, status?.ToString());
        return sdkTrades.Select(t => new Trade
        {
            id = t.Id, AccountId = t.AccountId, Symbol = t.Symbol,
            Quantity = t.Quantity, FilledPrice = t.FilledPrice,
            CreatedUtc = t.CreatedUtc, FilledUtc = t.FilledUtc
        }).ToList();
    }

    public async Task<Trade?> GetTradeAsync(string id, string accountId)
    {
        using var client = await GetClientAsync();
        var t = await client.Trades.GetTradeAsync(id);
        if (t == null) return null;
        return new Trade
        {
            id = t.Id, AccountId = t.AccountId, Symbol = t.Symbol,
            Quantity = t.Quantity, FilledPrice = t.FilledPrice,
            CreatedUtc = t.CreatedUtc, FilledUtc = t.FilledUtc
        };
    }

    // Portfolio
    public async Task<List<BrokerageAccount>> GetBrokerageAccountsAsync(string accountId, bool activeOnly = false)
    {
        using var client = await GetClientAsync();
        var sdkAccounts = await client.Portfolio.GetAccountsAsync();
        return sdkAccounts.Select(a => new BrokerageAccount
        {
            id = a.Id, AccountId = a.AccountId, Name = a.Name, IsActive = a.IsActive
        }).ToList();
    }

    public Task<BrokerageAccount> CreateBrokerageAccountAsync(BrokerageAccount account) =>
        throw new NotSupportedException("Brokerage account creation via SDK not yet implemented.");

    public async Task<List<Position>> GetPositionsAsync(string accountId, string? brokerageAccountId = null)
    {
        using var client = await GetClientAsync();
        var sdkPositions = await client.Portfolio.GetPositionsAsync(brokerageAccountId);
        return sdkPositions.Select(p => new Position
        {
            id = p.Id, BrokerageAccountId = p.BrokerageAccountId,
            Symbol = p.Symbol, Quantity = p.Quantity,
            AverageCost = p.AverageCost, CurrentPrice = p.CurrentPrice
        }).ToList();
    }

    // Daisinet team import — not available in MAUI
    public Task<List<DaisinetTeamMember>> GetDaisinetTeamMembersAsync() =>
        throw new NotSupportedException("Team import is only available in the web app.");

    // Subscriptions — not yet in SDK
    public Task<List<Subscription>> GetSubscriptionsAsync(string accountId) =>
        Task.FromResult(new List<Subscription>());

    public Task<Subscription> CreateSubscriptionAsync(Subscription subscription) =>
        throw new NotSupportedException("Subscription management via SDK not yet implemented.");

    public Task<Subscription> CancelSubscriptionAsync(string id, string accountId) =>
        throw new NotSupportedException("Subscription management via SDK not yet implemented.");
}
