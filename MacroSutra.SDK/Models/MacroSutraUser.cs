using System.Text.Json.Serialization;

namespace MacroSutra.SDK.Models;

public class MacroSutraUser
{
    public string Id { get; set; } = "";
    public string AccountId { get; set; } = "";
    public string DaisinetUserId { get; set; } = "";
    public string Name { get; set; } = "";
    public string Email { get; set; } = "";

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public string Role { get; set; } = "Viewer";

    public bool IsActive { get; set; } = true;
}
