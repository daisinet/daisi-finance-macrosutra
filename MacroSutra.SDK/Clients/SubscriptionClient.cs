using System.Net.Http.Json;
using MacroSutra.SDK.Models;

namespace MacroSutra.SDK.Clients;

public class SubscriptionClient(HttpClient http)
{
    public async Task<List<Subscription>> GetSubscriptionsAsync()
    {
        return await http.GetFromJsonAsync<List<Subscription>>("/api/subscriptions", MacroSutraClient.JsonOptions)
            ?? new();
    }

    public async Task<Subscription?> GetSubscriptionAsync(string id)
    {
        try
        {
            return await http.GetFromJsonAsync<Subscription>($"/api/subscriptions/{id}", MacroSutraClient.JsonOptions);
        }
        catch
        {
            return null;
        }
    }

    public async Task<Subscription> SubscribeAsync(CreateSubscriptionRequest request)
    {
        var response = await http.PostAsJsonAsync("/api/subscriptions", request, MacroSutraClient.JsonOptions);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<Subscription>(MacroSutraClient.JsonOptions)
            ?? new Subscription();
    }

    public async Task CancelAsync(string id)
    {
        var response = await http.DeleteAsync($"/api/subscriptions/{id}");
        response.EnsureSuccessStatusCode();
    }

    public async Task<List<SubscriptionAction>> GetActionsAsync(string subscriptionId)
    {
        return await http.GetFromJsonAsync<List<SubscriptionAction>>(
            $"/api/subscriptions/{subscriptionId}/actions", MacroSutraClient.JsonOptions)
            ?? new();
    }

    public async Task<List<Subscription>> GetPublisherSubscriptionsAsync()
    {
        return await http.GetFromJsonAsync<List<Subscription>>(
            "/api/subscriptions/publisher", MacroSutraClient.JsonOptions)
            ?? new();
    }
}
