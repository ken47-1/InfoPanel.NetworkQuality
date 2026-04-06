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
    
        // EMA state
    private float emaLoss = 0f;
    private float emaSmoothing = 0.035f;

    // FIFO buffer for Ping/Jitter
    private readonly Queue<float?> samples = new();

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
        : base("network-quality", "InfoPanel.NetworkQuality", "EMA-based Network Monitor")
    {
    }

    public override TimeSpan UpdateInterval => TimeSpan.FromSeconds(1);

    public override void Initialize()
    {
        EnsureConfigExists();
        LoadConfig();

        lock (samples)
        {
            pingSender?.Dispose();
            pingSender = new Ping();
            samples.Clear();
            emaLoss = 0f; // Reset average on init
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
        if (cancellationToken.IsCancellationRequested || ping == null || jitter == null || packetLoss == null)
            return;

        Ping? sender;
        lock (samples) { sender = pingSender; }
        if (sender == null) return;

        float? measured;
        try
        {
            var reply = await sender.SendPingAsync(targetHost, timeoutMs).ConfigureAwait(false);
            measured = reply.Status == IPStatus.Success ? (float)reply.RoundtripTime : null;
        }
        catch { measured = null; }

        // --- EMA Packet Loss Calculation ---
        // If it failed, instant loss is 100%. If it worked, it's 0%.
        float instantLoss = measured.HasValue ? 0f : 100f;
        
        // Formula: NewValue = (Current * Alpha) + (OldValue * (1 - Alpha))
        emaLoss = (instantLoss * emaSmoothing) + (emaLoss * (1f - emaSmoothing));

        lock (samples)
        {
            samples.Enqueue(measured);
            while (samples.Count > samplesWindow) samples.Dequeue();
        }

        // --- Ping & Jitter logic stays the same ---
        var successes = samples.Where(s => s.HasValue).Select(s => s!.Value).ToList();
        if (successes.Count > 0) ping.Value = successes.Average();
        
        if (successes.Count >= 2)
        {
            double sumAbs = 0;
            for (int i = 1; i < successes.Count; i++)
                sumAbs += Math.Abs(successes[i] - successes[i - 1]);
            jitter.Value = (float)(sumAbs / (successes.Count - 1));
        }

        packetLoss.Value = emaLoss;
    }

    private void EnsureConfigExists()
    {
        var path = ConfigFilePath;
        if (File.Exists(path)) return;

        var template = new[]
        {
            "[Network]",
            "# Target host (IPv4, IPv6, or hostname)",
            $"Host = {targetHost}",
            "",
            "# Window size for ping/jitter averaging",
            $"Samples = {samplesWindow}",
            "",
            "# EMA Smoothing (0.001 to 1.0). 0.02 is roughly a 100-sample window.",
            $"Smoothing = {emaSmoothing}",
            "",
            "# Ping timeout in milliseconds",
            $"TimeoutMs = {timeoutMs}",
            ""
        };
        File.WriteAllText(path, string.Join(Environment.NewLine, template));
    }

    private void LoadConfig()
    {
        var path = ConfigFilePath;
        if (!File.Exists(path)) return;

        foreach (var raw in File.ReadAllLines(path))
        {
            var line = raw.Trim();
            if (string.IsNullOrEmpty(line) || line.StartsWith('#') || line.StartsWith('[')) continue;

            var idx = line.IndexOf('=');
            if (idx <= 0) continue;

            var key = line[..idx].Trim();
            var value = line[(idx + 1)..].Trim();

            if (key.Equals("Host", StringComparison.OrdinalIgnoreCase)) targetHost = value;
            else if (key.Equals("Samples", StringComparison.OrdinalIgnoreCase)) 
                int.TryParse(value, out samplesWindow);
            else if (key.Equals("Smoothing", StringComparison.OrdinalIgnoreCase)) 
                float.TryParse(value, out emaSmoothing);
            else if (key.Equals("TimeoutMs", StringComparison.OrdinalIgnoreCase)) 
                int.TryParse(value, out timeoutMs);
        }
    }

    public override void Close()
    {
        pingSender?.Dispose();
    }
}