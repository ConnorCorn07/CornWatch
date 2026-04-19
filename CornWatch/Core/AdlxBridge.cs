namespace CornWatch.Core;

/// <summary>
/// Stub for AMD ADLX SDK integration.
///
/// ADLX (AMD Device Library eXtra) is the official AMD GPU monitoring API
/// and the only way to get temperature, fan speed, clock, and power on
/// RDNA 4 (RX 9000) cards.
///
/// TO ENABLE:
/// 1. Download AMD ADLX SDK from:
///    https://gpuopen.com/adlx/
/// 2. Copy ADLX headers/libs into Core/ADLX/
/// 3. Add a C++/CLI wrapper project (CornWatch.AdlxWrapper.dll)
///    that exposes the metrics as a simple managed interface
/// 4. Call AdlxBridge.Read() from GpuMonitor and merge into GpuInfo
///
/// WHY C++/CLI WRAPPER:
/// ADLX is a native C++ API. The cleanest integration for a C# app is a
/// thin C++/CLI (.NET-compatible C++) wrapper that calls ADLX and returns
/// plain structs back to C#. This keeps the C# codebase clean and avoids
/// complex P/Invoke marshalling for the ADLX COM-like interfaces.
/// </summary>
public static class AdlxBridge
{
    public static bool IsAvailable => false; // flip to true once wrapper is built

    public static AdlxGpuMetrics? Read()
    {
        if (!IsAvailable) return null;
        // TODO: P/Invoke into CornWatch.AdlxWrapper.dll
        // return AdlxWrapper.GetPrimaryGpuMetrics();
        return null;
    }
}

public class AdlxGpuMetrics
{
    public float TemperatureCelsius { get; set; }
    public float FanSpeedRpm        { get; set; }
    public float CoreClockMhz       { get; set; }
    public float MemoryClockMhz     { get; set; }
    public float PowerWatts         { get; set; }
    public float VoltageMilliVolts  { get; set; }
}
