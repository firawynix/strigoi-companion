using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Strigoi.Companion;

internal sealed class ValidationWindow : Window
{
    private readonly RuntimeProbe probe;
    private readonly TextBlock status = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 14, 0, 14) };
    private readonly TextBox game = new() { MaxLength = 120, Padding = new Thickness(6) };
    private readonly ComboBox display = new() { ItemsSource = new[] { "Não informado", "Janela", "Borderless", "Fullscreen exclusivo" }, SelectedIndex = 0, Padding = new Thickness(6) };
    private readonly Dictionary<string, ComboBox> answers = new();
    private readonly string reportPath;
    private readonly Button start, stop;
    private bool hasStarted;

    public ValidationWindow(PetWindow pet, string reportsDirectory)
    {
        probe = new RuntimeProbe(pet);
        reportPath = Path.Combine(reportsDirectory, $"validation-{DateTime.Now:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}.json");
        Title = "Strigoi Companion — teste com seu jogo"; Width = 540; Height = 760;
        MinWidth = 420; MinHeight = 440; WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Background = new SolidColorBrush(Color.FromRgb(23, 19, 31)); Foreground = Brushes.WhiteSmoke;
        var body = new StackPanel { Margin = new Thickness(24) };
        Content = new ScrollViewer { Content = body, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        void Text(string text, int size = 14) => body.Children.Add(new TextBlock { Text = text, FontSize = size, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 6) });
        Text("Vamos testar na sua partida.", 25);
        Text("Abra o jogo em janela ou borderless. Inicie a medição e minimize este painel. O pet deve continuar visível e bloqueado; use a bandeja para reposicioná-lo quando quiser.");
        Text("A medição dura 30 minutos e observa somente o consumo do Companion. Não captura imagens, não lê o jogo e não mede FPS.", 12);
        Text("Jogo"); body.Children.Add(game);
        Text("Modo da janela"); body.Children.Add(display);
        var actions = new WrapPanel();
        start = Button("Iniciar medição", () => { });
        stop = Button("Encerrar medição", () => { probe.Stop(); UpdateStatus(); });
        stop.IsEnabled = false;
        start.Click += (_, _) =>
        {
            hasStarted = true; probe.Start(); start.IsEnabled = false; stop.IsEnabled = true;
            game.IsEnabled = false; display.IsEnabled = false;
            status.Text = "Medição em andamento. Minimize este painel e volte ao jogo.";
        };
        actions.Children.Add(start); actions.Children.Add(stop); body.Children.Add(actions);
        body.Children.Add(status);
        Text("Depois de testar, registre o que aconteceu:", 15);
        foreach (var (key, label) in new[]
        {
            ("clickThrough", "Os cliques atravessaram o pet bloqueado?"),
            ("focus", "O pet ficou sem roubar o foco?"),
            ("drag", "Foi possível reposicionar e voltar a bloquear?"),
            ("shortcut", "O atalho ou a bandeja recuperou os controles?"),
            ("perceivedPerformance", "O jogo continuou fluido durante o teste?")
        })
        {
            Text(label, 12);
            var answer = new ComboBox { ItemsSource = new[] { "Ainda não testei", "Sim, funcionou", "Encontrei problema" }, SelectedIndex = 0, Margin = new Thickness(0, 0, 0, 3) };
            answers.Add(key, answer); body.Children.Add(answer);
        }
        Text("Suas respostas serão identificadas como relato do jogador. Itens não testados continuam pendentes.", 12);
        body.Children.Add(Button("Salvar avaliação local", SaveReport));
        body.Children.Add(Button("Abrir pasta dos relatórios", () =>
        {
            Directory.CreateDirectory(reportsDirectory);
            Process.Start(new ProcessStartInfo("explorer.exe") { ArgumentList = { reportsDirectory }, UseShellExecute = false });
        }));
        probe.Updated += UpdateStatus;
        status.Text = "Medição ainda não iniciada.";
        Closed += (_, _) =>
        {
            probe.Updated -= UpdateStatus; probe.Stop();
            if (hasStarted) SaveReport();
            probe.Dispose();
        };
    }
    private void UpdateStatus()
    {
        stop.IsEnabled = probe.Running;
        string state = probe.Running ? "Em andamento" : probe.Seconds >= 1800 ? "30 minutos registrados" : "Amostra curta encerrada";
        status.Text = $"{state} · {TimeSpan.FromSeconds(probe.Seconds):mm\\:ss} · {probe.Samples.Count} amostras. A avaliação do jogo continua dependendo das suas respostas.";
        if (!probe.Running) SaveReport();
    }
    private void SaveReport()
    {
        try
        {
            string[] values = { "not-tested", "passed", "failed" };
            probe.Save(reportPath, game.Text.Trim(), display.SelectedItem?.ToString() ?? "Não informado", answers.ToDictionary(a => a.Key, a => values[Math.Max(0, a.Value.SelectedIndex)]));
            status.Text = probe.Running ? "Avaliação salva; a medição continua." : "Avaliação salva na pasta de relatórios. Itens não testados permanecem pendentes.";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            status.Text = "Não consegui salvar o relatório. Confira a permissão da pasta de relatórios.";
        }
    }
    internal void RenderPreview(string file)
    {
        UpdateLayout();
        var bitmap = new System.Windows.Media.Imaging.RenderTargetBitmap((int)ActualWidth, (int)ActualHeight, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(this);
        var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
        encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
        using var stream = File.Create(file); encoder.Save(stream);
    }
    // Tests exercise the same UI events as the two buttons, without desktop input injection.
    internal void StartForTest() => start.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
    internal void StopForTest() => stop.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
    internal string ReportPath => reportPath;
    private static Button Button(string text, Action action)
    {
        var button = new Button { Content = text, Padding = new Thickness(10, 7, 10, 7), Margin = new Thickness(0, 8, 8, 4) };
        button.Click += (_, _) => action(); return button;
    }
}
