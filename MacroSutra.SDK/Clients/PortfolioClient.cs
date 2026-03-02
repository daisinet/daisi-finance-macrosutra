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
}
