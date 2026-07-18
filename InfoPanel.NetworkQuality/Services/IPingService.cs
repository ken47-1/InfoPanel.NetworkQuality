namespace InfoPanel.NetworkQuality.Services;

public interface IPingService
{
    Task<float?> SendPingAsync(string host, int timeoutMs, CancellationToken ct);
}
