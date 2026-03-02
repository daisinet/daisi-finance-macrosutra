using System.Net.Http.Json;
using MacroSutra.SDK.Models;

namespace MacroSutra.SDK.Clients;

public class UserClient(HttpClient http)
{
    public async Task<MacroSutraUser?> GetCurrentUserAsync()
    {
        return await http.GetFromJsonAsync<MacroSutraUser>("/api/users/me", MacroSutraClient.JsonOptions);
    }

    public async Task<List<MacroSutraUser>> GetUsersAsync()
    {
        return await http.GetFromJsonAsync<List<MacroSutraUser>>("/api/users", MacroSutraClient.JsonOptions)
            ?? new();
    }
}
