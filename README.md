# InfoPanel.NetworkQuality

ICMP-based network quality plugin for **InfoPanel**.

Reports real-time network health metrics:
- **Ping** (ms) — average round-trip time
- **Jitter** (ms) — mean absolute delta between consecutive RTTs
- **Packet loss** (%) — percentage of timed-out pings

Designed to be lightweight, predictable, and suitable for continuous display.

## Features

- Periodic ICMP echo sampling (1 second interval)
- Configurable target host and timeout
- Time‑based sliding window for metric calculation
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
|-------|------|---------|-------------|
| `Host` | string | `1.1.1.1` | Target IP or hostname to ping |
| `TimeWindowSec` | int | `30` | Sliding window duration (seconds). Metrics are calculated over samples within this window. Valid range: 5–3600. |
| `TimeoutMs` | int | `1000` | Per-ping timeout in milliseconds. Valid range: 100–5000. |

## How Metrics Are Calculated

| Metric | Calculation |
|--------|-------------|
| **Ping** | Average RTT of all successful pings in the time window |
| **Jitter** | Mean absolute difference between consecutive successful RTTs |
| **Packet Loss** | `(timed_out / total_samples) * 100` |

## Notes

- Only `IPStatus.Success` and `IPStatus.TimedOut` are processed. Other statuses (e.g., TTL expired, destination unreachable) are ignored.
- Network exceptions are caught and do not add samples for that interval.
- The plugin uses an async update loop with a 1-second interval.

## Credits

This project was developed with human oversight and AI-assisted code generation.