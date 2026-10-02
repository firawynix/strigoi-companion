using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Strigoi.Companion.Core;

namespace Strigoi.Companion;

public sealed record WorkWindow(long Handle, string Title, string ProcessName)
{
    public override string ToString() => $"{Title} · {ProcessName}";
}

public sealed record WorkFrame(byte[] Pixels, int Width, int Height, double Seconds);

/// <summary>Work-oriented facade over Strigoi capture, local vision and Chronicle.</summary>
public sealed class WorkCompanionEngine : IDisposable
{
    private readonly FamiliarCoordinator coordinator;
    private readonly SettingsStore settingsStore;

    public WorkCompanionEngine(string profileDirectory)
    {
        settingsStore = new SettingsStore(profileDirectory);
        var saved = settingsStore.Load();
        var settings = saved with
        {
            FollowGameEnabled = true,
            AutoDetectGame = File.Exists(Path.Combine(profileDirectory, "settings.json")) && saved.AutoDetectGame,
            ModelStartupMode = ModelStartupMode.OnFirstInteraction,
            SpoilerPolicy = SpoilerPolicy.full
        };
        coordinator = new FamiliarCoordinator(profileDirectory, settings, settingsStore.Save, workMode: true);
        coordinator.Changed += () => Changed?.Invoke();
        coordinator.Balloon += message => Notice?.Invoke(message);
        coordinator.Start(settings);
    }

    public event Action? Changed;
    public event Action<string>? Notice;
    public string ProjectName => coordinator.GameName;
    public string SessionName => coordinator.RunName;
    public string Status => coordinator.Status;
    public bool Capturing => coordinator.Capturing;
    public bool Watching => coordinator.Watching;
    public bool AutoFollowing => coordinator.AutoDetecting;
    public bool WebResearchEnabled => coordinator.Settings.WebResearchEnabled;
    public IReadOnlyList<WorkWindow> Windows() => CaptureNative.Targets()
        .Select(target => new WorkWindow((long)target.Handle, target.Title, target.ProcessName)).ToArray();

    public async Task StartAsync(WorkWindow window)
    {
        var target = CaptureNative.Targets().FirstOrDefault(item => (long)item.Handle == window.Handle && item.Title == window.Title)
            ?? throw new InvalidOperationException("A janela selecionada não está mais disponível.");
        if (coordinator.Capturing) await coordinator.StopAsync();
        await coordinator.StartForTargetAsync(target, false);
    }

    public Task StopAsync() => coordinator.StopAsync();
    public Task<string> AskAsync(string question, CancellationToken token) => coordinator.AskAsync(question, token);
    public void CancelAsk() => coordinator.CancelAsk();
    public string Recap() => coordinator.Recap();
    public IReadOnlyList<(string Id, string Name)> Sessions() => coordinator.Runs.Select(run => (run.Id, run.DisplayName)).ToArray();
    public void SwitchSession(string id) => coordinator.SwitchRun(id);
    public void CreateSession(string name) => coordinator.CreateRun(name);
    public void RecordNote(string note) => coordinator.RecordNote(note);
    public void SetWatch(bool enabled) => coordinator.SetWatch(enabled);
    public void SetAutoFollow(bool enabled) => coordinator.SetAutoDetect(enabled);
    public void SetWebResearch(bool enabled) => coordinator.SetWebResearch(enabled);
    public string SaveReport() => coordinator.SaveReport();
    public WorkFrame? LatestFrame()
    {
        var image = coordinator.LatestImage();
        return image is null ? null : new WorkFrame(image.Pixels, image.Width, image.Height, 0);
    }
    public IReadOnlyList<WorkFrame> Replay() => coordinator.ReplayFrames()
        .Select(frame => new WorkFrame(frame.Pixels, frame.Width, frame.Height, frame.Time.TotalSeconds)).ToArray();
    public void Dispose() => coordinator.Dispose();
}
