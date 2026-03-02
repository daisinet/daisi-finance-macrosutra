using System.Security.Claims;
using Daisi.SDK.Web.Services;
using MacroSutra.UI.Services;

namespace MacroSutra.Web.Services;

/// <summary>
/// IAuthProvider implementation for Blazor Server wrapping Daisi.SDK.Web AuthService.
/// Exposes the user's SSO clientKey from cookie for inference billing.
/// </summary>
public class WebAuthProvider(AuthService auth, IHttpContextAccessor httpContextAccessor) : IAuthProvider
{
    public async Task<bool> IsAuthenticatedAsync()
    {
        return await auth.IsAuthenticatedAsync();
    }

    public async Task<string?> GetUserNameAsync()
    {
        return await auth.GetUserNameAsync();
    }

    public async Task<string?> GetAccountIdAsync()
    {
        return await auth.GetAccountIdAsync();
    }

    public Task<string?> GetDaisinetUserIdAsync()
    {
        var userId = httpContextAccessor.HttpContext?.User?.Claims
            .FirstOrDefault(c => c.Type == ClaimTypes.Sid)?.Value;
        return Task.FromResult(userId);
    }

    public Task<string?> GetClientKeyAsync()
    {
        var clientKey = httpContextAccessor.HttpContext?.Request.Cookies[AuthService.CLIENT_KEY_STORAGE_KEY];
        return Task.FromResult(clientKey);
    }

    public async Task LogoutAsync()
    {
        await auth.LogoutAsync();
    }
}
