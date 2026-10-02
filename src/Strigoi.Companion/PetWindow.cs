using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Strigoi.Companion.Core;
using Forms = System.Windows.Forms;

namespace Strigoi.Companion;

internal sealed class PetWindow : Window
{
    private readonly FamiliarManifest manifest;
    private readonly BitmapSource[,] frames;
    private readonly Image image = new() { Stretch = Stretch.Fill };
    private readonly TextBlock badge = new() { Foreground = Brushes.White, FontSize = 12, HorizontalAlignment = HorizontalAlignment.Center };
    private readonly Border badgeBorder;
    private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromMilliseconds(67) };
    private readonly DispatcherTimer balloonTimer = new();
    private readonly Popup speech = new() { AllowsTransparency = true, StaysOpen = true, Placement = PlacementMode.Top, VerticalOffset = 2, IsHitTestVisible = false };
    private readonly TextBlock speechText = new() { Foreground = Brushes.WhiteSmoke, FontSize = 13, LineHeight = 18, TextWrapping = TextWrapping.Wrap, MaxWidth = 320 };
    private readonly Stopwatch clock = new();
    private readonly Action openTalk;
    private readonly Action openQuickControl;
    private readonly Action toggleFollowing;
    private readonly Action changed;
    private readonly Action recover;
    private string state = "idle";
    private double duration;
    private bool paused;
    private int lastFrame = -1;
    private HwndSource? source;
    public CompanionSettings Settings { get; set; }
    public IntPtr Handle { get; private set; }
    public bool HotkeyRegistered { get; private set; }
    public int ObservedActivations { get; private set; }
    public bool AnimationRunning => timer.IsEnabled;
    public string CurrentState => state;
    public string? CurrentFallback => manifest.Clips[state].Fallback;
    public bool IsPaused => paused;
    public event Action? PresentationChanged;
    public long RenderedFrames { get; private set; }
    private readonly bool enableShortcut;

    public PetWindow(CompanionSettings settings, Action openTalk, Action openQuickControl, Action toggleFollowing, Action changed, Action recover, bool enableShortcut = true)
    {
        Settings = settings;
        this.openTalk = openTalk;
        this.openQuickControl = openQuickControl;
        this.toggleFollowing = toggleFollowing;
        this.changed = changed;
        this.recover = recover;
        this.enableShortcut = enableShortcut;
        Title = "Strigoi Companion — Familiar";
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;
        ShowActivated = false;
        Topmost = true;
        Focusable = false;
        manifest = FamiliarManifest.Load(Path.Combine(AppContext.BaseDirectory, "Assets", "familiar.json"));
        var bitmap = new BitmapImage();
        bitmap.BeginInit(); bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.UriSource = new Uri(Path.Combine(AppContext.BaseDirectory, "Assets", "spritesheet.png"));
        bitmap.EndInit(); bitmap.Freeze();
        if (bitmap.PixelWidth != manifest.Columns * manifest.CellWidth || bitmap.PixelHeight != manifest.Rows * manifest.CellHeight)
            throw new InvalidDataException("Dimensões do spritesheet não correspondem ao manifesto.");
        frames = new BitmapSource[manifest.Rows, manifest.Columns];
        for (int row = 0; row < manifest.Rows; row++)
            for (int col = 0; col < manifest.Columns; col++)
            {
                var frame = new CroppedBitmap(bitmap, new Int32Rect(col * manifest.CellWidth, row * manifest.CellHeight, manifest.CellWidth, manifest.CellHeight));
                frame.Freeze(); frames[row, col] = frame;
            }
        var grid = new Grid();
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(28) });
        grid.RowDefinitions.Add(new RowDefinition());
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(24) });
        badgeBorder = new Border { Child = badge, Background = new SolidColorBrush(Color.FromRgb(42, 30, 57)), CornerRadius = new CornerRadius(10), Padding = new Thickness(9, 3, 9, 3), HorizontalAlignment = HorizontalAlignment.Center, Visibility = Visibility.Hidden };
        grid.Children.Add(badgeBorder);
        Grid.SetRow(image, 1); grid.Children.Add(image);
        var speechBody = new Border { Child = speechText, Background = new SolidColorBrush(Color.FromRgb(35, 22, 47)), BorderBrush = new SolidColorBrush(Color.FromRgb(184, 138, 230)), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(14), Padding = new Thickness(14, 10, 14, 10), Effect = new System.Windows.Media.Effects.DropShadowEffect { Color = Colors.Black, Opacity = .45, BlurRadius = 12, ShadowDepth = 3 } };
        var tail = new System.Windows.Shapes.Polygon { Points = new PointCollection { new Point(0, 0), new Point(16, 0), new Point(8, 8) }, Fill = new SolidColorBrush(Color.FromRgb(35, 22, 47)), Stroke = new SolidColorBrush(Color.FromRgb(184, 138, 230)), StrokeThickness = 1, HorizontalAlignment = HorizontalAlignment.Center };
        speech.Child = new StackPanel { Children = { speechBody, tail } };
        speech.PlacementTarget = image;
        var follow = new Button { Content = "👁", FontSize = 11, Padding = new Thickness(6, 1, 6, 1), Background = Brushes.Transparent, Foreground = Brushes.WhiteSmoke, BorderBrush = Brushes.Transparent, ToolTip = "Acompanhar jogo: ligar/desligar" };
        follow.Click += (_, _) => toggleFollowing(); Grid.SetRow(follow, 2); grid.Children.Add(follow);
        Content = grid;
        RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.HighQuality);
        SetSize();
        SourceInitialized += (_, _) =>
        {
            Handle = new WindowInteropHelper(this).Handle;
            source = HwndSource.FromHwnd(Handle); source.AddHook(Hook);
            Native.Mode(Handle, Settings.Mode == InteractionMode.Locked);
            RegisterShortcut();
        };
        Loaded += (_, _) => { RestorePosition(); React("idle"); };
        Activated += (_, _) => ObservedActivations++;
        IsVisibleChanged += (_, _) => UpdateTimer();
        MouseLeftButtonDown += OnClick;
        MouseRightButtonUp += (_, e) => { openQuickControl(); e.Handled = true; };
        timer.Tick += (_, _) => Tick();
        balloonTimer.Tick += (_, _) => { balloonTimer.Stop(); speech.IsOpen = false; };
        Closed += (_, _) => { timer.Stop(); balloonTimer.Stop(); speech.IsOpen = false; Native.UnregisterHotKey(Handle, 1); source?.RemoveHook(Hook); };
    }

    private IntPtr Hook(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (message == 0x21) { handled = true; return (IntPtr)3; } // MA_NOACTIVATE
        if (message == 0x312 && wParam == (IntPtr)1) { openQuickControl(); handled = true; }
        if (message == 0x232) { SavePosition(); changed(); } // end native move loop
        if (message == 0x7E || message == 0x1A) // monitor/work-area change
            Dispatcher.BeginInvoke(new Action(() => { RestorePosition(); recover(); }), DispatcherPriority.Background);
        return IntPtr.Zero;
    }

    private void OnClick(object sender, MouseButtonEventArgs e)
    {
        if (Settings.Mode == InteractionMode.Reposition)
        {
            Native.ReleaseCapture();
            Native.SendMessage(Handle, 0xA1, (IntPtr)2, IntPtr.Zero); // native caption drag without changing cursor
            e.Handled = true;
        }
        else { openTalk(); e.Handled = true; }
    }

    public void SetMode(InteractionMode mode)
    {
        Settings = Settings with { Mode = mode };
        Native.Mode(Handle, mode == InteractionMode.Locked);
        UpdateBadge();
        changed();
        PresentationChanged?.Invoke();
    }

    public void RegisterShortcut()
    {
        Native.UnregisterHotKey(Handle, 1);
        HotkeyRegistered = enableShortcut && Native.RegisterHotKey(Handle, 1, 0x4003, (uint)Settings.Hotkey);
    }

    public void SetScale(double scale)
    {
        SavePosition();
        Settings = (Settings with { Scale = scale }).Normalize();
        SetSize();
        Dispatcher.BeginInvoke(new Action(() => { RestorePosition(); changed(); }), DispatcherPriority.Loaded);
    }

    private void SetSize() { Width = 192 * Settings.Scale; Height = 208 * Settings.Scale + 52; }

    public void RestorePosition()
    {
        if (Handle == IntPtr.Zero) return;
        var screens = Forms.Screen.AllScreens;
        var screen = Array.Find(screens, x => x.DeviceName == Settings.Monitor) ?? Forms.Screen.PrimaryScreen ?? screens[0];
        var work = screen.WorkingArea;
        if (!Native.GetWindowRect(Handle, out var rect)) return;
        var point = Placement.Restore(new(work.Left, work.Top, work.Width, work.Height), rect.Right - rect.Left, rect.Bottom - rect.Top, Settings.X, Settings.Y);
        Native.SetWindowPos(Handle, IntPtr.Zero, point.X, point.Y, 0, 0, Native.NoSize | Native.NoZOrder | Native.NoActivatePos);
        // A monitor transition can change the physical size after WPF handles DPI.
        Native.GetWindowRect(Handle, out rect);
        point = Placement.Restore(new(work.Left, work.Top, work.Width, work.Height), rect.Right - rect.Left, rect.Bottom - rect.Top, Settings.X, Settings.Y);
        Native.SetWindowPos(Handle, IntPtr.Zero, point.X, point.Y, 0, 0, Native.NoSize | Native.NoZOrder | Native.NoActivatePos);
    }

    public void SavePosition()
    {
        if (Handle == IntPtr.Zero || !Native.GetWindowRect(Handle, out var rect)) return;
        var screen = Forms.Screen.FromHandle(Handle);
        var work = screen.WorkingArea;
        var point = Placement.Save(new(work.Left, work.Top, work.Width, work.Height), rect.Right - rect.Left, rect.Bottom - rect.Top, rect.Left, rect.Top);
        Settings = Settings with { Monitor = screen.DeviceName, X = point.X, Y = point.Y };
    }

    public void ResetPosition()
    {
        Settings = Settings with { Monitor = "", X = .85, Y = .8 };
        Show(); RestorePosition(); SavePosition(); changed();
    }

    public void SetPaused(bool value)
    {
        paused = value;
        React(value ? "sleep" : "idle", 0);
        PresentationChanged?.Invoke();
    }

    public void Say(string text, TimeSpan? duration = null)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        speechText.Text = text.Trim();
        balloonTimer.Stop(); speech.IsOpen = true;
        balloonTimer.Interval = duration ?? TimeSpan.FromSeconds(Math.Clamp(3 + text.Length / 32d, 4, 12)); balloonTimer.Start();
    }

    // The HUD retains the complete response. The Familiar speaks a short,
    // readable excerpt so a long answer never covers the game.
    public void SayAnswer(string answer)
    {
        var lines = answer.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Trim())
            .Where(line => !line.Contains(" · ", StringComparison.Ordinal) && !line.StartsWith("Pesquisa externa", StringComparison.OrdinalIgnoreCase) && !line.StartsWith("qwen", StringComparison.OrdinalIgnoreCase));
        var text = string.Join(" ", lines);
        if (text.Length > 300)
        {
            var cut = text.LastIndexOf(' ', 280);
            text = text[..(cut > 100 ? cut : 280)].TrimEnd() + "…";
        }
        Say(text, TimeSpan.FromSeconds(12));
    }

    public void SetReducedMotion(bool value)
    {
        Settings = Settings with { ReducedMotion = value };
        Tick(); UpdateTimer(); changed();
    }

    public void React(string next, double seconds = 4)
    {
        // Pausing is authoritative: manual previews must not restart animation.
        if (paused) next = "sleep";
        state = manifest.Clips.ContainsKey(next) ? next : "idle";
        duration = state is "idle" or "sleep" ? 0 : seconds;
        lastFrame = -1; clock.Restart();
        UpdateBadge();
        Tick(); UpdateTimer();
    }

    private void SetBadge(string? text)
    {
        badge.Text = text ?? "";
        badgeBorder.Visibility = text is null ? Visibility.Hidden : Visibility.Visible;
    }
    private void UpdateBadge() => SetBadge(state == "warning" ? "! Atenção · prévia" : state == "sleep" ? "Pausado" : Settings.Mode == InteractionMode.Reposition ? "Arraste para mover" : null);

    private void Tick()
    {
        if (duration > 0 && clock.Elapsed.TotalSeconds >= duration) { React(paused ? "sleep" : "idle", 0); return; }
        var clip = manifest.Clips[state];
        int frame = AnimationFrame.At(clip, clock.Elapsed.TotalMilliseconds, Settings.ReducedMotion);
        if (lastFrame != frame) { image.Source = frames[clip.Row, frame]; lastFrame = frame; RenderedFrames++; }
        if (!clip.Loop && duration == 0) UpdateTimer();
    }

    private void UpdateTimer()
    {
        var clip = manifest.Clips[state];
        bool animate = !Settings.ReducedMotion && !paused && clip.Frames.Length > 1 && (clip.Loop || clock.Elapsed.TotalMilliseconds < clip.FrameMs * clip.Frames.Length);
        if (IsVisible && (animate || duration > 0)) timer.Start(); else timer.Stop();
    }

    public void RenderPreview(string file)
    {
        UpdateLayout();
        var target = new RenderTargetBitmap((int)Math.Ceiling(ActualWidth), (int)Math.Ceiling(ActualHeight), 96, 96, PixelFormats.Pbgra32);
        target.Render(this);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(target));
        using var stream = File.Create(file); encoder.Save(stream);
    }
}
