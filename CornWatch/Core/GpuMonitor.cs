using System.Diagnostics;
using System.Management;
using CornWatch.Models;

namespace CornWatch.Core;

/// <summary>
/// Reads GPU metrics using Windows-native APIs.
/// - GPU Engine perf counters  → load % (all GPUs incl. RDNA 4)
/// - GPU Adapter Memory counters → true dedicated VRAM total (bypasses WMI 4GB cap)
/// - GPU Process Memory counters → VRAM used
/// - WMI Win32_VideoController  → GPU name
/// </summary>
public sealed class GpuMonitor : IDisposable
{
    private readonly List<PerformanceCounter> _3dCounters      = [];
    private readonly List<PerformanceCounter> _computeCounters = [];
    private readonly List<PerformanceCounter> _videoCounters   = [];
    private readonly List<PerformanceCounter> _vramCounters    = [];
    private string _dgpuLuid   = string.Empty;
    private string _dgpuName   = "GPU";
    private float  _dgpuVramMb = 0f;
    private bool   _disposed;

    public string DebugSensorDump { get; private set; } = string.Empty;

    public GpuMonitor()
    {
        _dgpuName = ReadGpuNameFromWmi();
        _dgpuLuid = FindDgpuLuid();
        InitCounters();
    }

    // ── GPU name from WMI (name only — AdapterRAM is useless for >4GB) ────────
    private static string ReadGpuNameFromWmi()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT Name FROM Win32_VideoController WHERE PNPDeviceID LIKE 'PCI%'");
            string fallback = string.Empty;
            foreach (ManagementObject obj in searcher.Get())
            {
                var name = obj["Name"]?.ToString()?.Trim() ?? string.Empty;
                if (string.IsNullOrEmpty(name)) continue;
                if (name.Contains("Microsoft Basic", StringComparison.OrdinalIgnoreCase)) continue;
                if (name.Contains("Virtual",         StringComparison.OrdinalIgnoreCase)) continue;
                // Prefer the specific model name over the generic iGPU label
                if (!name.Contains("Radeon(TM) Graphics", StringComparison.OrdinalIgnoreCase))
                    return name;
                fallback = name;
            }
            if (!string.IsNullOrEmpty(fallback)) return fallback;
        }
        catch { }
        return "GPU";
    }

    // ── Find the dGPU LUID from GPU Engine perf counter instances ─────────────
    private string FindDgpuLuid()
    {
        try
        {
            var category  = new PerformanceCounterCategory("GPU Engine");
            var instances = category.GetInstanceNames();

            var luidCounts     = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var luidHasCompute = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var inst in instances)
            {
                var luid = ExtractLuid(inst);
                if (luid is null) continue;
                if (inst.Contains("engtype_3D",      StringComparison.OrdinalIgnoreCase))
                    luidCounts[luid] = luidCounts.GetValueOrDefault(luid) + 1;
                if (inst.Contains("engtype_Compute", StringComparison.OrdinalIgnoreCase))
                    luidHasCompute.Add(luid);
            }

            DebugSensorDump = "LUIDs found:\n" + string.Join("\n", luidCounts.Select(kv =>
                $"  {kv.Key}  3D-instances={kv.Value}  hasCompute={luidHasCompute.Contains(kv.Key)}"));

            if (luidCounts.Count > 0)
            {
                // Prefer: has compute AND more than 1 instance (filters virtual adapters)
                // Among those, fewest 3D instances = least desktop composition = dGPU
                var candidates = luidCounts
                    .Where(kv => kv.Value > 1 && luidHasCompute.Contains(kv.Key))
                    .OrderBy(kv => kv.Value)
                    .ToList();

                var chosen = candidates.Count > 0
                    ? candidates.First().Key
                    : luidCounts.OrderBy(kv => kv.Value).First().Key;

                DebugSensorDump += $"\nChosen LUID: {chosen}";
                return chosen;
            }
        }
        catch (Exception ex)
        {
            DebugSensorDump = "LUID scan failed: " + ex.Message;
        }
        return string.Empty;
    }

    // ── Init perf counters for the chosen dGPU LUID ───────────────────────────
    private void InitCounters()
    {
        if (string.IsNullOrEmpty(_dgpuLuid)) return;
        try
        {
            // GPU Engine → load
            var engCat = new PerformanceCounterCategory("GPU Engine");
            foreach (var inst in engCat.GetInstanceNames())
            {
                if (!inst.Contains(_dgpuLuid, StringComparison.OrdinalIgnoreCase)) continue;
                try
                {
                    var c = new PerformanceCounter("GPU Engine", "Utilization Percentage", inst);
                    _ = c.NextValue();
                    if      (inst.Contains("engtype_3D",      StringComparison.OrdinalIgnoreCase)) _3dCounters.Add(c);
                    else if (inst.Contains("engtype_Compute", StringComparison.OrdinalIgnoreCase)) _computeCounters.Add(c);
                    else if (inst.Contains("engtype_Video",   StringComparison.OrdinalIgnoreCase)) _videoCounters.Add(c);
                }
                catch { }
            }

            // GPU Process Memory → VRAM used
            try
            {
                var memCat = new PerformanceCounterCategory("GPU Process Memory");
                foreach (var inst in memCat.GetInstanceNames())
                {
                    if (!inst.Contains(_dgpuLuid, StringComparison.OrdinalIgnoreCase)) continue;
                    try
                    {
                        var c = new PerformanceCounter("GPU Process Memory", "Dedicated Usage", inst);
                        _ = c.NextValue();
                        _vramCounters.Add(c);
                    }
                    catch { }
                }
            }
            catch { }

            // GPU Adapter Memory → true VRAM total (not capped at 4GB like WMI)
            try
            {
                var adpCat = new PerformanceCounterCategory("GPU Adapter Memory");
                foreach (var inst in adpCat.GetInstanceNames())
                {
                    if (!inst.Contains(_dgpuLuid, StringComparison.OrdinalIgnoreCase)) continue;
                    try
                    {
                        using var c = new PerformanceCounter("GPU Adapter Memory", "Dedicated Memory Budget", inst);
                        var budget = c.NextValue();
                        if (budget > 0) { _dgpuVramMb = budget / 1024f / 1024f; break; }
                    }
                    catch { }
                }
            }
            catch { }

            DebugSensorDump += $"\n3D={_3dCounters.Count} Compute={_computeCounters.Count} " +
                               $"Video={_videoCounters.Count} VRAM={_vramCounters.Count} " +
                               $"VRAMTotal={_dgpuVramMb:0}MB";
        }
        catch (Exception ex)
        {
            DebugSensorDump += "\nCounter init error: " + ex.Message;
        }
    }

    // ── Read ──────────────────────────────────────────────────────────────────
    public List<GpuInfo> Read()
    {
        var info = new GpuInfo
        {
            Name        = _dgpuName,
            VramTotalMb = _dgpuVramMb,
        };

        // GPU load — sum all 3D engine utilization, clamp 0–100
        float load = 0f;
        foreach (var c in _3dCounters) try { load += c.NextValue(); } catch { }
        info.UsagePercent = Math.Min(100f, load);

        // Compute engine max
        float compute = 0f;
        foreach (var c in _computeCounters) try { compute = Math.Max(compute, c.NextValue()); } catch { }
        info.ComputeUsagePercent = compute;

        // VRAM used — sum dedicated usage across all processes (bytes → MB)
        float vramBytes = 0f;
        foreach (var c in _vramCounters) try { vramBytes += c.NextValue(); } catch { }
        info.VramUsedMb = vramBytes / 1024f / 1024f;

        if (info.VramTotalMb > 0)
            info.VramUsagePercent = Math.Min(100f, info.VramUsedMb / info.VramTotalMb * 100f);

        return [info];
    }

    private static string? ExtractLuid(string instanceName)
    {
        var parts = instanceName.Split('_');
        for (int i = 0; i < parts.Length - 3; i++)
            if (parts[i].Equals("luid", StringComparison.OrdinalIgnoreCase))
                return parts[i + 1] + "_" + parts[i + 2];
        return null;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        foreach (var c in _3dCounters.Concat(_computeCounters).Concat(_videoCounters).Concat(_vramCounters))
            c.Dispose();
    }
}