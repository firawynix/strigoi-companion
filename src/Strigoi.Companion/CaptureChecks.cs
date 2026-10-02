using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace Strigoi.Companion;

internal static class CaptureChecks
{
    // Real WGC, but only an application-owned synthetic window. Never a player's game.
    internal static async Task Run(PetWindow pet, string directory)
    {
        Directory.CreateDirectory(directory);
        var checks = new Dictionary<string, bool>();
        var fixture = new Window { Title = "Strigoi · fixture de captura", Width = 420, Height = 300, Background = Brushes.Red, ShowActivated = false };
        var capture = new WindowCapture();
        CaptureWindow? panel = null;
        byte[]? latest = null; int w = 0, h = 0;
        capture.Frame += (pixels, width, height, _, _) => { latest = pixels; w = width; h = height; };
        async Task Until(Func<bool> condition)
        {
            for (int i = 0; i < 60 && !condition(); i++) await Task.Delay(100);
            if (!condition()) throw new Exception("Capture test timeout: " + capture.Status);
        }
        bool Color(bool red)
        {
            if (latest is null) return false;
            int i = ((h / 2) * w + w / 2) * 4;
            return red ? latest[i + 2] > 220 && latest[i + 1] < 30 : latest[i + 1] > 220 && latest[i + 2] < 30;
        }
        try
        {
            fixture.Show();
            nint hwnd = new WindowInteropHelper(fixture).Handle;
            var target = new CaptureTarget(hwnd, (uint)Environment.ProcessId, fixture.Title);
            capture.Start(target); await Until(() => Color(true));
            checks["real-wgc-selected-window-red"] = true;
            var firstSession = capture.SessionId;
            fixture.Background = Brushes.Lime; await Until(() => Color(false));
            checks["visual-change-detected"] = capture.History.Events > 0;
            fixture.Width = 580; fixture.Height = 340;
            await Until(() => w > 420);
            checks["resize-recovers"] = w > 420 && Color(false);
            checks["bounded-reduced-history"] = capture.History.Bytes <= Core.VisualChanges.MaximumBytes && capture.History.Count <= 8;
            await capture.Stop(); int stoppedFrames = capture.Frames;
            await Task.Delay(1100);
            checks["stop-clears-and-no-late-frames"] = !capture.Running && capture.History.Count == 0 && capture.Frames == stoppedFrames;
            latest = null; capture.Start(target); await Until(() => latest is not null);
            checks["restart-new-session"] = capture.SessionId != firstSession;
            fixture.WindowState = WindowState.Minimized; await Until(() => !capture.Running);
            checks["minimize-stops-and-clears"] = capture.History.Count == 0;
            fixture.Close();
            fixture = new Window { Title = "Strigoi · segunda fixture", Width = 420, Height = 300, Background = Brushes.Lime, ShowActivated = false };
            fixture.Show();
            target = new CaptureTarget(new WindowInteropHelper(fixture).Handle, (uint)Environment.ProcessId, fixture.Title);
            capture.Start(target); await Until(() => capture.Frames > 0);
            fixture.Width = 1920; fixture.Height = 1080;
            using (var probe = new RuntimeProbe(pet))
            {
                probe.Start();
                for (int i = 0; i < 30; i++) { fixture.Background = i % 2 == 0 ? Brushes.Lime : Brushes.Red; await Task.Delay(500); }
                probe.Stop(); probe.Save(Path.Combine(directory, "capture-cost.json"), "Owned synthetic window, requested 1920x1080; 15-second capture microbenchmark", "Desktop", new());
            }
            fixture.Close(); await Until(() => !capture.Running);
            checks["target-close-stops-and-clears"] = capture.History.Count == 0;
            fixture = new Window { Title = "Strigoi · teste dos controles", Width = 420, Height = 300, Background = Brushes.Red, ShowActivated = false };
            fixture.Show();
            panel = new CaptureWindow(directory) { ShowActivated = false }; panel.Show();
            panel.SelectForTest(new(new WindowInteropHelper(fixture).Handle, (uint)Environment.ProcessId, fixture.Title));
            panel.StartForTest(); await Until(() => panel.SessionForTest.Frames > 0);
            fixture.Background = Brushes.Lime;
            await Until(() => panel.SessionForTest.History.Count >= 2);
            panel.ReplayForTest();
            checks["replay-opens-with-capture-running"] = panel.ReviewingForTest && panel.SessionForTest.Running;
            panel.NextForTest();
            checks["replay-navigates"] = panel.ReviewIndexForTest == 1;
            int replayFrames = panel.SessionForTest.Frames;
            fixture.Background = Brushes.Blue; await Until(() => panel.SessionForTest.Frames > replayFrames);
            checks["replay-stays-selected-while-capture-advances"] = panel.ReviewingForTest && panel.ReviewIndexForTest == 1;
            panel.LiveForTest();
            checks["replay-returns-live"] = !panel.ReviewingForTest && panel.SessionForTest.Running;
            panel.ReplayForTest();
            panel.RenderPreview(Path.Combine(directory, "capture-panel.png"));
            checks["capture-ui-starts-selected-window"] = panel.SessionForTest.Running;
            panel.PauseForTest(); await Until(() => !panel.SessionForTest.Running);
            checks["capture-ui-pause-clears"] = panel.SessionForTest.History.Count == 0 && !panel.ReviewingForTest;
            panel.SaveReportForTest();
            var saved = Directory.GetFiles(directory, $"capture-*-{panel.SessionForTest.SessionId:N}.json").SingleOrDefault();
            var savedText = saved is null ? "" : File.ReadAllText(saved);
            checks["session-report-has-no-images"] = saved is not null && !savedText.Contains("Pixels", StringComparison.Ordinal) && savedText.Contains("replayOpened", StringComparison.Ordinal) && savedText.Contains("pauseUsed", StringComparison.Ordinal);
            panel.Close(); await Until(() => !panel.IsVisible);
            checks["close-paused-panel"] = !panel.SessionForTest.Running;
            panel = new CaptureWindow(directory) { ShowActivated = false }; panel.Show();
            panel.Close(); await Until(() => !panel.IsVisible);
            checks["close-never-started-panel"] = true;
            panel = new CaptureWindow(directory) { ShowActivated = false }; panel.Show();
            panel.Close(); await panel.Shutdown();
            checks["close-and-shutdown-share-completion"] = !panel.IsVisible;
            panel = new CaptureWindow(directory) { ShowActivated = false }; panel.Show();
            panel.SelectForTest(new(new WindowInteropHelper(fixture).Handle, (uint)Environment.ProcessId, fixture.Title));
            panel.StartForTest(); await Until(() => panel.SessionForTest.Frames > 0);
            panel.PauseForTest(); await Until(() => !panel.SessionForTest.Running);
            panel.PauseForTest(); await Until(() => panel.SessionForTest.Running && panel.SessionForTest.Frames > 0);
            panel.Close(); await Until(() => !panel.SessionForTest.Running && !panel.IsVisible);
            checks["capture-ui-resume-and-close"] = panel.SessionForTest.History.Count == 0 && !panel.ReviewingForTest;
        }
        finally
        {
            if (panel is not null) await panel.Shutdown();
            await capture.Stop(); fixture.Close();
            File.WriteAllText(Path.Combine(directory, "capture-result.json"), JsonSerializer.Serialize(new { checks, passed = checks.Count == 19 && !checks.ContainsValue(false), scope = "Actual Windows.Graphics.Capture of an owned synthetic window only; no game compatibility claim. Minimized fixture was not programmatically restored; a new selected fixture covers target closure." }, new JsonSerializerOptions { WriteIndented = true }));
        }
        if (checks.Count != 19 || checks.ContainsValue(false)) throw new Exception("Capture checks failed.");
    }
}
