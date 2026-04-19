namespace CornWatch.Models;

/// <summary>
/// A point-in-time snapshot of system health metrics.
/// Serialized to JSON and sent to the WebView2 dashboard via JS bridge.
/// </summary>
public class SystemSnapshot
{
    public DateTime Timestamp { get; set; } = DateTime.Now;

    // ── CPU ──────────────────────────────────────────────────────────────────
    public float CpuTotalUsage { get; set; }         // 0–100%
    public float[] CpuCoreUsages { get; set; } = [];  // per-core, 0–100%
    public float CpuTemperature { get; set; }         // °C (WMI, may be 0 if unavailable)
    public int CpuBaseSpeedMhz { get; set; }
    public string CpuName { get; set; } = string.Empty;

    // ── RAM ──────────────────────────────────────────────────────────────────
    public long RamTotalBytes { get; set; }
    public long RamUsedBytes { get; set; }
    public float RamUsagePercent => RamTotalBytes > 0
        ? (float)RamUsedBytes / RamTotalBytes * 100f
        : 0f;

    // ── Disk ─────────────────────────────────────────────────────────────────
    public List<DiskInfo> Disks { get; set; } = [];
    public float DiskReadMbps { get; set; }
    public float DiskWriteMbps { get; set; }

    // ── Network ──────────────────────────────────────────────────────────────
    public float NetworkSentMbps { get; set; }
    public float NetworkReceivedMbps { get; set; }
    public string ActiveAdapterName { get; set; } = string.Empty;

    // ── GPU (future) ─────────────────────────────────────────────────────────
    public float GpuUsage { get; set; }
    public float GpuTemperature { get; set; }
    public string GpuName { get; set; } = string.Empty;

    // ── Health Score ─────────────────────────────────────────────────────────
    /// <summary>
    /// Composite score 0–100. 100 = perfect health.
    /// Deductions: high CPU temp, high RAM usage, low disk space.
    /// </summary>
    public int HealthScore { get; set; }

    // ── Alerts ───────────────────────────────────────────────────────────────
    public List<HealthAlert> Alerts { get; set; } = [];
}

public class DiskInfo
{
    public string DriveLetter { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public long TotalBytes { get; set; }
    public long FreeBytes { get; set; }
    public float UsagePercent => TotalBytes > 0
        ? (float)(TotalBytes - FreeBytes) / TotalBytes * 100f
        : 0f;
    public string DriveFormat { get; set; } = string.Empty;
}

public class HealthAlert
{
    public AlertSeverity Severity { get; set; }
    public string Message { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty; // "CPU", "RAM", "Disk", "Network"
}

public enum AlertSeverity { Info, Warning, Critical }
