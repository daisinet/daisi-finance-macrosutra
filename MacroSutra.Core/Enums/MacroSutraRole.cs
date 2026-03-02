namespace MacroSutra.Core.Enums;

/// <summary>
/// Roles for MacroSutra users, ordered by privilege level for >= comparisons.
/// </summary>
public enum MacroSutraRole
{
    Viewer = 0,
    Trader = 1,
    Manager = 2,
    Owner = 3
}
