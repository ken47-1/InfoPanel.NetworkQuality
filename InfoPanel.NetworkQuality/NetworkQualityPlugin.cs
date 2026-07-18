using InfoPanel.Plugins;
using InfoPanel.NetworkQuality.Calculators;
using InfoPanel.NetworkQuality.Config;
using InfoPanel.NetworkQuality.Logging;
using InfoPanel.NetworkQuality.Services;
using System.Reflection;

namespace InfoPanel.NetworkQuality;

public class NetworkQualityPlugin : BasePlugin
{
    private PluginSensor? _ping;
    private PluginSensor? _pingMin;
    private PluginSensor? _pingMax;
    private PluginSensor? _jitter;
    private PluginSensor? _jitterMin;
    private PluginSensor? _jitterMax;
    private PluginSensor? _loss;

    private readonly IPingService _pingService;
    private readonly IConfigManager _config;
    private IMetricCalculator _calculator;

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
        : base("network-quality", "InfoPanel.NetworkQuality", "Network quality monitor")
    {
        _pingService = new PingService();
        _config = new ConfigManager(ConfigFilePath);
        _calculator = new MetricCalculator(_config.TimeWindowSec);
    }

    public override TimeSpan UpdateInterval => TimeSpan.FromSeconds(1);

    public override void Initialize()
    {
        Logger.Log("Plugin initializing");

        _config.EnsureConfigExists();
        _config.Load();
        _calculator = new MetricCalculator(_config.TimeWindowSec);

        _config.StartWatching(OnConfigReloaded);

        Logger.Log("Plugin initialized successfully");
    }

    private void OnConfigReloaded()
    {
        _calculator = new MetricCalculator(_config.TimeWindowSec);
        Logger.Log("Calculator reset with new window size");
    }

    public override void Load(List<IPluginContainer> containers)
    {
        var container = new PluginContainer("network", "Network Quality");

        _ping = new PluginSensor("ping", "Ping", 0f, "ms");
        _pingMin = new PluginSensor("ping_min", "Ping (min)", 0f, "ms");
        _pingMax = new PluginSensor("ping_max", "Ping (max)", 0f, "ms");
        _jitter = new PluginSensor("jitter", "Jitter", 0f, "ms");
        _jitterMin = new PluginSensor("jitter_min", "Jitter (min)", 0f, "ms");
        _jitterMax = new PluginSensor("jitter_max", "Jitter (max)", 0f, "ms");
        _loss = new PluginSensor("loss", "Packet loss", 0f, "%");

        container.Entries.Add(_ping);
        container.Entries.Add(_pingMin);
        container.Entries.Add(_pingMax);
        container.Entries.Add(_jitter);
        container.Entries.Add(_jitterMin);
        container.Entries.Add(_jitterMax);
        container.Entries.Add(_loss);

        containers.Add(container);
        Logger.Log("Sensors registered");
    }

    public override void Update() { }

    public override async Task UpdateAsync(CancellationToken cancellationToken)
    {
        if (_ping == null || _pingMin == null || _pingMax == null ||
            _jitter == null || _jitterMin == null || _jitterMax == null ||
            _loss == null || cancellationToken.IsCancellationRequested)
            return;

        var rtt = await _pingService.SendPingAsync(_config.Host, _config.TimeoutMs, cancellationToken);
        _calculator.AddSample(rtt);

        var (ping, pingMin, pingMax, jitter, jitterMin, jitterMax, loss) = _calculator.Compute();

        _ping.Value = ping;
        _pingMin.Value = pingMin;
        _pingMax.Value = pingMax;
        _jitter.Value = jitter;
        _jitterMin.Value = jitterMin;
        _jitterMax.Value = jitterMax;
        _loss.Value = loss;
    }

    public override void Close()
    {
        Logger.Log("Plugin closing");
        _config.StopWatching();
        _calculator.Clear();
        Logger.Log("Plugin closed");
    }
}
