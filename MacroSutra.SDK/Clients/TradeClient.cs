using System.Net.Http.Json;
using MacroSutra.SDK.Models;

namespace MacroSutra.SDK.Clients;

public class TradeClient(HttpClient http)
{
    public async Task<List<Trade>> GetTradesAsync(string? symbol = null, string? status = null)
    {
        var url = "/api/trades";
        var queryParams = new List<string>();
        if (!string.IsNullOrEmpty(symbol)) queryParams.Add($"symbol={Uri.EscapeDataString(symbol)}");
        if (!string.IsNullOrEmpty(status)) queryParams.Add($"status={Uri.EscapeDataString(status)}");
        if (queryParams.Count > 0) url += "?" + string.Join("&", queryParams);

        return await http.GetFromJsonAsync<List<Trade>>(url, MacroSutraClient.JsonOptions) ?? new();
    }

    public async Task<Trade?> GetTradeAsync(string id)
    {
        return await http.GetFromJsonAsync<Trade>($"/api/trades/{id}", MacroSutraClient.JsonOptions);
    }
}
