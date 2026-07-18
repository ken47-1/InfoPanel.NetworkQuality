namespace InfoPanel.NetworkQuality.Config;

public interface IConfigManager
{
    string Host { get; }
    int TimeWindowSec { get; }
    int TimeoutMs { get; }
    void Load();
    void EnsureConfigExists();
    void StartWatching(Action onChanged);
    void StopWatching();
}
