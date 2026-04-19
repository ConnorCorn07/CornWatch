# 🌽 CornWatch — Windows System Health Dashboard

> *"Keeping an eye on your kernel"*

A companion utility to [Win11 Optimizer](https://github.com/ConnorCorn07/win11op).  
Real-time system health monitoring with a terminal-aesthetic WebView2 dashboard.

---

## Features

| Panel | What it shows |
|---|---|
| **CPU Gauge** | Total usage ring, sparkline history, temperature |
| **RAM Gauge** | Usage ring, sparkline, used/total GB |
| **Network Chart** | Live send/recv graph, Mbps readouts |
| **CPU Core Heatmap** | Per-core colour-coded usage grid |
| **Disk I/O & Space** | R/W MB/s, per-drive fill bars |
| **Top Processes** | Watchdog table (future) |
| **Health Alerts** | Auto-generated warnings + critical flags |
| **Health Score** | Composite 0–100 rating |

---

## Architecture

```
CornWatch/
├── Program.cs                   Entry point
├── Core/
│   └── SystemMonitor.cs         Background polling engine (1-second ticks)
├── Models/
│   └── SystemSnapshot.cs        Data model serialised to JSON for the dashboard
└── UI/
    └── Dashboard/
        ├── MainForm.cs           WinForms host + WebView2 container
        └── dashboard.html        HTML/CSS/JS dashboard frontend
```

**Data flow:**
```
System APIs (PerformanceCounter + WMI)
       ↓
  SystemMonitor (background thread)
       ↓  SystemSnapshot (JSON)
  MainForm.PushToJs()
       ↓  ExecuteScriptAsync
  dashboard.html → window.cornWatch.onSnapshot()
       ↓
  DOM updates (gauges, charts, alerts)
```

---

## Tech Stack

- **C# .NET 8 + WinForms** — app host, system polling
- **PerformanceCounter** — CPU, disk, network live metrics
- **WMI (System.Management)** — CPU name, temperature, RAM
- **Microsoft.Web.WebView2** — Chromium-based HTML panel
- **Vanilla HTML/CSS/JS** — dashboard frontend (no frameworks needed)

---

## Getting Started

```bash
# Prerequisites: .NET 8 SDK, Windows 10/11, WebView2 Runtime
dotnet restore
dotnet run
```

> **Note:** Temperature readings via WMI may require running as Administrator.

---

## Planned Features

- [ ] Process watchdog table (live top-N processes by CPU/RAM)
- [ ] GPU monitoring (NVIDIA/AMD via native APIs)
- [ ] Historical data persistence (SQLite)
- [ ] Configurable alert thresholds
- [ ] System tray mode with balloon notifications
- [ ] Dark/light theme toggle
- [ ] Export snapshot as PNG/JSON

---

## Related Projects

- [Win11 Optimizer](https://github.com/ConnorCorn07/win11op) — Windows performance & privacy tweaks
- [CornDownloader](https://github.com/ConnorCorn07/CornDownloader) — Utility auto-downloader for fresh installs
