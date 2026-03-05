using System.Net.Http.Json;
using System.Text.Json;

namespace MacroSutra.SDK.Clients;

public class DcaClient(HttpClient http)
{
    public async Task<List<JsonElement>> GetSchedulesAsync() =>
        await http.GetFromJsonAsync<List<JsonElement>>("/api/dca", MacroSutraClient.JsonOptions) ?? new();

    public async Task<JsonElement?> GetScheduleAsync(string id) =>
        await http.GetFromJsonAsync<JsonElement>($"/api/dca/{id}", MacroSutraClient.JsonOptions);

    public async Task<JsonElement> CreateScheduleAsync(object schedule)
    {
        var response = await http.PostAsJsonAsync("/api/dca", schedule);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<JsonElement>(MacroSutraClient.JsonOptions);
    }

    public async Task<JsonElement> UpdateScheduleAsync(string id, object schedule)
    {
        var response = await http.PutAsJsonAsync($"/api/dca/{id}", schedule);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<JsonElement>(MacroSutraClient.JsonOptions);
    }

    public async Task DeleteScheduleAsync(string id)
    {
        var response = await http.DeleteAsync($"/api/dca/{id}");
        response.EnsureSuccessStatusCode();
    }

    public async Task<JsonElement> ActivateScheduleAsync(string id)
    {
        var response = await http.PostAsync($"/api/dca/{id}/activate", null);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<JsonElement>(MacroSutraClient.JsonOptions);
    }

    public async Task<JsonElement> DeactivateScheduleAsync(string id)
    {
        var response = await http.PostAsync($"/api/dca/{id}/deactivate", null);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<JsonElement>(MacroSutraClient.JsonOptions);
    }
}
