namespace MacroSutra.Core.Enums;

/// <summary>
/// What a subscription does when the source strategy fires.
/// </summary>
public enum SubscriptionActionType
{
    Mirror = 0,
    Alert = 1,
    ScaledMirror = 2,
    Webhook = 3,
    Email = 4,
    Push = 5
}
