using System.Text.Json;

namespace Strigoi.Companion.Core;

public sealed record CaptureSessionSnapshot(
    Guid SessionId,
    DateTimeOffset StartedAt,
    DateTimeOffset? EndedAt,
    string TargetTitle,
    uint TargetProcessId,
    int Frames,
    int VisualChanges,
    string EndStatus,
    bool ReplayOpened,
    bool PauseUsed,
    bool ResumeUsed);

public static class CaptureReport
{
    public static string Save(string directory, CaptureSessionSnapshot snapshot, string applicationVersion)
    {
        Directory.CreateDirectory(directory);
        string file = Path.Combine(directory, $"capture-{snapshot.StartedAt:yyyyMMdd-HHmmss}-{snapshot.SessionId:N}.json");
        var report = new
        {
            schemaVersion = 1,
            createdAt = DateTimeOffset.UtcNow,
            applicationVersion,
            capture = snapshot,
            scope = "Session metadata only. No captured images, game FPS, OCR, inference, or gameplay outcome is stored.",
            playerActions = new
            {
                replayOpened = snapshot.ReplayOpened,
                pauseUsed = snapshot.PauseUsed,
                resumeUsed = snapshot.ResumeUsed,
                note = "Button use is recorded; it does not automatically prove the player found the result correct."
            }
        };
        string temp = file + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(temp, file, true);
        return file;
    }
}
