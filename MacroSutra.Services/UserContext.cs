using MacroSutra.Core.Models;

namespace MacroSutra.Services;

/// <summary>
/// Scoped session holder for the current authenticated user.
/// Set by the layout after auth resolution, consumed by pages and services.
/// </summary>
public class UserContext
{
    public MacroSutraUser? CurrentUser { get; set; }
    public string UserId => CurrentUser?.id ?? "";
    public string UserName => CurrentUser?.Name ?? "";
}
