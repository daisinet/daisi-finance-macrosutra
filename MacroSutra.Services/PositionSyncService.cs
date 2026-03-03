using MacroSutra.Brokers;
using MacroSutra.Core.Enums;
using MacroSutra.Core.Models;
using MacroSutra.Data;

namespace MacroSutra.Services;

/// <summary>
/// Synchronizes positions and balances from brokerage providers.
/// </summary>
public class PositionSyncService(
    MacroSutraCosmo cosmo,
    BrokerageProviderFactory providerFactory)
{
    /// <summary>
    /// Result of syncing a single brokerage account.
    /// </summary>
    public class SyncResult
    {
        public int PositionCount { get; set; }
        public decimal? Balance { get; set; }
        public string? Error { get; set; }
    }

    /// <summary>
    /// Syncs positions and balance for a single brokerage account.
    /// Refreshes tokens if needed, fetches remote positions, upserts matches,
    /// deletes stale positions, and updates cached balance + LastSyncUtc.
    /// </summary>
    public virtual async Task<SyncResult> SyncAccountAsync(BrokerageAccount account)
    {
        var result = new SyncResult();

        try
        {
            var provider = providerFactory.GetProvider(account.Provider);

            // Attempt token refresh (only Webull overrides this)
            var credentialRef = !string.IsNullOrEmpty(account.CredentialData) ? account.CredentialData : account.CredentialRef;
            var refreshedCreds = await provider.TryRefreshCredentialsAsync(credentialRef);
            if (refreshedCreds != null)
            {
                account.CredentialData = refreshedCreds;
                await cosmo.UpdateBrokerageAccountAsync(account);
                credentialRef = refreshedCreds;
            }

            // Fetch remote positions
            var remotePositions = await provider.GetPositionsAsync(credentialRef);
            result.PositionCount = remotePositions.Count;

            // Fetch existing local positions
            var localPositions = await cosmo.GetPositionsAsync(account.AccountId, account.id);

            // Upsert remote positions
            var remoteSymbols = new HashSet<string>();
            foreach (var remote in remotePositions)
            {
                remoteSymbols.Add(remote.Symbol);
                var local = localPositions.FirstOrDefault(p => p.Symbol == remote.Symbol);
                if (local != null)
                {
                    local.Quantity = remote.Quantity;
                    local.AverageCost = remote.AverageCost;
                    local.CurrentPrice = remote.CurrentPrice;
                    await cosmo.UpdatePositionAsync(local);
                }
                else
                {
                    remote.AccountId = account.AccountId;
                    remote.BrokerageAccountId = account.id;
                    await cosmo.CreatePositionAsync(remote);
                }
            }

            // Delete stale local positions not in remote
            foreach (var local in localPositions)
            {
                if (!remoteSymbols.Contains(local.Symbol))
                    await cosmo.DeletePositionAsync(local.id, local.AccountId);
            }

            // Fetch and cache balance
            var balance = await provider.GetAccountBalanceAsync(credentialRef);
            result.Balance = balance;

            account.CachedBalance = balance;
            account.LastSyncUtc = DateTime.UtcNow;
            await cosmo.UpdateBrokerageAccountAsync(account);
        }
        catch (Exception ex)
        {
            result.Error = ex.Message;
        }

        return result;
    }

    /// <summary>
    /// Syncs all active non-Paper brokerage accounts for a given account.
    /// </summary>
    public virtual async Task<Dictionary<string, SyncResult>> SyncAllAccountsAsync(string accountId)
    {
        var accounts = await cosmo.GetBrokerageAccountsAsync(accountId, activeOnly: true);
        var results = new Dictionary<string, SyncResult>();

        foreach (var account in accounts.Where(a => a.Provider != BrokerageProvider.Paper))
        {
            results[account.id] = await SyncAccountAsync(account);
        }

        return results;
    }
}
