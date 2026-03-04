using System.Net.Http.Json;
using MacroSutra.SDK.Models;

namespace MacroSutra.SDK.Clients;

public class CommunityClient(HttpClient http)
{
    public async Task<List<TradingStrategy>> GetPublicStrategiesAsync(int page = 0, int pageSize = 20, string? sortBy = null)
    {
        var url = $"/api/community/strategies?page={page}&pageSize={pageSize}";
        if (!string.IsNullOrEmpty(sortBy))
            url += $"&sortBy={sortBy}";
        return await http.GetFromJsonAsync<List<TradingStrategy>>(url, MacroSutraClient.JsonOptions)
            ?? new();
    }

    public async Task<TradingStrategy?> GetPublicStrategyAsync(string id)
    {
        return await http.GetFromJsonAsync<TradingStrategy>($"/api/community/strategies/{id}", MacroSutraClient.JsonOptions);
    }

    public async Task<StrategyCommunityStats?> GetCommunityStatsAsync(string strategyId)
    {
        try
        {
            var detail = await http.GetFromJsonAsync<PublicStrategyDetail>($"/api/community/strategies/{strategyId}", MacroSutraClient.JsonOptions);
            return detail?.Stats;
        }
        catch
        {
            return null;
        }
    }

    public async Task<List<StrategyReview>> GetReviewsAsync(string strategyId)
    {
        try
        {
            var detail = await http.GetFromJsonAsync<PublicStrategyDetail>($"/api/community/strategies/{strategyId}", MacroSutraClient.JsonOptions);
            return detail?.Reviews ?? new();
        }
        catch
        {
            return new();
        }
    }

    public async Task<List<LeaderboardEntry>> GetLeaderboardAsync(string sortBy = "sharpe", int limit = 25)
    {
        return await http.GetFromJsonAsync<List<LeaderboardEntry>>(
            $"/api/community/leaderboard?sortBy={sortBy}&limit={limit}", MacroSutraClient.JsonOptions)
            ?? new();
    }

    public async Task<TradingStrategy?> ForkStrategyAsync(string strategyId)
    {
        var response = await http.PostAsync($"/api/strategies/{strategyId}/fork", null);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<TradingStrategy>(MacroSutraClient.JsonOptions);
    }

    public async Task<StrategyReview> CreateReviewAsync(string strategyId, CreateReviewRequest request)
    {
        var response = await http.PostAsJsonAsync($"/api/strategies/{strategyId}/reviews", request, MacroSutraClient.JsonOptions);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<StrategyReview>(MacroSutraClient.JsonOptions)
            ?? new StrategyReview();
    }

    public async Task DeleteReviewAsync(string strategyId, string reviewId)
    {
        var response = await http.DeleteAsync($"/api/strategies/{strategyId}/reviews/{reviewId}");
        response.EnsureSuccessStatusCode();
    }

    private class PublicStrategyDetail
    {
        public TradingStrategy? Strategy { get; set; }
        public StrategyCommunityStats? Stats { get; set; }
        public List<StrategyReview>? Reviews { get; set; }
    }
}
