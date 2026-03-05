using System.Net.Http.Json;
using MacroSutra.SDK.Models;

namespace MacroSutra.SDK.Clients;

public class MarketDataClient(HttpClient http)
{
    public async Task<List<MarketBar>> GetHistoricalBarsAsync(string symbol, DateOnly from, DateOnly to, string timeFrame = "1D")
    {
        var url = $"/api/market/bars?symbol={Uri.EscapeDataString(symbol)}&from={from:yyyy-MM-dd}&to={to:yyyy-MM-dd}&timeFrame={Uri.EscapeDataString(timeFrame)}";
        return await http.GetFromJsonAsync<List<MarketBar>>(url, MacroSutraClient.JsonOptions) ?? new();
    }
}
