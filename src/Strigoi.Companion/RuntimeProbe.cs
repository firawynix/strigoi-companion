using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows.Threading;
using Strigoi.Companion.Core;

namespace Strigoi.Companion;

internal sealed class RuntimeProbe : IDisposable
{
    [StructLayout(LayoutKind.Sequential)]
    private struct MemoryCounters
    {
        public uint Size, PageFaultCount;
        public nuint PeakWorkingSet, WorkingSet, QuotaPeakPagedPool, QuotaPagedPool,
            QuotaPeakNonPagedPool, QuotaNonPagedPool, PagefileUsage, PeakPagefileUsage,
            PrivateUsage, PrivateWorkingSet;
        public ulong SharedCommitUsage;
    }
    [DllImport("psapi.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetProcessMemoryInfo(IntPtr process, ref MemoryCounters counters, uint size);

    private readonly Process process = Process.GetCurrentProcess();
    private readonly Stopwatch clock = new();
    private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromSeconds(5) };
    private readonly PetWindow pet;
    private readonly List<RuntimeSample> samples = new();
    private TimeSpan previousCpu;
    private double previousSeconds;
    private bool disposed;
    public bool Running => timer.IsEnabled;
    public double Seconds => clock.Elapsed.TotalSeconds;
    public IReadOnlyList<RuntimeSample> Samples => samples;
    public event Action? Updated;

    public RuntimeProbe(PetWindow pet)
    {
        this.pet = pet;
        timer.Tick += OnTick;
    }
    public void Start()
    {
        if (Running) return;
        samples.Clear(); process.Refresh(); previousCpu = process.TotalProcessorTime;
        previousSeconds = 0; clock.Restart(); Capture(); timer.Start();
    }
    public void Stop()
    {
        if (!Running) return;
        timer.Stop(); Capture(); clock.Stop(); Updated?.Invoke();
    }
    private void OnTick(object? sender, EventArgs e)
    {
        Capture();
        if (Seconds >= 1800) { timer.Stop(); clock.Stop(); }
        Updated?.Invoke();
    }
    private void Capture()
    {
        process.Refresh();
        double seconds = clock.Elapsed.TotalSeconds;
        var cpu = process.TotalProcessorTime;
        double interval = seconds - previousSeconds;
        var memory = new MemoryCounters { Size = (uint)Marshal.SizeOf<MemoryCounters>() };
        long? privateWorkingSet = GetProcessMemoryInfo(process.Handle, ref memory, memory.Size)
            ? checked((long)memory.PrivateWorkingSet) : null;
        samples.Add(new(seconds, samples.Count == 0 ? 0 : (cpu - previousCpu).TotalSeconds / Math.Max(.001, interval) / Environment.ProcessorCount * 100,
            process.WorkingSet64, process.PrivateMemorySize64, privateWorkingSet, process.HandleCount,
            pet.ObservedActivations, pet.RenderedFrames));
        previousSeconds = seconds; previousCpu = cpu;
    }
    public void Save(string file, string game, string displayMode, Dictionary<string, string> observations)
    {
        var report = new
        {
            schemaVersion = 2, createdAt = DateTimeOffset.UtcNow,
            applicationVersion = typeof(RuntimeProbe).Assembly.GetName().Version?.ToString(),
            summary = Stability.Summarize(samples),
            game = new { name = game, displayMode, source = "player-reported; not detected" },
            observations, samples,
            scope = "Own-process CPU and memory only. This report does not measure game FPS; optional capture may contribute to process usage. Player observations are not automatically verified.",
            gameplayGate = observations.Count == 0 || observations.ContainsValue("not-tested") ? "pending" : observations.ContainsValue("failed") ? "needs-review" : "player-reported-pass; benchmark and hardware matrix still required"
        };
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(file))!);
        string temp = file + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(temp, file, true);
    }
    public void Dispose()
    {
        if (disposed) return;
        Stop(); timer.Tick -= OnTick; process.Dispose(); disposed = true;
    }
}
