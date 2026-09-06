# InfoPanel.NetworkQuality

ICMP-based network quality plugin for InfoPanel.

Reports real-time network health metrics:
- Ping (ms) — average round-trip time
- Ping (min) (ms) — minimum round-trip time
- Ping (max) (ms) — maximum round-trip time
- Jitter (ms) — mean absolute delta between consecutive RTTs
- Jitter (min) (ms) — minimum jitter
- Jitter (max) (ms) — maximum jitter
- Packet loss (%) — percentage of failed pings

The plugin is lightweight and suitable for continuous display.

## Features

- Periodic ICMP echo sampling at 1 second interval
- Configurable target host and timeout
- Time-based sliding window for metric calculation
- Config hot-reload: edit .ini while running, changes apply instantly
- File logging with auto-rotation
- Graceful handling of network errors
- Integrates with InfoPanel's plugin sensor system

## Installation

1. Download the latest release from GitHub.
2. Open InfoPanel.
3. Import the ZIP file using the "Import Plugin" feature.
4. Restart InfoPanel.

## Configuration

After first launch, edit the generated config file:

`InfoPanel.NetworkQuality.dll.ini`

```ini
[Network]
# Target host to ping (IP address or hostname)
Host = 1.1.1.1

# Time window in seconds (valid: 5-3600, default: 30)
TimeWindowSec = 30

# Ping timeout in milliseconds (valid: 100-5000, default: 1000)
TimeoutMs = 1000
```

### Configuration Fields

| Field | Type | Default | Description |
|---|---|---|---|
| Host | string | 1.1.1.1 | Target IP or hostname to ping. Empty falls back to 1.1.1.1. |
| TimeWindowSec | int | 30 | Sliding window duration in seconds. Valid range: 5-3600. |
| TimeoutMs | int | 1000 | Per-ping timeout in milliseconds. Valid range: 100-5000. |

### Hot-Reload

Changes to the .ini file apply instantly. You do not need to restart InfoPanel.

## How Metrics Are Calculated

| Metric | Calculation |
|---|---|
| Ping | Average RTT of successful pings in the time window |
| Ping (min) | Minimum RTT in the time window |
| Ping (max) | Maximum RTT in the time window |
| Jitter | Mean absolute difference between consecutive successful RTTs |
| Jitter (min) | Minimum absolute difference |
| Jitter (max) | Maximum absolute difference |
| Packet Loss | (failed / total_samples) * 100 |

## Logging

The plugin writes logs to:

```
%localappdata%\InfoPanel.NetworkQuality\log.txt
```

The log includes config loads, ping errors, config reloads, startup, and shutdown events. The log auto-rotates at 1 MB.

## Notes

- Any ping status other than Success counts as loss. This includes timeouts, TTL expired, destination unreachable, and exceptions.
- The plugin catches network exceptions and logs them.
- The plugin uses an async update loop with a 1-second interval.

## License

MIT