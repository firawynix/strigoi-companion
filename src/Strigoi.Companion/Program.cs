using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Strigoi.Companion.Core;
using Forms = System.Windows.Forms;

namespace Strigoi.Companion;

internal static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        bool smoke = args.Length == 2 && args[0] == "--smoke-test";
        bool stability = args.Length == 3 && args[0] == "--stability-test" && int.TryParse(args[1], out var requestedSeconds) && requestedSeconds is >= 30 and <= 1800;
        bool captureTest = args.Length == 2 && args[0] == "--capture-test";
        bool storeScreenshot = args.Length == 2 && args[0] == "--store-screenshot";
#if GAME_FUSION
        bool fusionSmoke = args.Length == 2 && args[0] == "--fusion-smoke-test";
#else
        bool fusionSmoke = false;
#endif
        bool diagnostic = smoke || stability || captureTest || storeScreenshot || fusionSmoke;
        if (args.Length > 0 && !diagnostic) return 2;
        string reportDirectory = diagnostic ? Path.GetFullPath(args[^1]) : "";
        using var mutex = new Mutex(true, diagnostic ? "Local\\Strigoi.Companion.Diagnostics" :
#if GAME_FUSION
            "Local\\Strigoi.Companion.Assistant"
#else
            "Local\\Strigoi.Companion"
#endif
            , out bool first);
        if (!first) { MessageBox.Show("O Familiar já está aberto. Use o ícone da bandeja ou seu atalho (padrão: Ctrl + Alt + F10).", "Strigoi Companion"); return 0; }
        var directory = diagnostic ? Path.Combine(reportDirectory, "profile") : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
#if GAME_FUSION
            "StrigoiCompanionAssistant"
#else
            "StrigoiCompanion"
#endif
        );
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        PetWindow? pet = null;
        ControlWindow? panel = null;
        ValidationWindow? validation = null;
        ManualGameWindow? manualCapture = null;
        FamiliarCoordinator? familiar = null;
        FamiliarHudWindow? quickTalk = null, quickControl = null;
        Forms.NotifyIcon? tray = null;
        var store = new SettingsStore(directory);
        bool quitting = false;
        void Log(string message)
        {
            try
            {
                Directory.CreateDirectory(directory);
                string path = Path.Combine(directory, "diagnostics.log");
                if (File.Exists(path) && new FileInfo(path).Length > 1_000_000) File.Move(path, path + ".previous", true);
                File.AppendAllText(path, $"{DateTimeOffset.Now:O} {message}{Environment.NewLine}");
            }
            catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
        void Save()
        {
            if (pet is null) return;
            try { store.Save(pet.Settings); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Log("settings-save-failed: " + ex.GetType().Name);
                pet?.Say("Não consegui salvar as preferências.");
            }
        }
        void Open()
        {
            if (pet is null) return;
            if (!pet.IsVisible) pet.Show();
            if (panel is null)
            {
                panel = new ControlWindow(pet, Save, store.Warning, Validate, Capture);
                panel.Closed += (_, _) => panel = null;
                panel.Show();
                pet.Settings = pet.Settings with { Onboarded = true }; Save();
            }
            else { if (panel.WindowState == WindowState.Minimized) panel.WindowState = WindowState.Normal; panel.Activate(); }
        }
        void OpenTalk()
        {
            if (pet is null || familiar is null) return;
            quickTalk?.Close(); quickTalk = FamiliarHudWindow.Talk(pet, familiar); quickTalk.Closed += (_, _) => quickTalk = null; quickTalk.ShowNearFamiliar();
        }
        void OpenQuickControl()
        {
            if (pet is null || familiar is null) return;
            quickControl?.Close(); quickControl = FamiliarHudWindow.Control(pet, familiar, Open); quickControl.Closed += (_, _) => quickControl = null; quickControl.ShowNearFamiliar();
        }
        void Validate()
        {
            if (pet is null) return;
            if (validation is null)
            {
                validation = new ValidationWindow(pet, Path.Combine(directory, "reports"));
                validation.Closed += (_, _) => validation = null;
                validation.Show();
            }
            else { validation.WindowState = WindowState.Normal; validation.Activate(); }
        }
        void Capture()
        {
            if (familiar is null) return;
            if (manualCapture is null) { manualCapture = new ManualGameWindow(familiar); manualCapture.Closed += (_, _) => manualCapture = null; manualCapture.Show(); }
            else { manualCapture.Activate(); }
        }
        async void Quit()
        {
            if (quitting) return;
            quitting = true;
#if GAME_FUSION
            Firaw.WorkAssistant.CompanionModuleHost.CloseGamePlanner();
#endif
            familiar?.Dispose(); quickTalk?.Close(); quickControl?.Close(); pet?.SavePosition(); Save(); validation?.Close(); panel?.Close(); tray?.Dispose(); pet?.Close();
            Log("shutdown"); app.Shutdown();
        }
        app.DispatcherUnhandledException += (_, e) =>
        {
            Log(e.Exception.ToString()); e.Handled = true;
            Environment.ExitCode = 1;
            if (!diagnostic) MessageBox.Show("O Companion encontrou um erro e será encerrado. Detalhes em " + directory, "Strigoi Companion");
            Quit();
        };
        try
        {
            var settings = diagnostic ? new CompanionSettings() : store.Load();
            pet = new PetWindow(settings, OpenTalk, OpenQuickControl, () => familiar?.SetFollowing(!(familiar?.Following ?? true)), Save, () => { Log("display-topology-change"); }, !diagnostic);
            familiar = new FamiliarCoordinator(directory, settings, next => { if (pet is not null) { pet.Settings = next; Save(); } });
            familiar.Balloon += message => pet?.Say(message);
            familiar.Presentation += presentation => pet.React(presentation switch
            {
                FamiliarPresentation.Watching => "happy", FamiliarPresentation.Thinking => "thinking", FamiliarPresentation.Researching => "thinking",
                FamiliarPresentation.Warning => "warning", FamiliarPresentation.Passive => "sleep", FamiliarPresentation.CanHelp => "excited", _ => "idle"
            });
            if (!diagnostic)
            {
                tray = new Forms.NotifyIcon { Icon = System.Drawing.Icon.ExtractAssociatedIcon(Environment.ProcessPath!) ?? System.Drawing.SystemIcons.Application, Text = WorkAssistantBridge.IsAvailable ? ProductBrand.IntegratedName : "Strigoi Companion", Visible = true };
                var menu = new Forms.ContextMenuStrip();
                void Item(string label, Action action) => menu.Items.Add(label, null, (_, _) => app.Dispatcher.Invoke(action));
                Item("Abrir controles", Open);
                Item("Testar com meu jogo", Validate);
                Item("Captura local · escolher janela", Capture);
                if (WorkAssistantBridge.IsAvailable) Item("Assistente de trabalho · tarefas e notas", WorkAssistantBridge.Open);
                Item("Bloquear · deixar cliques passar", () => pet.SetMode(InteractionMode.Locked));
                Item("Reposicionar", () => { pet.Show(); pet.SetMode(InteractionMode.Reposition); });
                Item("Trazer pet de volta", pet.ResetPosition);
                bool paused = false;
                Item("Pausar / retomar animação", () => { paused = !paused; pet.SetPaused(paused); });
                Item("Mostrar / ocultar", () => { if (pet.IsVisible) pet.Hide(); else pet.Show(); });
                menu.Items.Add(new Forms.ToolStripSeparator()); Item("Sair", Quit);
                tray.ContextMenuStrip = menu;
                tray.DoubleClick += (_, _) => app.Dispatcher.Invoke(Open);
            }
            app.SessionEnding += (_, _) => Quit();
            app.Startup += (_, _) =>
            {
                pet.Show(); familiar.Start(diagnostic ? settings with { FollowGameEnabled = false, AutoDetectGame = false } : settings); if (diagnostic) pet.React("idle", 0); Log("startup familiar-first; auto detection uses foreground polling");
                if (diagnostic) app.Dispatcher.BeginInvoke(new Action(async () =>
                {
                    try
                    {
                        if (fusionSmoke)
                        {
#if GAME_FUSION
                            Firaw.WorkAssistant.CompanionModuleHost.OpenGamePlanner(Path.Combine(reportDirectory, "planner"));
                            await Task.Delay(750);
                            if (!Firaw.WorkAssistant.CompanionModuleHost.IsOpen) throw new InvalidOperationException("O diário de jogo não abriu.");
                            Directory.CreateDirectory(reportDirectory);
                            Firaw.WorkAssistant.CompanionModuleHost.SavePreview(Path.Combine(reportDirectory, "strigoi-missions.png"));
                            File.WriteAllText(Path.Combine(reportDirectory, "fusion-smoke.txt"), "Diário de jogo aberto no processo Strigoi.\n");
#endif
                        }
                        else if (storeScreenshot)
                        {
                            Open();
                            await Task.Delay(500);
                            if (panel is null) throw new InvalidOperationException("Control Center não abriu.");
                            SaveStoreScreenshot(panel, pet, reportDirectory);
                        }
                        else if (captureTest) await CaptureChecks.Run(pet, reportDirectory);
                        else if (smoke) await Smoke.Run(pet, reportDirectory, store);
                        else
                        {
                            using var probe = new RuntimeProbe(pet);
                            probe.Updated += () => probe.Save(Path.Combine(reportDirectory, "stability-progress.json"), "", "Desktop", new());
                            probe.Start(); await Task.Delay(TimeSpan.FromSeconds(int.Parse(args[1]))); probe.Stop();
                            probe.Save(Path.Combine(reportDirectory, "stability-result.json"), "", "Desktop", new());
                        }
                        Quit();
                    }
                    catch (Exception ex) { Log(ex.ToString()); Environment.ExitCode = 1; Quit(); }
                }), DispatcherPriority.ApplicationIdle);
                else if (!settings.Onboarded || !pet.HotkeyRegistered) Open();
            };
            app.Run();
            return Environment.ExitCode;
        }
        catch (Exception ex)
        {
            Log(ex.ToString()); tray?.Dispose();
            if (!diagnostic) MessageBox.Show("Não foi possível iniciar o Familiar. " + ex.Message, "Strigoi Companion");
            return 1;
        }
    }

    private static void SaveStoreScreenshot(Window panel, PetWindow pet, string directory)
    {
        Directory.CreateDirectory(directory);
        panel.UpdateLayout();
        var width = Math.Max(1, (int)Math.Ceiling(panel.ActualWidth));
        var height = Math.Max(1, (int)Math.Ceiling(panel.ActualHeight));
        var panelImage = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        panelImage.Render(panel);
        pet.UpdateLayout();
        var petWidth = Math.Max(1, (int)Math.Ceiling(pet.ActualWidth));
        var petHeight = Math.Max(1, (int)Math.Ceiling(pet.ActualHeight));
        var petImage = new RenderTargetBitmap(petWidth, petHeight, 96, 96, PixelFormats.Pbgra32);
        petImage.Render(pet);
        var visual = new DrawingVisual();
        using (var context = visual.RenderOpen())
        {
            context.DrawRectangle(new SolidColorBrush(Color.FromRgb(22, 16, 32)), null, new Rect(0, 0, 1366, 768));
            context.DrawImage(petImage, new Rect(155, (768 - petHeight) / 2.0, petWidth, petHeight));
            context.DrawImage(panelImage, new Rect((1366 - width) / 2.0, (768 - height) / 2.0, width, height));
        }
        var screenshot = new RenderTargetBitmap(1366, 768, 96, 96, PixelFormats.Pbgra32);
        screenshot.Render(visual);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(screenshot));
        using var file = File.Create(Path.Combine(directory, "strigoi-control-center.png"));
        encoder.Save(file);
    }
}

internal static class Smoke
{
    internal static async Task Run(PetWindow pet, string directory, SettingsStore store)
    {
        Directory.CreateDirectory(directory);
        var checks = new Dictionary<string, bool>();
        var foreground = Native.GetForegroundWindow();
        await Task.Delay(250);
        pet.SetMode(InteractionMode.Locked);
        await Task.Delay(50);
        long style = Native.GetWindowLongPtr(pet.Handle, Native.ExStyle).ToInt64();
        checks["layered-window"] = (style & 0x80000) != 0;
        checks["locked-passes-input-style"] = (style & Native.Transparent) != 0;
        checks["no-activate-style"] = (style & Native.NoActivate) != 0;
        checks["not-in-taskbar"] = !pet.ShowInTaskbar;
        checks["animation-running"] = pet.AnimationRunning;
        Native.GetWindowRect(pet.Handle, out var inputRect);
        var points = (from y in Enumerable.Range(0, 10) from x in Enumerable.Range(0, 10)
                      select new Native.Point { X = inputRect.Left + (inputRect.Right - inputRect.Left) * (x * 2 + 1) / 20,
                                               Y = inputRect.Top + (inputRect.Bottom - inputRect.Top) * (y * 2 + 1) / 20 }).ToArray();
        checks["locked-hit-testing-skips-pet-at-100-points"] = points.All(p => Native.WindowFromPoint(p) != pet.Handle);
        pet.SetMode(InteractionMode.Interactive);
        await Task.Delay(100);
        checks["interactive-removes-pass-through"] = (Native.GetWindowLongPtr(pet.Handle, Native.ExStyle).ToInt64() & Native.Transparent) == 0;
        checks["interactive-hit-testing-finds-visible-pet"] = points.Any(p => Native.WindowFromPoint(p) == pet.Handle);
        pet.SetMode(InteractionMode.Locked);
        pet.Hide(); checks["hidden-stops-timer"] = !pet.AnimationRunning;
        pet.Show();
        pet.SetReducedMotion(true);
        checks["reduced-motion-stops-idle-timer"] = !pet.AnimationRunning;
        pet.SetReducedMotion(false);
        pet.SetPaused(true); long frameCount = pet.RenderedFrames;
        pet.React("excited"); await Task.Delay(300);
        checks["paused-preview-remains-asleep"] = pet.CurrentState == "sleep" && !pet.AnimationRunning;
        checks["paused-preview-does-not-animate"] = pet.RenderedFrames <= frameCount + 1;
        pet.SetPaused(false);
        foreach (var state in new[] { "idle", "blink", "happy", "excited", "thinking", "warning", "facepalm", "shocked", "sleep" })
        {
            pet.React(state, 0);
            await Task.Delay(80);
            pet.RenderPreview(Path.Combine(directory, state + ".png"));
            checks["renders-" + state] = new FileInfo(Path.Combine(directory, state + ".png")).Length > 100;
        }
        pet.SetScale(1.25); await Task.Delay(250);
        pet.SavePosition(); store.Save(pet.Settings);
        var reloaded = store.Load();
        checks["settings-roundtrip"] = reloaded.Scale == pet.Settings.Scale && reloaded.X == pet.Settings.X && reloaded.Y == pet.Settings.Y && reloaded.Mode == pet.Settings.Mode && reloaded.Version == 2;
        Native.GetWindowRect(pet.Handle, out var rect);
        var area = Forms.Screen.FromHandle(pet.Handle).WorkingArea;
        checks["position-in-current-work-area"] = rect.Left >= area.Left && rect.Top >= area.Top && rect.Right <= area.Right && rect.Bottom <= area.Bottom;
        pet.SetScale(1); await Task.Delay(200);
        pet.React("idle", 0);
        using var process = Process.GetCurrentProcess();
        process.Refresh(); var cpu = process.TotalProcessorTime;
        var time = Stopwatch.StartNew(); await Task.Delay(5000); process.Refresh();
        double cpuPercent = (process.TotalProcessorTime - cpu).TotalMilliseconds / time.Elapsed.TotalMilliseconds / Environment.ProcessorCount * 100;
        checks["no-spontaneous-activation"] = pet.ObservedActivations == 0;
        checks["foreground-unchanged"] = Native.GetForegroundWindow() == foreground || pet.ObservedActivations == 0;
        var panel = new ControlWindow(pet, () => { }, null) { ShowActivated = false };
        panel.Show(); await Task.Delay(200);
        pet.SetMode(InteractionMode.Interactive);
        checks["controls-follow-external-mode-change"] = panel.DisplayedMode == InteractionMode.Interactive;
        pet.SetMode(InteractionMode.Locked);
        panel.RenderPreview(Path.Combine(directory, "controls.png"));
        checks["controls-open-and-render"] = panel.IsLoaded;
        panel.Close();
        var validation = new ValidationWindow(pet, Path.Combine(directory, "reports")) { ShowActivated = false };
        validation.Show(); await Task.Delay(100);
        validation.RenderPreview(Path.Combine(directory, "validation.png"));
        validation.StartForTest(); await Task.Delay(100); validation.StopForTest();
        using (var report = JsonDocument.Parse(File.ReadAllText(validation.ReportPath)))
        {
            checks["validation-keeps-untested-game-pending"] = report.RootElement.GetProperty("gameplayGate").GetString() == "pending";
            checks["validation-short-run-not-passed"] = report.RootElement.GetProperty("summary").GetProperty("Status").GetString() == "short-sample";
        }
        validation.Close();
        pet.SetMode(InteractionMode.Interactive);
        using (var probe = new RuntimeProbe(pet))
        {
            probe.Start(); await Task.Delay(150); probe.Stop();
            probe.Save(Path.Combine(directory, "probe-smoke.json"), "", "Desktop", new());
            checks["private-working-set-measured"] = probe.Samples.All(s => s.PrivateWorkingSetBytes is > 0 && s.PrivateWorkingSetBytes <= s.WorkingSetBytes);
        }
        File.WriteAllText(Path.Combine(directory, "smoke-result.json"), JsonSerializer.Serialize(new
        {
            passed = checks.All(x => x.Value), checks, cpuPercent, workingSetBytes = process.WorkingSet64,
            privateBytes = process.PrivateMemorySize64, hotkeyRegistered = pet.HotkeyRegistered,
            limitation = "Short desktop smoke test. No real click delivery, game FPS, mixed-DPI hardware or 30-minute soak verified."
        }, new JsonSerializerOptions { WriteIndented = true }));
        if (checks.Any(x => !x.Value)) Environment.ExitCode = 1;
    }
}
