using MacroSutra.UI.Services;

namespace MacroSutra.App.Services;

/// <summary>
/// IAuthProvider implementation for MAUI using SecureStorage.
/// Stores the user's SSO clientKey obtained during login flow.
/// All API calls use this key so billing is attributed to the user.
/// </summary>
public class MauiAuthProvider : IAuthProvider
{
    private const string ClientKeyKey = "macrosutra_client_key";
    private const string UserNameKey = "macrosutra_user_name";
    private const string AccountIdKey = "macrosutra_account_id";
    private const string DaisinetUserIdKey = "macrosutra_daisinet_user_id";

    public async Task<bool> IsAuthenticatedAsync()
    {
        var key = await SecureStorage.GetAsync(ClientKeyKey);
        return !string.IsNullOrEmpty(key);
    }

    public async Task<string?> GetUserNameAsync()
    {
        return await SecureStorage.GetAsync(UserNameKey);
    }

    public async Task<string?> GetAccountIdAsync()
    {
        return await SecureStorage.GetAsync(AccountIdKey);
    }

    public async Task<string?> GetDaisinetUserIdAsync()
    {
        return await SecureStorage.GetAsync(DaisinetUserIdKey);
    }

    public async Task<string?> GetClientKeyAsync()
    {
        return await SecureStorage.GetAsync(ClientKeyKey);
    }

    public Task LogoutAsync()
    {
        SecureStorage.Remove(ClientKeyKey);
        SecureStorage.Remove(UserNameKey);
        SecureStorage.Remove(AccountIdKey);
        SecureStorage.Remove(DaisinetUserIdKey);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Stores authentication data after successful SSO login.
    /// </summary>
    public async Task SetAuthDataAsync(string clientKey, string userName, string accountId, string daisinetUserId)
    {
        await SecureStorage.SetAsync(ClientKeyKey, clientKey);
        await SecureStorage.SetAsync(UserNameKey, userName);
        await SecureStorage.SetAsync(AccountIdKey, accountId);
        await SecureStorage.SetAsync(DaisinetUserIdKey, daisinetUserId);
    }
}
