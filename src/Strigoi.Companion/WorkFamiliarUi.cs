using System;
using System.Windows;
using System.Windows.Threading;
using Strigoi.Companion.Core;

namespace Strigoi.Companion;

/// <summary>The complete Familiar UI, driven by the same work coordinator as the Firaw capture panel.</summary>
public sealed class WorkFamiliarUi : IDisposable
{
    private readonly WorkCompanionEngine engine;
    private readonly FamiliarCoordinator coordinator;
    private readonly PetWindow pet;
    private ControlWindow? controls;
    private FamiliarHudWindow? talk;
    private FamiliarHudWindow? quickControl;
    private ManualGameWindow? capture;
    private bool disposed;

    public WorkFamiliarUi(WorkCompanionEngine engine, bool enableShortcut = true)
    {
        this.engine = engine;
        coordinator = engine.Coordinator;
        pet = new PetWindow(coordinator.Settings, OpenTalk, OpenQuickControl,
            () => coordinator.SetFollowing(!coordinator.Following), Save, () => { }, enableShortcut: enableShortcut, workMode: true);
        coordinator.Balloon += OnBalloon;
        coordinator.Presentation += OnPresentation;
        coordinator.Changed += OnChanged;
    }

    public bool IsVisible => !disposed && pet.IsVisible;
    public bool ControlsOpen => controls is { IsVisible: true };
    public void Show()
    {
        if (disposed) throw new ObjectDisposedException(nameof(WorkFamiliarUi));
        pet.Show();
    }
    public void Toggle()
    {
        if (IsVisible) pet.Hide();
        else Show();
    }
    public void OpenControls()
    {
        if (disposed) return;
        Show();
        if (controls is null)
        {
            controls = new ControlWindow(pet, Save, null, capture: OpenCapture, workMode: true);
            controls.Closed += (_, _) => controls = null;
            controls.Show();
            pet.Settings = pet.Settings with { Onboarded = true };
            Save();
        }
        else { controls.WindowState = WindowState.Normal; controls.Activate(); }
    }

    private void OpenTalk()
    {
        if (disposed) return;
        talk?.Close();
        talk = FamiliarHudWindow.Talk(pet, coordinator);
        talk.Closed += (_, _) => talk = null;
        talk.ShowNearFamiliar();
    }
    private void OpenQuickControl()
    {
        if (disposed) return;
        quickControl?.Close();
        quickControl = FamiliarHudWindow.Control(pet, coordinator, OpenControls);
        quickControl.Closed += (_, _) => quickControl = null;
        quickControl.ShowNearFamiliar();
    }
    private void OpenCapture()
    {
        if (disposed) return;
        if (capture is null)
        {
            capture = new ManualGameWindow(coordinator);
            capture.Closed += (_, _) => capture = null;
            capture.Show();
        }
        else capture.Activate();
    }
    private void Save() => coordinator.SetPresentationSettings(pet.Settings);
    private void OnChanged() => OnUi(() => pet.Settings = coordinator.Settings);
    private void OnBalloon(string message) => OnUi(() => pet.Say(message));
    private void OnPresentation(FamiliarPresentation state) => OnUi(() => pet.React(state switch
    {
        FamiliarPresentation.Watching => "happy",
        FamiliarPresentation.Thinking or FamiliarPresentation.Researching => "thinking",
        FamiliarPresentation.Warning => "warning",
        FamiliarPresentation.Passive => "sleep",
        FamiliarPresentation.CanHelp => "excited",
        _ => "idle"
    }));
    private void OnUi(Action action)
    {
        if (disposed) return;
        if (pet.Dispatcher.CheckAccess()) action();
        else pet.Dispatcher.BeginInvoke(action, DispatcherPriority.Normal);
    }
    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        coordinator.Balloon -= OnBalloon;
        coordinator.Presentation -= OnPresentation;
        coordinator.Changed -= OnChanged;
        talk?.Close(); quickControl?.Close(); capture?.Close(); controls?.Close();
        pet.SavePosition();
        Save();
        pet.Close();
    }
}
