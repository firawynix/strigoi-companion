using System;
using System.Linq;

namespace Strigoi.Companion.Core;

public sealed record ForegroundCandidate(uint ProcessId, string ProcessName, string Title, bool Visible = true, bool Minimized = false, bool GameSized = true);

// Conservative policy shared by the Windows adapter and tests. It intentionally
// identifies only candidates worth trying with local capture; it never names a game.
public static class FamiliarAutomation
{
    private static readonly string[] Excluded = ["strigoi", "explorer", "applicationframehost", "searchhost", "shellexperiencehost", "textinputhost", "powershell", "cmd.exe", "windows terminal", "settings", "calculator", "notepad", "chrome", "msedge", "firefox", "opera", "brave", "vivaldi", "code", "codex", "discord", "slack", "teams", "outlook", "steam", "raycast", "snipping", "chatgpt", "launcher"];
    private static readonly string[] GameSignals = ["game", "bg3", "baldur", "star wars", "dawnwalker", "elden", "cyberpunk", "witcher", "dota", "leagueoflegends", "valorant", "fortnite", "minecraft", "grand theft", "gta", "hades", "skyrim", "fallout", "diablo", "warhammer"];
    public static bool IsEligible(ForegroundCandidate candidate, int companionProcessId)
    {
        if (!candidate.Visible || candidate.Minimized || !candidate.GameSized || candidate.ProcessId == companionProcessId || string.IsNullOrWhiteSpace(candidate.Title)) return false;
        var value = (candidate.ProcessName + " " + candidate.Title).ToLowerInvariant();
        return !Excluded.Any(value.Contains) && GameSignals.Any(value.Contains);
    }
}
