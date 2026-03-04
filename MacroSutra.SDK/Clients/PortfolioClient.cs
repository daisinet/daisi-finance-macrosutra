using System.Net.Http.Json;
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
}

public class SyncResult
{
    public int PositionCount { get; set; }
    public decimal? Balance { get; set; }
    public string? Error { get; set; }
}
