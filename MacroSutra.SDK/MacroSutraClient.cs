using System.Text.Json;
using MacroSutra.SDK.Clients;

namespace MacroSutra.SDK;

/// <summary>
/// Main entry point for the MacroSutra SDK.
/// Constructor takes the user's SSO clientKey so all API calls
/// are authenticated and billed to the correct user.
/// </summary>
public class MacroSutraClient : IDisposable
{
    private readonly HttpClient _http;

    internal static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public UserClient Users { get; }
    public StrategyClient Strategies { get; }
    public TradeClient Trades { get; }
    public PortfolioClient Portfolio { get; }

    public MacroSutraClient(string baseUrl, string userClientKey)
    {
        _http = new HttpClient
        {
            BaseAddress = new Uri(baseUrl)
        };
        _http.DefaultRequestHeaders.Add("x-daisi-client-key", userClientKey);

        Users = new UserClient(_http);
        Strategies = new StrategyClient(_http);
        Trades = new TradeClient(_http);
        Portfolio = new PortfolioClient(_http);
    }

    public MacroSutraClient(HttpClient httpClient)
    {
        _http = httpClient;
        Users = new UserClient(_http);
        Strategies = new StrategyClient(_http);
        Trades = new TradeClient(_http);
        Portfolio = new PortfolioClient(_http);
    }

    public void Dispose()
    {
        _http.Dispose();
    }
}
