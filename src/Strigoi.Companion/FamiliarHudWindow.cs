using System;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Strigoi.Companion.Core;
using Forms = System.Windows.Forms;

namespace Strigoi.Companion;

internal sealed class FamiliarHudWindow : Window
{
    private readonly FamiliarCoordinator coordinator;
    private readonly PetWindow pet;
    private readonly StackPanel body = new() { Margin = new Thickness(14) };
    private readonly TextBlock heading = Text("", 13, "#D9B8FF");
    private CancellationTokenSource? asking;
    private bool closing;

    private FamiliarHudWindow(PetWindow pet, FamiliarCoordinator coordinator)
    {
        this.pet = pet; this.coordinator = coordinator;
        Title = "Strigoi Companion"; Width = 370; MinWidth = 310; MaxWidth = 450; SizeToContent = SizeToContent.Height;
        WindowStyle = WindowStyle.None; ResizeMode = ResizeMode.NoResize; ShowInTaskbar = false; Topmost = true;
        Background = Brush("#1B1425"); Foreground = Brush("#F4ECFA");
        BorderThickness = new Thickness(1); BorderBrush = Brush("#76548E");
        Content = new Border { CornerRadius = new CornerRadius(14), Background = Background, BorderBrush = BorderBrush, BorderThickness = BorderThickness, Child = body };
        PreviewKeyDown += (_, e) => { if (e.Key == Key.Escape) Close(); };
        Deactivated += (_, _) => DismissWhenInactive();
        Closed += (_, _) => asking?.Cancel();
    }
    internal static FamiliarHudWindow Talk(PetWindow pet, FamiliarCoordinator coordinator)
    {
        var hud = new FamiliarHudWindow(pet, coordinator); hud.BuildTalk(); return hud;
    }
    internal static FamiliarHudWindow Control(PetWindow pet, FamiliarCoordinator coordinator, Action openControlCenter)
    {
        var hud = new FamiliarHudWindow(pet, coordinator); hud.BuildControl(openControlCenter); return hud;
    }
    internal void ShowNearFamiliar()
    {
        Show(); PositionNearFamiliar();
    }
    private void DismissWhenInactive()
    {
        if (asking is not null || closing || !IsVisible) return;
        closing = true;
        // Deactivation can be raised again during WPF's own Close sequence.
        // Queue exactly one close after that event has returned.
        Dispatcher.BeginInvoke(new Action(Close), System.Windows.Threading.DispatcherPriority.Background);
    }
    private void PositionNearFamiliar()
    {
        UpdateLayout();
        if (!Native.GetWindowRect(pet.Handle, out var anchor) || !Native.GetWindowRect(new System.Windows.Interop.WindowInteropHelper(this).Handle, out var popup)) return;
        var screen = Forms.Screen.FromHandle(pet.Handle).WorkingArea;
        int width = popup.Right - popup.Left, height = popup.Bottom - popup.Top;
        int x = anchor.Left - width - 10;
        if (x < screen.Left) x = anchor.Right + 10;
        x = Math.Clamp(x, screen.Left, Math.Max(screen.Left, screen.Right - width));
        // Align the bottom with the Familiar first. This leaves room for a long
        // answer above a pet placed near the bottom edge of a monitor.
        int y = Math.Clamp(anchor.Bottom - height, screen.Top, Math.Max(screen.Top, screen.Bottom - height));
        Native.SetWindowPos(new System.Windows.Interop.WindowInteropHelper(this).Handle, IntPtr.Zero, x, y, 0, 0, Native.NoSize | Native.NoZOrder | Native.NoActivatePos);
        Activate();
    }
    private void BuildTalk()
    {
        Header("🦇  Pergunte ao Familiar");
        body.Children.Add(Text(coordinator.GameName + "  ·  " + coordinator.RunName, 12, "#C9B8D5"));
        body.Children.Add(Text("Ele responde aqui e fala um resumo acima da própria cabeça.", 12, "#A997B9"));
        var prompt = new TextBox { MinHeight = 42, MaxLength = 2000, Padding = new Thickness(11, 9, 11, 9), Background = Brush("#291E35"), Foreground = Foreground, BorderBrush = Brush("#A67ACC"), ToolTip = "Escreva uma pergunta sobre a cena" };
        body.Children.Add(prompt);
        var result = Text(coordinator.LastTalkOutput, 13, "#F4ECFA");
        var actions = new WrapPanel { Margin = new Thickness(0, 8, 0, 4) };
        Button? send = null;
        send = Button("Enviar", async () =>
        {
            if (asking is not null) return;
            asking = new CancellationTokenSource(); send!.IsEnabled = false; result.Text = "O Familiar está olhando a cena…";
            var hideForGameplay = coordinator.Capturing;
            if (hideForGameplay) { Hide(); coordinator.RefocusGame(); }
            try { result.Text = await coordinator.AskAsync(prompt.Text, asking.Token); coordinator.RememberTalk(result.Text); pet.SayAnswer(result.Text); }
            catch (OperationCanceledException) { result.Text = "Pergunta cancelada."; coordinator.RememberTalk(result.Text); pet.Say(result.Text); }
            catch (Exception ex) { result.Text = ex.Message; coordinator.RememberTalk(result.Text); pet.Say(result.Text); }
            finally { asking?.Dispose(); asking = null; send!.IsEnabled = true; if (hideForGameplay) Close(); else PositionNearFamiliar(); }
        });
        var cancel = Button("Fechar", () =>
        {
            if (asking is not null) { asking.Cancel(); coordinator.CancelAsk(); return; }
            coordinator.RefocusGame(); Close();
        });
        actions.Children.Add(send); actions.Children.Add(cancel); body.Children.Add(actions);
        var suggestions = coordinator.Capturing
            ? new[] { "O que está acontecendo?", "Quais opções aparecem?", "O que você faria?", "Essa escolha é perigosa?", "Me dá um recap." }
            : new[] { "Me dá um recap.", "O que estou fazendo atualmente?", "O que está acontecendo?" };
        var quick = new WrapPanel(); foreach (var item in suggestions) quick.Children.Add(Button(item, () => { prompt.Text = item; prompt.Focus(); })); body.Children.Add(quick);
        body.Children.Add(new Border { Background = Brush("#241A30"), BorderBrush = Brush("#563B70"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(10), Padding = new Thickness(9, 6, 5, 6), Margin = new Thickness(0, 5, 0, 0), Child = new ScrollViewer { Content = result, MaxHeight = 220, VerticalScrollBarVisibility = ScrollBarVisibility.Auto } });
        Loaded += (_, _) => prompt.Focus();
    }
    private void BuildControl(Action openControlCenter)
    {
        // This method is also used after toggling Web/follow. Clear first so a
        // previously attached heading is never added to a second visual tree.
        body.Children.Clear();
        Header("🦇  Painel do Familiar");
        body.Children.Add(Text(coordinator.Status, 12, "#C9B8D5"));
        var grid = new WrapPanel();
        grid.Children.Add(Button(coordinator.Following ? "👁 Acompanhando" : "◌ Passivo", () => { coordinator.SetFollowing(!coordinator.Following); BuildControl(openControlCenter); }));
        grid.Children.Add(Button(coordinator.Watching ? "🧠 Watch: ON" : "🧠 Watch: OFF", () => { coordinator.SetWatch(!coordinator.Watching); BuildControl(openControlCenter); }));
        if (coordinator.Watching) body.Children.Add(Text(coordinator.WatchStatus, 11, "#A997B9"));
        grid.Children.Add(Button("📖 Recap", () => ShowText("Recap", coordinator.Recap(), openControlCenter)));
        grid.Children.Add(Button("🗂 Run", () => BuildRuns(openControlCenter)));
        grid.Children.Add(Button(coordinator.Settings.WebResearchEnabled ? "🌐 Web: ON" : "🌐 Web: OFF", () => { coordinator.SetWebResearch(!coordinator.Settings.WebResearchEnabled); BuildControl(openControlCenter); }));
        grid.Children.Add(Button("⚠ Spoilers", () => BuildSpoilers(openControlCenter)));
        grid.Children.Add(Button("📁 Pasta local", () => coordinator.OpenGameFolder()));
        grid.Children.Add(Button("⚙ Control Center", () => { Close(); openControlCenter(); }));
        body.Children.Add(grid);
    }
    private void BuildRuns(Action openControlCenter)
    {
        body.Children.Clear(); Header("🗂 Runs"); body.Children.Add(Text("Atual: " + coordinator.RunName, 12, "#C9B8D5"));
        foreach (var item in coordinator.Runs) body.Children.Add(Button(item.DisplayName, () => { coordinator.SwitchRun(item.Id); BuildRuns(openControlCenter); }));
        var name = new TextBox { Padding = new Thickness(8), Background = Brush("#291E35"), Foreground = Foreground, BorderBrush = Brush("#805BA0"), Text = "Nova run" };
        body.Children.Add(name);
        var actions = new WrapPanel(); actions.Children.Add(Button("Criar", () => { coordinator.CreateRun(name.Text); BuildRuns(openControlCenter); })); actions.Children.Add(Button("Renomear atual", () => { coordinator.RenameRun(name.Text); BuildRuns(openControlCenter); })); actions.Children.Add(Button("Voltar", () => BuildControl(openControlCenter))); body.Children.Add(actions);
    }
    private void BuildSpoilers(Action openControlCenter)
    {
        body.Children.Clear(); Header("⚠ Spoilers"); body.Children.Add(Text("Política atual: " + coordinator.Settings.SpoilerPolicy, 12, "#C9B8D5"));
        foreach (var policy in Enum.GetValues<SpoilerPolicy>()) body.Children.Add(Button(policy.ToString(), () => { coordinator.SetSpoilerPolicy(policy); BuildControl(openControlCenter); }));
    }
    private void ShowText(string title, string text, Action openControlCenter)
    {
        body.Children.Clear(); Header(title); body.Children.Add(Text(text, 13)); body.Children.Add(Button("Ver detalhes", () => { Close(); openControlCenter(); })); body.Children.Add(Button("Voltar", () => BuildControl(openControlCenter)));
    }
    private void Header(string value) { heading.Text = value; body.Children.Add(heading); }
    private static TextBlock Text(string value, int size, string color = "#F4ECFA") => new() { Text = value, FontSize = size, Foreground = Brush(color), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 3, 0, 8) };
    private static Button Button(string label, Action action) => Button(label, () => { action(); return System.Threading.Tasks.Task.CompletedTask; });
    private static Button Button(string label, Func<System.Threading.Tasks.Task> action)
    {
        var button = new Button { Content = label, Padding = new Thickness(10, 6, 10, 6), Margin = new Thickness(0, 3, 6, 3), Background = Brush("#352443"), Foreground = Brush("#F4ECFA"), BorderBrush = Brush("#A67ACC"), Cursor = Cursors.Hand };
        button.Click += async (_, _) => await action(); return button;
    }
    private static SolidColorBrush Brush(string value) => (SolidColorBrush)new BrushConverter().ConvertFromString(value)!;
}
