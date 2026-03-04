using System.Net.Http.Json;
using MacroSutra.SDK.Models;

namespace MacroSutra.SDK.Clients;

public class StrategyClient(HttpClient http)
{
    public async Task<List<TradingStrategy>> GetStrategiesAsync()
    {
        return await http.GetFromJsonAsync<List<TradingStrategy>>("/api/strategies", MacroSutraClient.JsonOptions)
            ?? new();
    }

    public async Task<TradingStrategy?> GetStrategyAsync(string id)
    {
        return await http.GetFromJsonAsync<TradingStrategy>($"/api/strategies/{id}", MacroSutraClient.JsonOptions);
    }

    public async Task<TradingStrategy?> CreateStrategyAsync(TradingStrategy strategy)
    {
        var response = await http.PostAsJsonAsync("/api/strategies", strategy, MacroSutraClient.JsonOptions);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<TradingStrategy>(MacroSutraClient.JsonOptions);
    }

    public async Task<TradingStrategy?> UpdateStrategyAsync(string id, TradingStrategy strategy)
    {
        var response = await http.PutAsJsonAsync($"/api/strategies/{id}", strategy, MacroSutraClient.JsonOptions);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<TradingStrategy>(MacroSutraClient.JsonOptions);
    }

    public async Task DeleteStrategyAsync(string id)
    {
        var response = await http.DeleteAsync($"/api/strategies/{id}");
        response.EnsureSuccessStatusCode();
    }

    public async Task<StrategyEvaluationResult> EvaluateStrategyAsync(string id)
    {
        var response = await http.PostAsync($"/api/strategies/{id}/evaluate", null);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<StrategyEvaluationResult>(MacroSutraClient.JsonOptions)
            ?? new StrategyEvaluationResult();
    }

    public async Task<TradingStrategy?> ActivateStrategyAsync(string id)
    {
        var response = await http.PostAsync($"/api/strategies/{id}/activate", null);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<TradingStrategy>(MacroSutraClient.JsonOptions);
    }

    public async Task<TradingStrategy?> DeactivateStrategyAsync(string id)
    {
        var response = await http.PostAsync($"/api/strategies/{id}/deactivate", null);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<TradingStrategy>(MacroSutraClient.JsonOptions);
    }

    public async Task<List<StrategyTemplate>> GetTemplatesAsync()
    {
        return await http.GetFromJsonAsync<List<StrategyTemplate>>("/api/strategies/templates", MacroSutraClient.JsonOptions)
            ?? new();
    }

    public async Task<StrategyTemplate?> GetTemplateAsync(string id)
    {
        return await http.GetFromJsonAsync<StrategyTemplate>($"/api/strategies/templates/{id}", MacroSutraClient.JsonOptions);
    }
}
