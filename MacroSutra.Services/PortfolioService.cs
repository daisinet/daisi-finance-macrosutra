using MacroSutra.Brokers;
using MacroSutra.Core.Enums;
using MacroSutra.Core.Models;
using MacroSutra.Data;

namespace MacroSutra.Services;

/// <summary>
/// BrokerageAccount management and position sync.
/// </summary>
public class PortfolioService(MacroSutraCosmo cosmo, BrokerageProviderFactory providerFactory)
{
    // ── Brokerage Accounts ──

    public virtual async Task<BrokerageAccount> CreateBrokerageAccountAsync(BrokerageAccount account)
    {
        ValidateBrokerageAccount(account);
        return await cosmo.CreateBrokerageAccountAsync(account);
    }

    public virtual async Task<BrokerageAccount?> GetBrokerageAccountAsync(string id, string accountId)
    {
        return await cosmo.GetBrokerageAccountAsync(id, accountId);
    }

    public virtual async Task<List<BrokerageAccount>> GetBrokerageAccountsAsync(string accountId, bool activeOnly = false)
    {
        return await cosmo.GetBrokerageAccountsAsync(accountId, activeOnly);
    }

    public virtual async Task<BrokerageAccount> UpdateBrokerageAccountAsync(BrokerageAccount account)
    {
        ValidateBrokerageAccount(account);
        return await cosmo.UpdateBrokerageAccountAsync(account);
    }

    public virtual async Task<BrokerageAccount> DeactivateBrokerageAccountAsync(string id, string accountId)
    {
        var account = await cosmo.GetBrokerageAccountAsync(id, accountId)
            ?? throw new InvalidOperationException("Brokerage account not found.");

        account.IsActive = false;
        return await cosmo.UpdateBrokerageAccountAsync(account);
    }

    // ── Positions ──

    public virtual async Task<List<Position>> GetPositionsAsync(string accountId, string? brokerageAccountId = null)
    {
        return await cosmo.GetPositionsAsync(accountId, brokerageAccountId);
    }

    public virtual async Task<Position> SyncPositionAsync(Position position)
    {
        return await cosmo.UpdatePositionAsync(position);
    }

    /// <summary>
    /// Validates credentials with the provider and creates the account if valid.
    /// </summary>
    public virtual async Task<BrokerageAccount> ValidateAndCreateBrokerageAccountAsync(BrokerageAccount account)
    {
        ValidateBrokerageAccount(account);

        if (account.Provider != BrokerageProvider.Paper)
        {
            if (string.IsNullOrWhiteSpace(account.CredentialData))
                throw new InvalidOperationException("Credentials are required for non-Paper accounts.");

            var provider = providerFactory.GetProvider(account.Provider);
            var isValid = await provider.ValidateCredentialsAsync(account.CredentialData);
            if (!isValid)
                throw new InvalidOperationException("Credential validation failed. Please check your API keys.");
        }

        return await cosmo.CreateBrokerageAccountAsync(account);
    }

    internal static void ValidateBrokerageAccount(BrokerageAccount account)
    {
        if (string.IsNullOrWhiteSpace(account.Name))
            throw new InvalidOperationException("Brokerage account name is required.");
    }
}
