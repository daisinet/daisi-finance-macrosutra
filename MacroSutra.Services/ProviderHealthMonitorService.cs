using System.Collections.Concurrent;
using System.Diagnostics;
using MacroSutra.Brokers;
using MacroSutra.Core.Enums;
using MacroSutra.Core.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MacroSutra.Services;

/// <summary>
/// Background service that monitors brokerage provider health every 5 minutes.
/// Tracks consecutive failures and latency for each provider.
/// </summary>
public class ProviderHealthMonitorService(
    BrokerageProviderFactory providerFactory,
    ILogger<ProviderHealthMonitorService> logger) : BackgroundService
{
    private readonly ConcurrentDictionary<BrokerageProvider, ProviderHealthStatus> _healthStatuses = new();
    private static readonly TimeSpan CheckInterval = TimeSpan.FromMinutes(5);
    internal const int UnhealthyThreshold = 3;

    /// <summary>
    /// Gets the health status of a specific provider. Returns null if not yet checked.
    /// </summary>
    public ProviderHealthStatus? GetHealth(BrokerageProvider provider)
    {
        return _healthStatuses.TryGetValue(provider, out var status) ? status : null;
    }

    /// <summary>
    /// Returns true if the provider is healthy or hasn't been checked yet.
    /// </summary>
    public bool IsHealthy(BrokerageProvider provider)
    {
        if (provider == BrokerageProvider.Paper) return true;
        return !_healthStatuses.TryGetValue(provider, out var status) || status.IsHealthy;
    }

    /// <summary>
    /// Gets all provider health statuses.
    /// </summary>
    public IReadOnlyDictionary<BrokerageProvider, ProviderHealthStatus> GetAllHealth()
    {
        return _healthStatuses;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("ProviderHealthMonitorService starting.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CheckAllProvidersAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Error during provider health check cycle.");
            }

            await Task.Delay(CheckInterval, stoppingToken);
        }
    }

    internal async Task CheckAllProvidersAsync(CancellationToken cancellationToken = default)
    {
        // Check all non-paper providers that have active accounts
        var providers = Enum.GetValues<BrokerageProvider>()
            .Where(p => p != BrokerageProvider.Paper && p != BrokerageProvider.TDAmeritrade);

        var tasks = providers.Select(p => CheckProviderAsync(p, cancellationToken));
        await Task.WhenAll(tasks);
    }

    internal async Task CheckProviderAsync(BrokerageProvider providerEnum, CancellationToken cancellationToken = default)
    {
        var status = _healthStatuses.GetOrAdd(providerEnum, _ => new ProviderHealthStatus { Provider = providerEnum });
        var sw = Stopwatch.StartNew();

        try
        {
            var provider = providerFactory.GetProvider(providerEnum);

            // Quick validation with a known-bad credential to test connectivity
            // If the provider throws NotSupportedException, it's not implemented
            var isReachable = await provider.ValidateCredentialsAsync("{}");

            // Even if credentials are invalid, if we got a response (not a connection error), provider is healthy
            sw.Stop();

            status.IsHealthy = true;
            status.LatencyMs = sw.ElapsedMilliseconds;
            status.ErrorMessage = null;
            status.ConsecutiveFailures = 0;
            status.LastCheckUtc = DateTime.UtcNow;

            logger.LogDebug("Provider {Provider} health check OK ({LatencyMs}ms)", providerEnum, sw.ElapsedMilliseconds);
        }
        catch (NotSupportedException)
        {
            // Provider not implemented — mark as healthy (not a transient failure)
            sw.Stop();
            status.IsHealthy = true;
            status.LatencyMs = sw.ElapsedMilliseconds;
            status.ErrorMessage = null;
            status.ConsecutiveFailures = 0;
            status.LastCheckUtc = DateTime.UtcNow;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            sw.Stop();
            status.ConsecutiveFailures++;
            status.LatencyMs = sw.ElapsedMilliseconds;
            status.ErrorMessage = ex.Message;
            status.LastCheckUtc = DateTime.UtcNow;

            if (status.ConsecutiveFailures >= UnhealthyThreshold)
            {
                status.IsHealthy = false;
                logger.LogWarning("Provider {Provider} marked UNHEALTHY after {Failures} consecutive failures: {Error}",
                    providerEnum, status.ConsecutiveFailures, ex.Message);
            }
            else
            {
                logger.LogDebug("Provider {Provider} health check failed ({Failures}/{Threshold}): {Error}",
                    providerEnum, status.ConsecutiveFailures, UnhealthyThreshold, ex.Message);
            }
        }
    }
}
