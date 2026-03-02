namespace MacroSutra.SDK.Models;

public class TradingStrategy
{
    public string Id { get; set; } = "";
    public string AccountId { get; set; } = "";
    public string UserId { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public List<string> Symbols { get; set; } = new();
    public bool IsActive { get; set; }
    public bool IsPublic { get; set; }
    public DateTime CreatedUtc { get; set; }
}
