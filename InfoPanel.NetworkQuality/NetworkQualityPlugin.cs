using InfoPanel.Plugins;
using System.Net.NetworkInformation;
using System.Reflection;

namespace InfoPanel.NetworkQuality;

public class NetworkQualityPlugin : BasePlugin
{
    // Sensors
    private PluginSensor? ping;
    private PluginSensor? jitter;
    private PluginSensor? packetLoss;

    // ICMP sender
    private Ping? pingSender;

    // Config defaults
    private string targetHost = "1.1.1.1";
    private int timeoutMs = 1000;
    private int samplesWindow = 10;

    // FIFO buffer of recent samples; null == failed probe
    private readonly Queue<float?> samples = new();

    // Config file watcher
    private FileSystemWatcher? configWatcher;

    // Config file path: <dllname>.ini next to the plugin DLL
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
        : base(
            "network-quality",
            "InfoPanel.NetworkQuality",
            "ICMP-based InfoPanel plugin for monitoring ping, jitter, and packet loss"
        )
    {
    }

    public override TimeSpan UpdateInterval => TimeSpan.FromSeconds(1);

    public override void Initialize()
    {
        try
        {
            EnsureConfigExists();
            LoadConfig();

            // Watch for config file changes
            var configPath = ConfigFilePath;
            var dir = Path.GetDirectoryName(configPath);
            var fileName = Path.GetFileName(configPath);

            if (!string.IsNullOrEmpty(dir) && !string.IsNullOrEmpty(fileName))
            {
                configWatcher?.Dispose();
                configWatcher = new FileSystemWatcher(dir, fileName);
                configWatcher.Changed += (s, e) =>
                {
                    try { LoadConfig(); }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[NetworkQuality] Config auto-reload failed: {ex.Message}");
                    }
                };
                configWatcher.EnableRaisingEvents = true;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[NetworkQuality] Initialize config error: {ex.Message}");
        }

        lock (samples)
        {
            pingSender?.Dispose();
            pingSender = new Ping();
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

    public override void Update()
    {
        // intentionally unused; InfoPanel uses UpdateAsync
    }

    public override async Task UpdateAsync(CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
            return;

        if (ping == null || jitter == null || packetLoss == null)
            return;

        // Get current pingSender safely
        Ping? sender;
        lock (samples)
        {
            sender = pingSender;
        }

        if (sender == null)
        {
            lock (samples)
            {
                pingSender = new Ping();
                sender = pingSender;
            }
        }

        float? measured;
        try
        {
            var reply = await sender
                .SendPingAsync(targetHost, timeoutMs)
                .ConfigureAwait(false);

            measured = reply.Status == IPStatus.Success
                ? (float)reply.RoundtripTime
                : null;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[NetworkQuality] Ping failed: {ex.Message}");
            measured = null;
        }

        lock (samples)
        {
            samples.Enqueue(measured);
            while (samples.Count > samplesWindow)
                samples.Dequeue();
        }

        float?[] snapshot;
        lock (samples)
        {
            snapshot = samples.ToArray();
        }
        
        /*
            Ping = mean of successful probes
            Jitter = mean absolute delta between consecutive successful probes
            Loss = % failed probes over window
        */

        /* --- Packet loss --- */
        float lossPercent = 0f;
        if (snapshot.Length > 0)
        {
            int failed = snapshot.Count(s => !s.HasValue);
            lossPercent = (float)failed / snapshot.Length * 100f;
        }

        /* --- Ping & Jitter --- */
        var successes = snapshot
            .Where(s => s.HasValue)
            .Select(s => s!.Value)
            .ToList();

        float meanPing = 0f;
        float computedJitter = 0f;

        if (successes.Count > 0)
        {
            meanPing = successes.Average();
        }

        if (successes.Count >= 2)
        {
            double sumAbs = 0;
            for (int i = 1; i < successes.Count; i++)
                sumAbs += Math.Abs(successes[i] - successes[i - 1]);

            computedJitter = (float)(sumAbs / (successes.Count - 1));
        }

        ping.Value = meanPing;
        jitter.Value = computedJitter;
        packetLoss.Value = lossPercent;
    }

    public override void Close()
    {
        try
        {
            configWatcher?.Dispose();
            configWatcher = null;

            lock (samples)
            {
                pingSender?.Dispose();
                pingSender = null;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[NetworkQuality] Close error: {ex.Message}");
        }
    }

    private void EnsureConfigExists()
    {
        try
        {
            var path = ConfigFilePath;
            if (string.IsNullOrEmpty(path) || File.Exists(path))
                return;

            var template = new[]
            {
                "[Network]",
                "# Target host (IPv4, IPv6, or hostname)",
                "Host = 1.1.1.1",
                "# Window size for averaging (1-200 samples)",
                "Samples = 10",
                "# Ping timeout in milliseconds (100-5000)",
                "TimeoutMs = 1000",
                ""
            };

            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            File.WriteAllText(path, string.Join(Environment.NewLine, template));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[NetworkQuality] Config creation error: {ex.Message}");
        }
    }

    private void LoadConfig()
    {
        try
        {
            var path = ConfigFilePath;
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                return;

            foreach (var raw in File.ReadAllLines(path))
            {
                var line = raw.Trim();
                if (string.IsNullOrEmpty(line) ||
                    line.StartsWith('#') ||
                    line.StartsWith(';') ||
                    line.StartsWith('['))
                    continue;

                var idx = line.IndexOf('=');
                if (idx <= 0) continue;

                var key = line[..idx].Trim();
                var value = line[(idx + 1)..].Trim();

                if (key.Equals("Host", StringComparison.OrdinalIgnoreCase))
                {
                    if (!string.IsNullOrEmpty(value) && !value.Contains(' '))
                    {
                        targetHost = value;
                    }
                }
                else if (key.Equals("TimeoutMs", StringComparison.OrdinalIgnoreCase))
                {
                    if (int.TryParse(value, out var t))
                        timeoutMs = Math.Clamp(t, 100, 5000);
                }
                else if (key.Equals("Samples", StringComparison.OrdinalIgnoreCase))
                {
                    if (int.TryParse(value, out var s))
                        samplesWindow = Math.Clamp(s, 1, 200);
                }
            }

            lock (samples)
            {
                while (samples.Count > samplesWindow)
                    samples.Dequeue();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[NetworkQuality] Config load error: {ex.Message}");
        }
    }
}