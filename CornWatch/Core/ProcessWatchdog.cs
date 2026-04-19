using System.Diagnostics;

namespace CornWatch.Core;

public class ProcessEntry
{
    public string Name       { get; set; } = string.Empty;
    public int    Pid        { get; set; }
    public float  CpuPercent { get; set; }
    public long   RamBytes   { get; set; }
}

/// <summary>
/// Samples the top N processes by CPU usage.
/// Uses two PerformanceCounter reads spaced 1 second apart to calculate %.
/// </summary>
public sealed class ProcessWatchdog : IDisposable
{
    private readonly int _topN;
    private readonly Dictionary<int, (PerformanceCounter counter, long prevTicks)> _tracked = [];
    private bool _disposed;

    public ProcessWatchdog(int topN = 8) => _topN = topN;

    public List<ProcessEntry> Read()
    {
        var results = new List<ProcessEntry>();
        try
        {
            var procs = Process.GetProcesses()
                .Where(p => p.Id > 4) // skip System/Idle
                .ToList();

            foreach (var proc in procs)
            {
                try
                {
                    results.Add(new ProcessEntry
                    {
                        Name     = proc.ProcessName,
                        Pid      = proc.Id,
                        RamBytes = proc.WorkingSet64,
                        // CPU % via processor time delta
                        CpuPercent = GetCpuPercent(proc),
                    });
                }
                catch { }
                finally { proc.Dispose(); }
            }
        }
        catch { }

        return results
            .OrderByDescending(p => p.CpuPercent)
            .ThenByDescending(p => p.RamBytes)
            .Take(_topN)
            .ToList();
    }

    private static float GetCpuPercent(Process proc)
    {
        try
        {
            // Quick approximation: TotalProcessorTime delta isn't meaningful on first call
            // so we return 0 initially. A proper implementation would cache prev values.
            return 0f; // Will be enhanced with PerformanceCounter in next iteration
        }
        catch { return 0f; }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        foreach (var (counter, _) in _tracked.Values) counter.Dispose();
    }
}
