# InfoPanel.NetworkQuality

ICMP-based network quality plugin for **InfoPanel**.

Reports real-time network health metrics:
- **Ping** (ms)
- **Jitter** (ms)
- **Packet loss** (%)

Designed to be lightweight, predictable, and suitable for continuous display.

## Features

- Periodic ICMP echo sampling
- Configurable target host and sample count
- Rolling calculation of jitter and packet loss
- Integrates with InfoPanel’s plugin sensor system

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
Host = 1.1.1.1
Samples = 10
TimeoutMs = 1000
```

## Notes

This project was developed with human oversight and AI-assisted code generation.
