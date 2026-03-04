using System.Net.Http.Json;
using MacroSutra.SDK.Models;

namespace MacroSutra.SDK.Clients;

public class BacktestClient(HttpClient http)
{
    public async Task<BacktestResult> RunBacktestAsync(BacktestRequest request)
    {
        var response = await http.PostAsJsonAsync("/api/backtests", request, MacroSutraClient.JsonOptions);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<BacktestResult>(MacroSutraClient.JsonOptions)
            ?? new BacktestResult();
    }

    public async Task<List<BacktestResult>> GetBacktestsAsync(string? strategyId = null)
    {
        var url = "/api/backtests";
        if (!string.IsNullOrEmpty(strategyId))
            url += $"?strategyId={strategyId}";
        return await http.GetFromJsonAsync<List<BacktestResult>>(url, MacroSutraClient.JsonOptions)
            ?? new();
    }

    public async Task<BacktestResult?> GetBacktestAsync(string id)
    {
        return await http.GetFromJsonAsync<BacktestResult>($"/api/backtests/{id}", MacroSutraClient.JsonOptions);
    }

    public async Task DeleteBacktestAsync(string id)
    {
        var response = await http.DeleteAsync($"/api/backtests/{id}");
        response.EnsureSuccessStatusCode();
    }

    public async Task<WalkForwardResult> RunWalkForwardAsync(WalkForwardRequest request)
    {
        var response = await http.PostAsJsonAsync("/api/backtests/walk-forward", request, MacroSutraClient.JsonOptions);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<WalkForwardResult>(MacroSutraClient.JsonOptions)
            ?? new WalkForwardResult();
    }
}
