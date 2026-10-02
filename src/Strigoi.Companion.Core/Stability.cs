namespace Strigoi.Companion.Core;

public sealed record RuntimeSample(double Seconds, double CpuPercent, long WorkingSetBytes,
    long PrivateBytes, long? PrivateWorkingSetBytes, int HandleCount, int Activations, long RenderedFrames);

public sealed record StabilitySummary(string Status, double DurationSeconds, int SampleCount,
    double? MeanCpuPercent, long? PeakPrivateWorkingSetBytes, long? PrivateBytesGrowth,
    double MaxSampleGapSeconds, int ObservedActivations, bool MeetsThirtyMinuteDuration);

public static class Stability
{
    public static StabilitySummary Summarize(IReadOnlyList<RuntimeSample> samples)
    {
        if (samples.Count < 2) return new("insufficient-data", 0, samples.Count, null, null, null, 0, 0, false);
        var first = samples[0]; var last = samples[^1];
        double duration = last.Seconds - first.Seconds;
        double weightedCpu = 0, maxGap = 0;
        for (int i = 1; i < samples.Count; i++)
        {
            double gap = samples[i].Seconds - samples[i - 1].Seconds;
            if (gap <= 0) throw new ArgumentException("Amostras precisam de tempo crescente.");
            weightedCpu += samples[i].CpuPercent * gap;
            maxGap = Math.Max(maxGap, gap);
        }
        var privateWorkingSets = samples.Where(s => s.PrivateWorkingSetBytes.HasValue).Select(s => s.PrivateWorkingSetBytes!.Value).ToArray();
        int activations = last.Activations - first.Activations;
        // Activation may be intentional. Only the player can report focus disruption.
        string status = duration < 1800 ? "short-sample" : maxGap > 15 ? "needs-review" : "duration-complete";
        return new(status, duration, samples.Count, weightedCpu / duration,
            privateWorkingSets.Length == samples.Count ? privateWorkingSets.Max() : null,
            last.PrivateBytes - first.PrivateBytes, maxGap, activations, duration >= 1800);
    }
}
