using MacroSutra.Core.Enums;

namespace MacroSutra.Core.Models;

/// <summary>
/// A user within a MacroSutra account, linked to a Daisinet SSO user.
/// Stored in the Users container, partitioned by AccountId.
/// </summary>
public class MacroSutraUser
{
    public string id { get; set; } = "";
    public string Type { get; set; } = nameof(MacroSutraUser);
    public string AccountId { get; set; } = "";

    /// <summary>
    /// Links to the Daisinet SSO user (ClaimTypes.Sid from auth).
    /// </summary>
    public string DaisinetUserId { get; set; } = "";

    public string Name { get; set; } = "";
    public string Email { get; set; } = "";
    public MacroSutraRole Role { get; set; } = MacroSutraRole.Viewer;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedUtc { get; set; }
}
