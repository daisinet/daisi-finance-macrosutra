using MacroSutra.Core.Enums;

namespace MacroSutra.Core.Models;

/// <summary>
/// Tracks the health status of a brokerage provider for monitoring and failover.
/// </summary>
public class ProviderHealthStatus
{
    public BrokerageProvider Provider { get; set; }
    public bool IsHealthy { get; set; } = true;
    public DateTime LastCheckUtc { get; set; } = DateTime.UtcNow;
    public string? ErrorMessage { get; set; }
    public long LatencyMs { get; set; }
    public int ConsecutiveFailures { get; set; }
}
