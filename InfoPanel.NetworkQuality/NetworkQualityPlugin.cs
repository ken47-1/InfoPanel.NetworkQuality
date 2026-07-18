using InfoPanel.Plugins;
using System.Net.NetworkInformation;
using System.Reflection;

namespace InfoPanel.NetworkQuality;

public class NetworkQualityPlugin : BasePlugin
{
    private PluginSensor? ping;
    private PluginSensor? jitter;
    private PluginSensor? packetLoss;

    private string targetHost = "1.1.1.1";
    private int timeWindowSec = 30;   // time‑based window (seconds)
    private int timeoutMs = 1000;

    private struct Sample
    {
        public DateTime Timestamp;
        public float? Rtt; // null = loss (timeout or other failure)
    }

    private readonly Queue<Sample> samples = new();

    public override string ConfigFilePath
    {
        get
        {
            var dllPath = GetType().Assembly.Location 
                ?? Assembly.GetExecutingAssembly().Location 
                ?? Path.Combine(AppContext.BaseDirectory, "InfoPanel.NetworkQuality.dll");
            var dir = Path.GetDirectoryName(dllPath) ?? AppContext.BaseDirectory;
            var dllName = Path.GetFileNameWithoutExtension(dllPath);
            return Path.Combine(dir, $"{dllName}.ini");
        }
    }

    public NetworkQualityPlugin()
        : base("network-quality", "InfoPanel.NetworkQuality", "Time-based deterministic network monitor")
    {
    }

    public override TimeSpan UpdateInterval => TimeSpan.FromSeconds(1);

    public override void Initialize()
    {
        EnsureConfigExists();
        LoadConfig();

        lock (samples)
        {
            samples.Clear();
        }
    }

    public override void Load(List<IPluginContainer> containers)
    {
        var container = new PluginContainer("network", "Network Quality");
        ping = new PluginSensor("ping", "Ping", 0f, "ms");
        jitter = new PluginSensor("jitter", "Jitter", 0f, "ms");
        packetLoss = new PluginSensor("loss", "Packet loss", 0f, "%");

        container.Entries.Add(ping);
        container.Entries.Add(jitter);
        container.Entries.Add(packetLoss);
        containers.Add(container);
    }

    public override void Update() { }

    public override async Task UpdateAsync(CancellationToken cancellationToken)
    {
        var pingRef = ping;
        var jitterRef = jitter;
        var lossRef = packetLoss;

        if (cancellationToken.IsCancellationRequested || pingRef == null || jitterRef == null || lossRef == null)
            return;

        // Default: this attempt is a loss (null)
        float? measured = null;

        try
        {
            using var sender = new Ping();
            var reply = await sender.SendPingAsync(targetHost, timeoutMs).ConfigureAwait(false);

            // Only IPStatus.Success is considered a valid RTT.
            // Everything else (TimedOut, TtlExpired, DestinationUnreachable, etc.) is treated as loss.
            if (reply.Status == IPStatus.Success)
                measured = reply.RoundtripTime;
        }
        catch
        {
            // Exception also means loss – measured remains null
        }

        // ----- All shared state updates are inside a single lock -----
        lock (samples)
        {
            // 1. Add the new sample (one per second, always)
            samples.Enqueue(new Sample
            {
                Timestamp = DateTime.UtcNow,
                Rtt = measured
            });

            // 2. Evict samples older than the time window
            var cutoff = DateTime.UtcNow - TimeSpan.FromSeconds(timeWindowSec);
            while (samples.Count > 0 && samples.Peek().Timestamp < cutoff)
                samples.Dequeue();

            // 3. Compute metrics from the current queue
            int total = samples.Count;
            int timeouts = 0;
            var validRtts = new List<float>(total); // pre-size for performance

            foreach (var s in samples)
            {
                if (s.Rtt.HasValue)
                    validRtts.Add(s.Rtt.Value);
                else
                    timeouts++;
            }

            // Ping: average of successful RTTs
            pingRef.Value = validRtts.Count > 0 ? validRtts.Average() : 0f;

            // Jitter: mean absolute delta between consecutive valid RTTs
            if (validRtts.Count >= 2)
            {
                double sumAbs = 0;
                for (int i = 1; i < validRtts.Count; i++)
                    sumAbs += Math.Abs(validRtts[i] - validRtts[i - 1]);
                jitterRef.Value = (float)(sumAbs / (validRtts.Count - 1));
            }
            else
            {
                jitterRef.Value = 0f;
            }

            // Packet Loss: (timeouts / total) * 100  – now total includes all attempts
            lossRef.Value = total > 0 ? (timeouts / (float)total) * 100f : 0f;
        }
    }

    private void EnsureConfigExists()
    {
        var path = ConfigFilePath;
        if (File.Exists(path)) return;

        try
        {
            var dir = Path.GetDirectoryName(path);
            if (dir != null)
                Directory.CreateDirectory(dir);

            var template = new[]
            {
                "[Network]",
                "# Target host to ping (IP address or hostname)",
                $"Host = {targetHost}",
                "",
                "# Time window in seconds (valid: 5-3600, default: 30)",
                $"TimeWindowSec = {timeWindowSec}",
                "",
                "# Ping timeout in milliseconds (valid: 100-5000, default: 1000)",
                $"TimeoutMs = {timeoutMs}",
                ""
            };

            File.WriteAllText(path, string.Join(Environment.NewLine, template));
        }
        catch
        {
            // Config creation failed, continue with defaults
        }
    }

    private void LoadConfig()
    {
        var path = ConfigFilePath;
        if (!File.Exists(path)) return;

        try
        {
            foreach (var raw in File.ReadAllLines(path))
            {
                var line = raw.Trim();
                if (string.IsNullOrEmpty(line) || line.StartsWith('#') || line.StartsWith('['))
                    continue;

                var idx = line.IndexOf('=');
                if (idx <= 0) continue;

                var key = line[..idx].Trim();
                var value = line[(idx + 1)..].Trim();

                if (key.Equals("Host", StringComparison.OrdinalIgnoreCase))
                    targetHost = value;
                else if (key.Equals("TimeWindowSec", StringComparison.OrdinalIgnoreCase))
                    int.TryParse(value, out timeWindowSec);
                else if (key.Equals("TimeoutMs", StringComparison.OrdinalIgnoreCase))
                    int.TryParse(value, out timeoutMs);
            }
        }
        catch
        {
            // Config parse failed, continue with current values
        }

        timeWindowSec = Math.Clamp(timeWindowSec, 5, 3600);
        timeoutMs = Math.Clamp(timeoutMs, 100, 5000);
    }

    public override void Close()
    {
        lock (samples)
        {
            samples.Clear();
        }
    }
}
