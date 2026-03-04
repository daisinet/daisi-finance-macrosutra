using System.Net.Http.Json;
using MacroSutra.SDK.Models;

namespace MacroSutra.SDK.Clients;

public class PushClient(HttpClient http)
{
    public async Task<PushTokenResponse?> RegisterTokenAsync(PushTokenRequest request)
    {
        var response = await http.PostAsJsonAsync("/api/push-tokens", request, MacroSutraClient.JsonOptions);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<PushTokenResponse>(MacroSutraClient.JsonOptions);
    }

    public async Task RemoveTokenAsync(string id)
    {
        var response = await http.DeleteAsync($"/api/push-tokens/{Uri.EscapeDataString(id)}");
        response.EnsureSuccessStatusCode();
    }
}
