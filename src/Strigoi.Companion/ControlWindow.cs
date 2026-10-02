using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Strigoi.Companion.Core;

namespace Strigoi.Companion;

internal sealed class ControlWindow : Window
{
    private readonly PetWindow pet;
    private readonly TextBlock status;
    private readonly ComboBox modes;
    public ControlWindow(PetWindow pet, Action save, string? warning, Action? validate = null, Action? capture = null)
    {
        this.pet = pet;
        Title = WorkAssistantBridge.IsAvailable ? "Strigoi Companion Assistant · Control Center" : "Strigoi Companion · Control Center"; Width = 490; Height = 760;
        MinWidth = 400; MinHeight = 460; WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Background = Brush("#17131F"); Foreground = Brush("#F3EBFA"); FontFamily = new FontFamily("Segoe UI");
        status = Text("", 12, "#D8B8FF");
        var stack = new StackPanel { Margin = new Thickness(26) };
        Content = new ScrollViewer { Content = stack, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        stack.Children.Add(Text(WorkAssistantBridge.IsAvailable ? "STRIGOI COMPANION ASSISTANT" : "STRIGOI COMPANION", 12, "#BD93F9"));
        stack.Children.Add(Text("Control Center", 27));
        stack.Children.Add(Text("O Familiar é a interface principal. Use este painel para ajustes, captura manual e diagnóstico.", 14, "#C1B4D1"));
        stack.Children.Add(Text($"Versão {GetType().Assembly.GetName().Version?.ToString(3)} · captura e Ask/Talk locais.", 13, "#C1B4D1"));
        Label(stack, "INTERAÇÃO");
        modes = new ComboBox { ItemsSource = new[] { "Bloqueado · cliques passam ao jogo", "Interativo · clique no pet para abrir", "Reposicionar · arraste o pet" }, SelectedIndex = (int)pet.Settings.Mode, Margin = new Thickness(0, 4, 0, 8), Padding = new Thickness(6) };
        modes.SelectionChanged += (_, _) => { if (modes.SelectedIndex >= 0 && (int)pet.Settings.Mode != modes.SelectedIndex) pet.SetMode((InteractionMode)modes.SelectedIndex); UpdateStatus(); };
        stack.Children.Add(modes);
        stack.Children.Add(Text("Use o atalho ou o ícone da bandeja quando estiver bloqueado.", 12, "#C1B4D1"));
        Label(stack, "TAMANHO");
        var size = Text($"{pet.Settings.Scale:P0}", 13);
        var slider = new Slider { Minimum = .5, Maximum = 2, TickFrequency = .25, IsSnapToTickEnabled = true, Value = pet.Settings.Scale, Margin = new Thickness(0, 8, 0, 4) };
        slider.ValueChanged += (_, _) => { size.Text = $"{slider.Value:P0}"; pet.SetScale(slider.Value); };
        stack.Children.Add(slider); stack.Children.Add(size);
        var reduced = new CheckBox { Content = "Reduzir movimento", IsChecked = pet.Settings.ReducedMotion, Foreground = Foreground, Margin = new Thickness(0, 12, 0, 4) };
        reduced.Checked += (_, _) => pet.SetReducedMotion(true);
        reduced.Unchecked += (_, _) => pet.SetReducedMotion(false);
        stack.Children.Add(reduced);
        Label(stack, "ATALHO PARA ABRIR");
        var shortcut = new ComboBox { ItemsSource = new[] { "Ctrl + Alt + F8", "Ctrl + Alt + F9", "Ctrl + Alt + F10", "Ctrl + Alt + F11" }, SelectedIndex = pet.Settings.Hotkey - 119, Padding = new Thickness(6) };
        shortcut.SelectionChanged += (_, _) => { pet.Settings = pet.Settings with { Hotkey = 119 + shortcut.SelectedIndex }; pet.RegisterShortcut(); save(); UpdateStatus(); };
        stack.Children.Add(shortcut);
        Label(stack, "EXPRESSÕES · PRÉVIA MANUAL");
        var reactions = new WrapPanel();
        foreach (var (label, state) in new[] { ("Acenar", "happy"), ("Celebrar", "excited"), ("Pensar", "thinking"), ("Frustração*", "facepalm"), ("Surpresa*", "shocked"), ("Alerta*", "warning") })
            reactions.Children.Add(Button(label, () => { pet.React(state); status.Text = pet.CurrentFallback ?? "Prévia visual; nenhuma análise do jogo foi feita."; }));
        stack.Children.Add(reactions);
        stack.Children.Add(Text("* Usa poses existentes. Arte dedicada ainda pendente.", 11, "#C1B4D1"));
        stack.Children.Add(status); UpdateStatus();
        if (warning is not null) stack.Children.Add(Text(warning, 12, "#FFCC88"));
        stack.Children.Add(Button("Trazer pet de volta", () => pet.ResetPosition()));
        Label(stack, "DIAGNÓSTICO E FALLBACK");
        stack.Children.Add(Button("Captura manual · escolher janela", capture ?? (() => { })));
        stack.Children.Add(Button("Testar com meu jogo", validate ?? (() => { })));
        if (WorkAssistantBridge.IsAvailable)
        {
            Label(stack, "ASSISTENTE DE TRABALHO");
            stack.Children.Add(Button("Abrir assistente de trabalho · tarefas, notas e checklists", WorkAssistantBridge.Open));
        }
        stack.Children.Add(Button("Pronto · voltar ao jogo", Close));
        KeyDown += (_, e) => { if (e.Key == Key.Escape) Close(); };
        void Synchronize()
        {
            modes.SelectedIndex = (int)pet.Settings.Mode;
            UpdateStatus();
        }
        pet.PresentationChanged += Synchronize;
        Closed += (_, _) => { pet.PresentationChanged -= Synchronize; save(); };
    }
    private void UpdateStatus() => status.Text = pet.HotkeyRegistered ? $"Atalho ativo: Ctrl + Alt + F{pet.Settings.Hotkey - 111}. Escape fecha este painel." : "Atalho indisponível. Escolha outra tecla ou use a bandeja.";
    internal InteractionMode DisplayedMode => (InteractionMode)modes.SelectedIndex;
    public void RenderPreview(string path)
    {
        UpdateLayout();
        var bitmap = new System.Windows.Media.Imaging.RenderTargetBitmap((int)ActualWidth, (int)ActualHeight, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(this);
        var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
        encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
        using var stream = System.IO.File.Create(path); encoder.Save(stream);
    }
    private static SolidColorBrush Brush(string hex) => (SolidColorBrush)new BrushConverter().ConvertFromString(hex)!;
    private static TextBlock Text(string text, int size, string color = "#F3EBFA") => new() { Text = text, FontSize = size, Foreground = Brush(color), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 3, 0, 6) };
    private static void Label(Panel panel, string text) { var label = Text(text, 11, "#BD93F9"); label.Margin = new Thickness(0, 18, 0, 4); panel.Children.Add(label); }
    private static Button Button(string label, Action action)
    {
        var button = new Button { Content = label, Padding = new Thickness(11, 7, 11, 7), Margin = new Thickness(0, 4, 6, 4), Background = Brush("#30223F"), Foreground = Brush("#F3EBFA"), BorderBrush = Brush("#725091") };
        button.Click += (_, _) => action(); return button;
    }
}
