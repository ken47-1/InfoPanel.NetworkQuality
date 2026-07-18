using InfoPanel.NetworkQuality.Models;

namespace InfoPanel.NetworkQuality.Calculators;

public class MetricCalculator : IMetricCalculator
{
    private readonly Queue<Sample> _samples = new();
    private readonly int _windowSeconds;

    public MetricCalculator(int windowSeconds)
    {
        _windowSeconds = windowSeconds;
    }

    public void AddSample(float? rtt)
    {
        _samples.Enqueue(new Sample(DateTime.UtcNow, rtt));
    }

    public (float Ping, float PingMin, float PingMax, float Jitter, float JitterMin, float JitterMax, float Loss) Compute()
    {
        var cutoff = DateTime.UtcNow - TimeSpan.FromSeconds(_windowSeconds);
        while (_samples.Count > 0 && _samples.Peek().Timestamp < cutoff)
            _samples.Dequeue();

        int total = _samples.Count;
        if (total == 0)
            return (0f, 0f, 0f, 0f, 0f, 0f, 0f);

        int timeouts = 0;
        var validRtts = new List<float>(total);

        foreach (var s in _samples)
        {
            if (s.Rtt.HasValue)
                validRtts.Add(s.Rtt.Value);
            else
                timeouts++;
        }

        float ping, pingMin, pingMax;
        if (validRtts.Count > 0)
        {
            ping = validRtts.Average();
            pingMin = validRtts.Min();
            pingMax = validRtts.Max();
        }
        else
        {
            ping = pingMin = pingMax = 0f;
        }

        float jitter, jitterMin, jitterMax;
        if (validRtts.Count >= 2)
        {
            var jitterValues = new List<float>(validRtts.Count - 1);
            for (int i = 1; i < validRtts.Count; i++)
                jitterValues.Add(Math.Abs(validRtts[i] - validRtts[i - 1]));

            jitter = jitterValues.Average();
            jitterMin = jitterValues.Min();
            jitterMax = jitterValues.Max();
        }
        else
        {
            jitter = jitterMin = jitterMax = 0f;
        }

        float loss = total > 0 ? (timeouts / (float)total) * 100f : 0f;

        return (ping, pingMin, pingMax, jitter, jitterMin, jitterMax, loss);
    }

    public void Clear() => _samples.Clear();
}
