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

	// Config file path: <dllname>.ini next to the plugin DLL
    public override string ConfigFilePath
    {
        get
        {
            var dllPath = GetType().Assembly.Location ?? Assembly.GetExecutingAssembly().Location;
            var dir = Path.GetDirectoryName(dllPath) ?? AppContext.BaseDirectory;
            var dllName = Path.GetFileName(dllPath); // includes .dll
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
        }
        catch
        {
            // continue with defaults
        }

        pingSender?.Dispose();
        pingSender = new Ping();

        lock (samples)
        {
            samples.Clear();
        }
    }

    public override void Load(List<IPluginContainer> containers)
    {
        var container = new PluginContainer("network", "Network Quality");

        ping = new PluginSensor("ping", "Ping", -1f, "ms");
        jitter = new PluginSensor("jitter", "Jitter", -1f, "ms");
        packetLoss = new PluginSensor("loss", "Packet loss", -1f, "%");

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

        pingSender ??= new Ping();

        float? measured;
        try
        {
            var reply = await pingSender
                .SendPingAsync(targetHost, timeoutMs)
                .ConfigureAwait(false);

            measured = reply.Status == IPStatus.Success
                ? (float)reply.RoundtripTime
                : null;
        }
        catch
        {
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

		float meanPing = -1f;
		float computedJitter = -1f;

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
            pingSender?.Dispose();
            pingSender = null;
        }
        catch { }
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
                "Host = 1.1.1.1",
                "Samples = 10",
                "TimeoutMs = 1000",
                ""
            };

            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            File.WriteAllText(path, string.Join(Environment.NewLine, template));
        }
        catch { }
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
                    line.StartsWith("#") ||
                    line.StartsWith(";") ||
                    line.StartsWith("["))
                    continue;

                var idx = line.IndexOf('=');
                if (idx <= 0) continue;

                var key = line[..idx].Trim();
                var value = line[(idx + 1)..].Trim();

                if (key.Equals("Host", StringComparison.OrdinalIgnoreCase))
                {
                    if (!string.IsNullOrEmpty(value)) targetHost = value;
                }
                else if (key.Equals("TimeoutMs", StringComparison.OrdinalIgnoreCase))
                {
					if (int.TryParse(value, out var t))
						timeoutMs = Math.Clamp(t, 100, 5000);
                }
                else if (key.Equals("Samples", StringComparison.OrdinalIgnoreCase))
                {
                    if (int.TryParse(value, out var s) && s >= 1 && s <= 200)
                        samplesWindow = s;
                }
            }

            lock (samples)
            {
                while (samples.Count > samplesWindow)
                    samples.Dequeue();
            }
        }
        catch { }
    }
}