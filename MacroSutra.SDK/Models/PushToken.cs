namespace MacroSutra.SDK.Models;

public class PushTokenRequest
{
    public string Token { get; set; } = "";
    public string Platform { get; set; } = "";
    public string? DeviceName { get; set; }
}

public class PushTokenResponse
{
    public string Id { get; set; } = "";
    public string AccountId { get; set; } = "";
    public string Token { get; set; } = "";
    public string Platform { get; set; } = "";
    public bool IsActive { get; set; } = true;
}
