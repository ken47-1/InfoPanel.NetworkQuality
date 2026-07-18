namespace InfoPanel.NetworkQuality.Calculators;

public interface IMetricCalculator
{
    void AddSample(float? rtt);
    (float Ping, float PingMin, float PingMax, float Jitter, float JitterMin, float JitterMax, float Loss) Compute();
    void Clear();
}
