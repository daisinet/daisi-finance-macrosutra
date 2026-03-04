namespace MacroSutra.Core.Models;

/// <summary>
/// A registered push notification token for a user's device.
/// Stored in the PushTokens container, partitioned by AccountId.
/// </summary>
public class PushToken
{
    public string id { get; set; } = "";
    public string Type { get; set; } = nameof(PushToken);
    public string AccountId { get; set; } = "";
    public string UserId { get; set; } = "";

    /// <summary>
    /// The FCM or APNs device token.
    /// </summary>
    public string Token { get; set; } = "";

    /// <summary>
    /// Device platform: "ios", "android", or "windows".
    /// </summary>
    public string Platform { get; set; } = "";

    /// <summary>
    /// Optional device name for user management.
    /// </summary>
    public string? DeviceName { get; set; }

    public bool IsActive { get; set; } = true;
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public DateTime? LastUsedUtc { get; set; }
}
