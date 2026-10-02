using System.Text.Json;

namespace Strigoi.Companion.Core;

public sealed record Clip(int Row, int[] Frames, int FrameMs, bool Loop, string? Fallback);
public sealed record FamiliarManifest(int CellWidth, int CellHeight, int Columns, int Rows, Dictionary<string, Clip> Clips)
{
    public static FamiliarManifest Load(string file)
    {
        var result = JsonSerializer.Deserialize<FamiliarManifest>(File.ReadAllText(file), new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidDataException("Manifesto vazio.");
        if (result.CellWidth <= 0 || result.CellHeight <= 0 || result.Columns <= 0 || result.Rows <= 0 || !result.Clips.ContainsKey("idle"))
            throw new InvalidDataException("Grade ou idle inválido.");
        foreach (var clip in result.Clips.Values)
            if (clip.Row < 0 || clip.Row >= result.Rows || clip.Frames.Length == 0 || clip.Frames.Any(f => f < 0 || f >= result.Columns) || clip.FrameMs < 30)
                throw new InvalidDataException("Sequência inválida.");
        return result;
    }
}

public static class AnimationFrame
{
    public static int At(Clip clip, double elapsedMs, bool reducedMotion)
    {
        if (reducedMotion) return clip.Frames[0];
        var index = (int)(Math.Max(0, elapsedMs) / clip.FrameMs);
        return clip.Frames[clip.Loop ? index % clip.Frames.Length : Math.Min(index, clip.Frames.Length - 1)];
    }
}
