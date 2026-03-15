using System.Diagnostics;

namespace KustoTerminal.Driver.Rendering;

/// <summary>
/// Lightweight rendering performance tracker.
/// Tracks FPS, frame time, cells updated, ANSI bytes, and render mode
/// using Stopwatch ticks for zero-allocation high-resolution timing.
/// </summary>
internal sealed class RenderStats
{
    private const int WindowSize = 60; // rolling window for averages

    // Per-frame metrics (latest)
    public double LastFrameTimeMs { get; private set; }
    public int LastCellsUpdated { get; private set; }
    public int LastAnsiBytes { get; private set; }
    public string LastRenderMode { get; private set; } = "none";

    // Aggregates
    public long TotalFrames { get; private set; }
    public long TotalSkippedFrames { get; private set; }
    public long TotalAnsiBytes { get; private set; }
    public double Fps { get; private set; }
    public double AvgFrameTimeMs { get; private set; }
    public double MaxFrameTimeMs { get; private set; }
    public double P95FrameTimeMs { get; private set; }

    // Rolling window for averages
    private readonly double[] _frameTimes = new double[WindowSize];
    private int _frameIndex;

    // FPS calculation: 1-second sliding window
    private readonly long[] _frameTimestamps = new long[256];
    private int _tsHead;
    private int _tsCount;

    private static readonly double TicksToMs = 1000.0 / Stopwatch.Frequency;

    public void RecordFrame(long startTicks, long endTicks, int cellsUpdated, int ansiBytes, string renderMode)
    {
        double frameMs = (endTicks - startTicks) * TicksToMs;

        LastFrameTimeMs = frameMs;
        LastCellsUpdated = cellsUpdated;
        LastAnsiBytes = ansiBytes;
        LastRenderMode = renderMode;
        TotalFrames++;
        TotalAnsiBytes += ansiBytes;

        // Rolling average
        _frameTimes[_frameIndex] = frameMs;
        _frameIndex = (_frameIndex + 1) % WindowSize;

        int count = (int)Math.Min(TotalFrames, WindowSize);
        double sum = 0;
        for (int i = 0; i < count; i++) sum += _frameTimes[i];
        AvgFrameTimeMs = sum / count;

        // Max
        if (frameMs > MaxFrameTimeMs) MaxFrameTimeMs = frameMs;

        // P95 (sort the window)
        if (count >= 2)
        {
            Span<double> sorted = stackalloc double[count];
            for (int i = 0; i < count; i++) sorted[i] = _frameTimes[i];
            sorted.Sort();
            P95FrameTimeMs = sorted[(int)(count * 0.95)];
        }

        // FPS: count timestamps within last 1 second
        _frameTimestamps[_tsHead] = endTicks;
        _tsHead = (_tsHead + 1) & 0xFF;
        if (_tsCount < 256) _tsCount++;

        long oneSecAgo = endTicks - Stopwatch.Frequency;
        int fpsCount = 0;
        for (int i = 0; i < _tsCount; i++)
        {
            if (_frameTimestamps[i] >= oneSecAgo) fpsCount++;
        }
        Fps = fpsCount;
    }

    public void RecordSkipped() => TotalSkippedFrames++;

    /// <summary>Format a one-line summary for the overlay HUD.</summary>
    public string FormatOverlay()
    {
        string bytes = LastAnsiBytes < 1024
            ? $"{LastAnsiBytes}B"
            : $"{LastAnsiBytes / 1024.0:F1}KB";
        return $" FPS:{Fps:F0} | {LastFrameTimeMs:F1}ms | {LastRenderMode}:{LastCellsUpdated} | {bytes} ";
    }

    /// <summary>Format a multi-line summary for stderr on exit.</summary>
    public string FormatSummary()
    {
        return $"""
            ── Render Stats ──────────────────────
              Total frames:  {TotalFrames}
              Skipped:       {TotalSkippedFrames}
              Avg FPS:       {Fps:F1}
              Frame time:    avg={AvgFrameTimeMs:F2}ms  p95={P95FrameTimeMs:F2}ms  max={MaxFrameTimeMs:F2}ms
              Total ANSI:    {TotalAnsiBytes / 1024.0:F1} KB
            ──────────────────────────────────────
            """;
    }
}
