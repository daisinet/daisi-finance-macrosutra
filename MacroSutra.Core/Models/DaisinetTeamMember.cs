namespace MacroSutra.Core.Models;

/// <summary>
/// Lightweight representation of a Daisinet account user available for import.
/// </summary>
public class DaisinetTeamMember
{
    public string DaisinetUserId { get; set; } = "";
    public string Name { get; set; } = "";
    public string Email { get; set; } = "";
    public string DaisinetRole { get; set; } = "";
}
