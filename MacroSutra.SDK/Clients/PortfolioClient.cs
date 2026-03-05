using System.Net.Http.Json;
using System.Text.Json;
using MacroSutra.SDK.Models;

namespace MacroSutra.SDK.Clients;

public class PortfolioClient(HttpClient http)
{
    public async Task<List<BrokerageAccount>> GetAccountsAsync()
    {
        return await http.GetFromJsonAsync<List<BrokerageAccount>>("/api/portfolio/accounts", MacroSutraClient.JsonOptions)
            ?? new();
    }

    public async Task<List<Position>> GetPositionsAsync(string? brokerageAccountId = null)
    {
        var url = "/api/portfolio/positions";
        if (!string.IsNullOrEmpty(brokerageAccountId))
            url += $"?brokerageAccountId={Uri.EscapeDataString(brokerageAccountId)}";

        return await http.GetFromJsonAsync<List<Position>>(url, MacroSutraClient.JsonOptions) ?? new();
    }

    public async Task<SyncResult> SyncAccountAsync(string brokerageAccountId)
    {
        var response = await http.PostAsync($"/api/portfolio/accounts/{Uri.EscapeDataString(brokerageAccountId)}/sync", null);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<SyncResult>(MacroSutraClient.JsonOptions) ?? new();
    }

    public async Task<Dictionary<string, SyncResult>> SyncAllAsync()
    {
        var response = await http.PostAsync("/api/portfolio/sync", null);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<Dictionary<string, SyncResult>>(MacroSutraClient.JsonOptions) ?? new();
    }

    public async Task<BrokerageAccount?> CreateAccountAsync(BrokerageAccount account)
    {
        var response = await http.PostAsJsonAsync("/api/portfolio/accounts", account, MacroSutraClient.JsonOptions);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<BrokerageAccount>(MacroSutraClient.JsonOptions);
    }

    public async Task<BrokerageAccount?> UpdateAccountAsync(string id, BrokerageAccount account)
    {
        var response = await http.PutAsJsonAsync($"/api/portfolio/accounts/{Uri.EscapeDataString(id)}", account, MacroSutraClient.JsonOptions);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<BrokerageAccount>(MacroSutraClient.JsonOptions);
    }

    public async Task DeactivateAccountAsync(string id)
    {
        var response = await http.DeleteAsync($"/api/portfolio/accounts/{Uri.EscapeDataString(id)}");
        response.EnsureSuccessStatusCode();
    }

    // Rebalancing
    public async Task<List<JsonElement>> GetRebalanceTargetsAsync() =>
        await http.GetFromJsonAsync<List<JsonElement>>("/api/portfolio/rebalance", MacroSutraClient.JsonOptions) ?? new();

    public async Task<JsonElement?> GetRebalanceTargetAsync(string id) =>
        await http.GetFromJsonAsync<JsonElement>($"/api/portfolio/rebalance/{id}", MacroSutraClient.JsonOptions);

    public async Task<JsonElement> CreateRebalanceTargetAsync(object target)
    {
        var response = await http.PostAsJsonAsync("/api/portfolio/rebalance", target);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<JsonElement>(MacroSutraClient.JsonOptions);
    }

    public async Task<JsonElement> UpdateRebalanceTargetAsync(string id, object target)
    {
        var response = await http.PutAsJsonAsync($"/api/portfolio/rebalance/{id}", target);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<JsonElement>(MacroSutraClient.JsonOptions);
    }

    public async Task DeleteRebalanceTargetAsync(string id)
    {
        var response = await http.DeleteAsync($"/api/portfolio/rebalance/{id}");
        response.EnsureSuccessStatusCode();
    }

    public async Task<JsonElement> AnalyzeRebalanceAsync(string targetId) =>
        await http.GetFromJsonAsync<JsonElement>($"/api/portfolio/rebalance/{targetId}/analyze", MacroSutraClient.JsonOptions);

    public async Task<List<Trade>> ExecuteRebalanceAsync(string targetId)
    {
        var response = await http.PostAsync($"/api/portfolio/rebalance/{targetId}/execute", null);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<Trade>>(MacroSutraClient.JsonOptions) ?? new();
    }

    // Tax-loss harvesting
    public async Task<JsonElement> GetTaxLossHarvestingReportAsync(string? brokerageAccountId = null)
    {
        var url = "/api/portfolio/tax-loss-harvesting";
        if (!string.IsNullOrEmpty(brokerageAccountId))
            url += $"?brokerageAccountId={Uri.EscapeDataString(brokerageAccountId)}";
        return await http.GetFromJsonAsync<JsonElement>(url, MacroSutraClient.JsonOptions);
    }

    // Options
    public async Task<JsonElement> GetOptionsChainAsync(string brokerageAccountId, string symbol, DateOnly? expiration = null)
    {
        var url = $"/api/options/chain?brokerageAccountId={Uri.EscapeDataString(brokerageAccountId)}&symbol={Uri.EscapeDataString(symbol)}";
        if (expiration.HasValue) url += $"&expiration={expiration.Value:yyyy-MM-dd}";
        return await http.GetFromJsonAsync<JsonElement>(url, MacroSutraClient.JsonOptions);
    }

    public async Task<Trade?> PlaceOptionsOrderAsync(OptionsOrderRequest request)
    {
        var response = await http.PostAsJsonAsync("/api/options/orders", request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<Trade>(MacroSutraClient.JsonOptions);
    }
}

public class SyncResult
{
    public int PositionCount { get; set; }
    public decimal? Balance { get; set; }
    public string? Error { get; set; }
}
