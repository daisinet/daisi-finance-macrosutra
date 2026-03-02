namespace MacroSutra.SDK;

/// <summary>
/// Factory for creating MacroSutraClient instances.
/// Stores the baseUrl; Create takes the user's SSO clientKey
/// so each client is authenticated as the correct user.
/// </summary>
public class MacroSutraClientFactory
{
    private readonly string _baseUrl;

    public MacroSutraClientFactory(string baseUrl)
    {
        _baseUrl = baseUrl;
    }

    public MacroSutraClient Create(string userClientKey)
    {
        return new MacroSutraClient(_baseUrl, userClientKey);
    }
}
