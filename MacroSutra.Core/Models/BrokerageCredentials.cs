namespace MacroSutra.Core.Models;

/// <summary>
/// Alpaca API key credentials.
/// </summary>
public class AlpacaCredentials
{
    public string ApiKey { get; set; } = "";
    public string SecretKey { get; set; } = "";
}

/// <summary>
/// Webull OpenAPI credentials with OAuth tokens.
/// </summary>
public class WebullCredentials
{
    public string AppKey { get; set; } = "";
    public string AppSecret { get; set; } = "";
    public string AccessToken { get; set; } = "";
    public string RefreshToken { get; set; } = "";
    public DateTime? TokenExpiresUtc { get; set; }
}
