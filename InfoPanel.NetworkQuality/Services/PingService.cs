using System.Net.NetworkInformation;

namespace InfoPanel.NetworkQuality.Services;

public class PingService : IPingService
{
    public async Task<float?> SendPingAsync(string host, int timeoutMs, CancellationToken ct)
    {
        try
        {
            using var sender = new Ping();
            var reply = await sender.SendPingAsync(host, timeoutMs).ConfigureAwait(false);
            return reply.Status == IPStatus.Success ? reply.RoundtripTime : null;
        }
        catch
        {
            return null;
        }
    }
}
