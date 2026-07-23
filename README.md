# InfoPanel.NetworkQuality

*This project was developed with AI-assisted code generation and human oversight.*

ICMP-based network quality plugin for **InfoPanel**.

Reports real-time network health metrics:
- **Ping** (ms) — average round-trip time
- **Ping (min)** (ms) — minimum round-trip time
- **Ping (max)** (ms) — maximum round-trip time
- **Jitter** (ms) — mean absolute delta between consecutive RTTs
- **Jitter (min)** (ms) — minimum jitter
- **Jitter (max)** (ms) — maximum jitter
- **Packet loss** (%) — percentage of failed pings

Designed to be lightweight, predictable, and suitable for continuous display.

## Features

- Periodic ICMP echo sampling (1 second interval)
- Configurable target host and timeout
- Time‑based sliding window for metric calculation
- Config hot-reload – edit `.ini` while running, changes apply instantly
- File logging with auto-rotation
- Graceful handling of network errors
- Integrates with InfoPanel's plugin sensor system

## Installation

1. Download the latest release from GitHub.
2. Open InfoPanel.
3. Import the ZIP file via the "Import Plugin" feature.
4. Restart InfoPanel.

## Configuration

After first launch, edit the generated config file:

`InfoPanel.NetworkQuality.dll.ini`

```ini
[Network]
# Target host to ping (IP address or hostname)
Host = 1.1.1.1

# Time window in seconds (valid: 5–3600, default: 30)
TimeWindowSec = 30

# Ping timeout in milliseconds (valid: 100–5000, default: 1000)
TimeoutMs = 1000
```

### Configuration Fields

| Field | Type | Default | Description |
|------|-------|---------|-------------|
| `Host` | string | `1.1.1.1` | Target IP or hostname to ping (empty falls back to `1.1.1.1`) |
| `TimeWindowSec` | int | `30` | Sliding window duration (seconds). Valid range: 5–3600. |
| `TimeoutMs` | int | `1000` | Per-ping timeout in milliseconds. Valid range: 100–5000. |

### Hot-Reload

Changes to the `.ini` file apply instantly – no need to restart InfoPanel.

## How Metrics Are Calculated

| Metric | Calculation |
|--------|-------------|
| **Ping** | Average RTT of successful pings in the time window |
| **Ping (min)** | Minimum RTT in the time window |
| **Ping (max)** | Maximum RTT in the time window |
| **Jitter** | Mean absolute difference between consecutive successful RTTs |
| **Jitter (min)** | Minimum absolute difference |
| **Jitter (max)** | Maximum absolute difference |
| **Packet Loss** | `(failed / total_samples) * 100` |

## Logging

The plugin logs to:

```
%localappdata%\InfoPanel.NetworkQuality\log.txt
```

Includes: config loads, ping errors, config reloads, startup/shutdown events. Auto-rotates at 1MB.

## Notes

- Any ping status other than `Success` is counted as loss (including timeouts, TTL expired, destination unreachable, and exceptions).
- Network exceptions are caught and logged.
- The plugin uses an async update loop with a 1-second interval.

## License

MIT