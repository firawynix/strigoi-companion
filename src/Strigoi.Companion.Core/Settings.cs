using System;
using System.Collections.Generic;
using System.Text.Json;

namespace Strigoi.Companion.Core;

public enum InteractionMode { Locked, Interactive, Reposition }
public enum ModelStartupMode { AutomaticOnGameDetection, OnFirstInteraction, Manual }

public sealed record CompanionSettings
{
    public int Version { get; init; } = 2;
    public double Scale { get; init; } = 1;
    public string Monitor { get; init; } = "";
    public double X { get; init; } = .85;
    public double Y { get; init; } = .8;
    public InteractionMode Mode { get; init; } = InteractionMode.Interactive;
    public bool ReducedMotion { get; init; }
    public int Hotkey { get; init; } = 121; // F10 with Ctrl+Alt
    public bool Onboarded { get; init; }
    public string LocalVlmEndpoint { get; init; } = VisionRuntime.DefaultEndpoint;
    public string LocalVlmModel { get; init; } = VisionRuntime.DefaultModel;
    public bool WebResearchEnabled { get; init; }
    public SpoilerPolicy SpoilerPolicy { get; init; } = SpoilerPolicy.hint;
    public bool FollowGameEnabled { get; init; } = true;
    public bool WatchEnabled { get; init; }
    public bool AutoDetectGame { get; init; } = true;
    public bool AutoStartChronicle { get; init; } = true;
    public ModelStartupMode ModelStartupMode { get; init; } = ModelStartupMode.AutomaticOnGameDetection;
    public Dictionary<string, string> LastRunByGame { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    public CompanionSettings Normalize()
    {
        // v1's locked default made the Familiar itself impossible to click.
        // Preserve an explicit v2 choice, but migrate the old default to Quick Talk.
        var migratedMode = Version == 1 && Mode == InteractionMode.Locked ? InteractionMode.Interactive : Mode;
        return this with
        {
        Scale = double.IsFinite(Scale) ? Math.Clamp(Scale, .5, 2) : 1,
        X = double.IsFinite(X) ? Math.Clamp(X, 0, 1) : .85,
        Y = double.IsFinite(Y) ? Math.Clamp(Y, 0, 1) : .8,
        Version = 2,
        Mode = Enum.IsDefined(migratedMode) ? migratedMode : InteractionMode.Interactive,
        Hotkey = Hotkey is >= 119 and <= 122 ? Hotkey : 121,
        Monitor = Monitor ?? "",
        LocalVlmEndpoint = string.IsNullOrWhiteSpace(LocalVlmEndpoint) ? VisionRuntime.DefaultEndpoint : LocalVlmEndpoint.Trim(),
        LocalVlmModel = string.IsNullOrWhiteSpace(LocalVlmModel) ? VisionRuntime.DefaultModel : LocalVlmModel.Trim(),
        SpoilerPolicy = Enum.IsDefined(SpoilerPolicy) ? SpoilerPolicy : SpoilerPolicy.hint,
        ModelStartupMode = Enum.IsDefined(ModelStartupMode) ? ModelStartupMode : ModelStartupMode.AutomaticOnGameDetection,
        LastRunByGame = LastRunByGame ?? new(StringComparer.OrdinalIgnoreCase)
        };
    }
}

public sealed class SettingsStore(string directory)
{
    public string DirectoryPath => directory;
    public string? Warning { get; private set; }
    public CompanionSettings Load()
    {
        var file = Path.Combine(directory, "settings.json");
        if (!File.Exists(file)) return new();
        try
        {
            var settings = JsonSerializer.Deserialize<CompanionSettings>(File.ReadAllText(file));
            if (settings is null || settings.Version is < 1 or > 2) throw new InvalidDataException("Versão desconhecida.");
            return settings.Normalize();
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or JsonException or UnauthorizedAccessException)
        {
            Warning = "As preferências não puderam ser lidas. Usando valores padrão.";
            try
            {
                File.Copy(file, Path.Combine(directory, $"settings-recovery-{Guid.NewGuid():N}.json"));
                Warning += " Uma cópia do arquivo anterior foi preservada.";
            }
            catch (Exception copyError) when (copyError is IOException or UnauthorizedAccessException) { }
            return new();
        }
    }

    public void Save(CompanionSettings settings)
    {
        Directory.CreateDirectory(directory);
        var file = Path.Combine(directory, "settings.json");
        var temporary = file + ".tmp";
        // One UI writer; replacement preserves the previous file until the write finishes.
        File.WriteAllText(temporary, JsonSerializer.Serialize(settings.Normalize(), new JsonSerializerOptions { WriteIndented = true }));
        if (File.Exists(file)) File.Replace(temporary, file, file + ".bak", true);
        else File.Move(temporary, file);
    }
}

public readonly record struct WorkArea(int Left, int Top, int Width, int Height);
public static class Placement
{
    public static (int X, int Y) Restore(WorkArea area, int width, int height, double x, double y) =>
        (area.Left + (int)Math.Round(Math.Max(0, area.Width - width) * Math.Clamp(x, 0, 1)),
         area.Top + (int)Math.Round(Math.Max(0, area.Height - height) * Math.Clamp(y, 0, 1)));

    public static (double X, double Y) Save(WorkArea area, int width, int height, int x, int y) =>
        (Math.Clamp((double)(x - area.Left) / Math.Max(1, area.Width - width), 0, 1),
         Math.Clamp((double)(y - area.Top) / Math.Max(1, area.Height - height), 0, 1));
}
