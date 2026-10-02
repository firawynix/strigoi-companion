namespace Strigoi.Companion.Core;

// A visual difference is not a semantic game event. Only reduced images live here.
public sealed record CapturedMoment(byte[] Pixels, int Width, int Height, TimeSpan Time);

public sealed class VisualChanges
{
    private readonly Queue<CapturedMoment> frames = new();
    private byte[]? previous;
    private int width, height;
    private TimeSpan lastEvent = TimeSpan.MinValue;
    public int Count => frames.Count;
    public long Bytes { get; private set; }
    public int Events { get; private set; }
    public const int MaximumFrames = 8;
    public const long MaximumBytes = 8 * 640 * 360 * 4;

    public (double Difference, bool Changed) Add(byte[] pixels, int w, int h, TimeSpan time)
    {
        if (w < 1 || h < 1 || w > 640 || h > 360 || pixels.Length != checked(w * h * 4))
            throw new ArgumentException("Invalid reduced frame.");
        double difference = 0;
        if (previous is not null && width == w && height == h)
        {
            long total = 0;
            for (int i = 0; i < pixels.Length; i += 4)
                total += Math.Abs(pixels[i] - previous[i]) + Math.Abs(pixels[i + 1] - previous[i + 1]) + Math.Abs(pixels[i + 2] - previous[i + 2]);
            difference = total / (w * h * 3d * 255);
        }
        bool changed = difference >= .12 && (lastEvent == TimeSpan.MinValue || time - lastEvent >= TimeSpan.FromSeconds(2));
        if (changed) { Events++; lastEvent = time; }
        // Own the memory; the caller cannot mutate history after submitting a frame.
        previous = (byte[])pixels.Clone(); width = w; height = h;
        frames.Enqueue(new(previous, w, h, time)); Bytes += previous.Length;
        while (frames.Count > MaximumFrames || Bytes > MaximumBytes) Bytes -= frames.Dequeue().Pixels.Length;
        return (difference, changed);
    }
    public CapturedMoment[] Snapshot() => frames.Select(f => f with { Pixels = (byte[])f.Pixels.Clone() }).ToArray();
    public void Clear()
    {
        frames.Clear(); previous = null; width = height = Events = 0; Bytes = 0; lastEvent = TimeSpan.MinValue;
    }
}
