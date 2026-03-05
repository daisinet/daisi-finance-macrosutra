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

/// <summary>
/// Charles Schwab OAuth 2.0 credentials.
/// </summary>
public class SchwabCredentials
{
    public string AppKey { get; set; } = "";
    public string AppSecret { get; set; } = "";
    public string AccessToken { get; set; } = "";
    public string RefreshToken { get; set; } = "";
    public DateTime? TokenExpiresUtc { get; set; }
}

/// <summary>
/// Tradier bearer token credentials.
/// </summary>
public class TradierCredentials
{
    public string AccessToken { get; set; } = "";
    public string AccountNumber { get; set; } = "";
}

/// <summary>
/// Tastytrade session-based credentials.
/// </summary>
public class TastytradeCredentials
{
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
    public string SessionToken { get; set; } = "";
    public DateTime? TokenExpiresUtc { get; set; }
}

/// <summary>
/// TradeStation OAuth 2.0 credentials.
/// </summary>
public class TradeStationCredentials
{
    public string ClientId { get; set; } = "";
    public string ClientSecret { get; set; } = "";
    public string AccessToken { get; set; } = "";
    public string RefreshToken { get; set; } = "";
    public DateTime? TokenExpiresUtc { get; set; }
}

/// <summary>
/// Public.com API key credentials.
/// </summary>
public class PublicComCredentials
{
    public string ApiKey { get; set; } = "";
    public string SecretKey { get; set; } = "";
}

/// <summary>
/// Interactive Brokers TWS API connection credentials.
/// </summary>
public class InteractiveBrokersCredentials
{
    public string Host { get; set; } = "127.0.0.1";
    public int Port { get; set; } = 7497;
    public int ClientId { get; set; } = 1;
}

/// <summary>
/// moomoo OpenAPI credentials (requires OpenD gateway).
/// </summary>
public class MoomooCredentials
{
    public string Host { get; set; } = "127.0.0.1";
    public int Port { get; set; } = 11111;
    public string TradingPassword { get; set; } = "";
    public string SecurityFirm { get; set; } = "FutuSecurities";
}

/// <summary>
/// Robinhood crypto API credentials with Ed25519 signing.
/// </summary>
public class RobinhoodCredentials
{
    public string ApiKey { get; set; } = "";
    public string ApiSecret { get; set; } = "";
    public string Base64PrivateKey { get; set; } = "";
}
