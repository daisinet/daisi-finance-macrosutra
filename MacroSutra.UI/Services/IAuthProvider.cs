namespace MacroSutra.UI.Services;

/// <summary>
/// Abstraction for authentication, implemented differently by Web (SSO) and MAUI (SecureStorage).
/// </summary>
public interface IAuthProvider
{
    Task<bool> IsAuthenticatedAsync();
    Task<string?> GetUserNameAsync();
    Task<string?> GetAccountIdAsync();
    Task<string?> GetDaisinetUserIdAsync();
    Task<string?> GetClientKeyAsync();
    Task LogoutAsync();
}
