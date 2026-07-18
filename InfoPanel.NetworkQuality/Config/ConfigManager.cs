using InfoPanel.NetworkQuality.Logging;

namespace InfoPanel.NetworkQuality.Config;

public class ConfigManager : IConfigManager, IDisposable
{
    private string _host = "1.1.1.1";
    private int _timeWindowSec = 30;
    private int _timeoutMs = 1000;
    private readonly string _configPath;
    private FileSystemWatcher? _watcher;
    private Timer? _debounceTimer;
    private readonly object _lock = new();

    public string Host => _host;
    public int TimeWindowSec => _timeWindowSec;
    public int TimeoutMs => _timeoutMs;

    public ConfigManager(string configPath)
    {
        _configPath = configPath;
    }

    public void Load()
    {
        if (!File.Exists(_configPath)) return;

        try
        {
            var lines = File.ReadAllLines(_configPath);
            foreach (var raw in lines)
            {
                var line = raw.Trim();
                if (string.IsNullOrEmpty(line) || line.StartsWith('#') || line.StartsWith('['))
                    continue;

                var idx = line.IndexOf('=');
                if (idx <= 0) continue;

                var key = line[..idx].Trim();
                var value = line[(idx + 1)..].Trim();

                if (key.Equals("Host", StringComparison.OrdinalIgnoreCase))
                    _host = value;
                else if (key.Equals("TimeWindowSec", StringComparison.OrdinalIgnoreCase))
                    int.TryParse(value, out _timeWindowSec);
                else if (key.Equals("TimeoutMs", StringComparison.OrdinalIgnoreCase))
                    int.TryParse(value, out _timeoutMs);
            }

            if (string.IsNullOrWhiteSpace(_host))
                _host = "1.1.1.1";

            _timeWindowSec = Math.Clamp(_timeWindowSec, 5, 3600);
            _timeoutMs = Math.Clamp(_timeoutMs, 100, 5000);

            Logger.Log($"Config loaded: Host={_host}, Window={_timeWindowSec}s, Timeout={_timeoutMs}ms");
        }
        catch (Exception ex)
        {
            Logger.Log($"Failed to load config: {ex.Message}");
        }
    }

    public void EnsureConfigExists()
    {
        if (File.Exists(_configPath)) return;

        try
        {
            var dir = Path.GetDirectoryName(_configPath);
            if (dir != null)
                Directory.CreateDirectory(dir);

            var template = new[]
            {
                "[Network]",
                "# Target host to ping (IP address or hostname)",
                $"Host = {_host}",
                "",
                "# Time window in seconds (valid: 5-3600, default: 30)",
                $"TimeWindowSec = {_timeWindowSec}",
                "",
                "# Ping timeout in milliseconds (valid: 100-5000, default: 1000)",
                $"TimeoutMs = {_timeoutMs}",
                ""
            };

            File.WriteAllText(_configPath, string.Join(Environment.NewLine, template));
            Logger.Log($"Config file created at {_configPath}");
        }
        catch (Exception ex)
        {
            Logger.Log($"Failed to create config: {ex.Message}");
        }
    }

    public void StartWatching(Action onChanged)
    {
        try
        {
            var dir = Path.GetDirectoryName(_configPath);
            var fileName = Path.GetFileName(_configPath);

            if (string.IsNullOrEmpty(dir) || string.IsNullOrEmpty(fileName))
                return;

            _watcher = new FileSystemWatcher(dir, fileName);
            _watcher.Changed += (s, e) =>
            {
                lock (_lock)
                {
                    _debounceTimer?.Dispose();
                    _debounceTimer = new Timer(_ =>
                    {
                        Logger.Log("Config file changed, reloading...");
                        Load();
                        onChanged?.Invoke();
                        Logger.Log("Config reloaded successfully");
                    }, null, 300, Timeout.Infinite);
                }
            };
            _watcher.EnableRaisingEvents = true;

            Logger.Log($"Config watcher started for {_configPath}");
        }
        catch (Exception ex)
        {
            Logger.Log($"Failed to start config watcher: {ex.Message}");
        }
    }

    public void StopWatching()
    {
        _debounceTimer?.Dispose();
        _debounceTimer = null;
        _watcher?.Dispose();
        _watcher = null;
    }

    public void Dispose() => StopWatching();
}
