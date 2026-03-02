using MacroSutra.Core.Enums;
using MacroSutra.Core.Models;
using MacroSutra.Data;

namespace MacroSutra.Services;

/// <summary>
/// Manages MacroSutra user records.
/// First user is auto-provisioned as Owner. Prevents deactivation of the last Owner.
/// </summary>
public class UserManagementService(MacroSutraCosmo cosmo)
{
    public virtual async Task<MacroSutraUser> CreateUserAsync(MacroSutraUser user)
    {
        return await cosmo.CreateUserAsync(user);
    }

    public virtual async Task<MacroSutraUser?> GetUserAsync(string id, string accountId)
    {
        return await cosmo.GetUserAsync(id, accountId);
    }

    public virtual async Task<MacroSutraUser?> GetUserByDaisinetIdAsync(string daisinetUserId, string accountId)
    {
        return await cosmo.GetUserByDaisinetIdAsync(daisinetUserId, accountId);
    }

    public virtual async Task<List<MacroSutraUser>> GetUsersAsync(string accountId, bool activeOnly = false)
    {
        return await cosmo.GetUsersAsync(accountId, activeOnly);
    }

    public virtual async Task<MacroSutraUser> UpdateUserAsync(MacroSutraUser user)
    {
        return await cosmo.UpdateUserAsync(user);
    }

    /// <summary>
    /// Deactivates a user. Prevents deactivating the last active Owner.
    /// </summary>
    public virtual async Task<MacroSutraUser> DeactivateUserAsync(string id, string accountId)
    {
        var user = await cosmo.GetUserAsync(id, accountId)
            ?? throw new InvalidOperationException("User not found.");

        if (user.Role == MacroSutraRole.Owner)
        {
            var allUsers = await cosmo.GetUsersAsync(accountId, activeOnly: true);
            var activeOwners = allUsers.Count(u => u.Role == MacroSutraRole.Owner && u.id != id);
            if (activeOwners == 0)
                throw new InvalidOperationException("Cannot deactivate the last active Owner.");
        }

        user.IsActive = false;
        return await cosmo.UpdateUserAsync(user);
    }

    /// <summary>
    /// Reactivates a previously deactivated user.
    /// </summary>
    public virtual async Task<MacroSutraUser> ReactivateUserAsync(string id, string accountId)
    {
        var user = await cosmo.GetUserAsync(id, accountId)
            ?? throw new InvalidOperationException("User not found.");

        user.IsActive = true;
        return await cosmo.UpdateUserAsync(user);
    }
}
