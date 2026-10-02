using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Strigoi.Companion;

internal sealed class CaptureWindow : Window
{
    private readonly WindowCapture capture = new();
    private readonly ComboBox targets = new() { MinWidth = 240, Padding = new Thickness(7), MaxDropDownHeight = 240 };
    private readonly TextBlock status = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 12, 0, 8) };
    private readonly Image preview = new() { Height = 260, Stretch = Stretch.Uniform };
    private readonly Button start, pause, stop, refresh, replay, previous, next, live;
    private readonly Button saveReport;
    private readonly TextBox question = new() { MinWidth = 360, MaxLength = 2_000, Padding = new Thickness(8), TextWrapping = TextWrapping.Wrap, AcceptsReturn = true, Height = 70 };
    private readonly TextBox gameName = new() { MinWidth = 360, MaxLength = 200, Padding = new Thickness(8) };
    private readonly TextBox runName = new() { MinWidth = 260, MaxLength = 120, Padding = new Thickness(8) };
    private readonly ComboBox runs = new() { MinWidth = 260, DisplayMemberPath = "DisplayName", Padding = new Thickness(7) };
    private readonly TextBox memoryText = new() { MinWidth = 360, MaxLength = 1000, Padding = new Thickness(8), TextWrapping = TextWrapping.Wrap, AcceptsReturn = true, Height = 55 };
    private readonly CheckBox webResearch = new() { Content = "Permitir pesquisa web explícita", Margin = new Thickness(0, 8, 0, 4) };
    private readonly ComboBox spoilerPolicy = new() { MinWidth = 180, Padding = new Thickness(7), ItemsSource = Enum.GetValues<Core.SpoilerPolicy>() };
    private readonly TextBlock chronicleStatus = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 6, 0, 6) };
    private readonly Button ask = new() { Content = "Perguntar ao Familiar", Padding = new Thickness(12, 8, 12, 8), Margin = new Thickness(0, 8, 8, 4) };
    private readonly Button cancelAsk = new() { Content = "Cancelar pergunta", Padding = new Thickness(12, 8, 12, 8), Margin = new Thickness(0, 8, 8, 4), IsEnabled = false };
    private readonly TextBlock answer = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 8) };
    private readonly LocalVisionProvider provider;
    private readonly Core.GameChronicleService chronicle;
    private readonly Action<bool, Core.SpoilerPolicy> saveResearchPreferences;
    private readonly string reportsDirectory;
    private Core.CapturedMoment[]? review;
    private int reviewIndex;
    private readonly TextBlock reviewStatus = new() { Margin = new Thickness(0, 8, 0, 8), TextWrapping = TextWrapping.Wrap };
    private CaptureTarget? selected;
    private Core.ChronicleGame? activeGame;
    private Core.ChronicleRun? activeRun;
    private Core.ChronicleSession? activeChronicleSession;
    private bool busy, paused, closing, canClose;
    private bool asking;
    private CancellationTokenSource? askCancellation;
    private Task? askTask;
    private bool replayOpened, pauseUsed, resumeUsed;
    private Task? closeTask;
    internal CaptureWindow(string reportsDirectory) : this(reportsDirectory, reportsDirectory) { }
    internal CaptureWindow(string reportsDirectory, string profileDirectory, Core.VisionRuntime? runtime = null,
        bool webEnabled = false, Core.SpoilerPolicy policy = Core.SpoilerPolicy.hint, Action<bool, Core.SpoilerPolicy>? saveResearchPreferences = null)
    {
        this.reportsDirectory = reportsDirectory;
        provider = new LocalVisionProvider(runtime ?? Core.VisionRuntime.Default);
        chronicle = new Core.GameChronicleService(profileDirectory);
        this.saveResearchPreferences = saveResearchPreferences ?? ((_, _) => { });
        Title = "Strigoi Companion · Captura local"; Width = 700; Height = 770; MinWidth = 500; MinHeight = 460;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Background = new SolidColorBrush(Color.FromRgb(23, 19, 31)); Foreground = Brushes.WhiteSmoke;
        var body = new StackPanel { Margin = new Thickness(24) };
        Content = new ScrollViewer { Content = body, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        void Text(string value, int size = 14) => body.Children.Add(new TextBlock { Text = value, FontSize = size, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 12) });
        Text("Uma janela. Só nesta máquina.", 25);
        Text("Escolha a janela do jogo e inicie quando quiser. As imagens ficam apenas na memória, sem envio ou gravação. Fechar este painel encerra a captura.");
        body.Children.Add(targets);
        var buttons = new WrapPanel(); body.Children.Add(buttons);
        refresh = Add("Atualizar lista", () => { Refresh(); return Task.CompletedTask; });
        start = Add("Iniciar captura", () =>
        {
            selected = targets.SelectedItem as CaptureTarget;
            if (selected is null) { status.Text = "Escolha uma janela na lista."; return Task.CompletedTask; }
            if (string.IsNullOrWhiteSpace(gameName.Text)) gameName.Text = selected.Title.Trim(); paused = false; OpenChronicleSession(); capture.Start(selected); return Task.CompletedTask;
        });
        pause = Add("Pausar", async () =>
        {
            if (paused && selected is not null) { paused = false; resumeUsed = true; OpenChronicleSession(); capture.Start(selected); }
            else { paused = true; pauseUsed = true; await capture.Stop("Pausada. Imagens descartadas."); EndChronicleSession(); }
        });
        stop = Add("Encerrar", async () => { paused = false; await capture.Stop(); EndChronicleSession(); });
        saveReport = Add("Salvar relatório da sessão", () => { SaveReport(); return Task.CompletedTask; });
        body.Children.Add(status);
        body.Children.Add(new Border { Background = Brushes.Black, Child = preview, Padding = new Thickness(8) });
        var replayButtons = new WrapPanel(); body.Children.Add(replayButtons);
        replay = Add("Rever recentes", () => { review = capture.History.Snapshot(); reviewIndex = 0; replayOpened = review.Length > 0; ShowReview(); return Task.CompletedTask; });
        previous = Add("Anterior", () => { reviewIndex--; ShowReview(); return Task.CompletedTask; });
        next = Add("Próxima", () => { reviewIndex++; ShowReview(); return Task.CompletedTask; });
        live = Add("Voltar ao vivo", () => { review = null; ShowLatest(); return Task.CompletedTask; });
        foreach (var button in new[] { replay, previous, next, live }) { buttons.Children.Remove(button); replayButtons.Children.Add(button); }
        body.Children.Add(reviewStatus);
        Text("Jogo desta sessão", 16);
        body.Children.Add(gameName);
        Text("Confirmado a partir da janela escolhida. Edite se o título da janela não for o nome do jogo.", 12);
        Text("Game Chronicle", 16);
        body.Children.Add(chronicleStatus);
        body.Children.Add(runs);
        body.Children.Add(runName);
        var runButtons = new WrapPanel();
        var createRun = Button("Criar run", CreateRun); var renameRun = Button("Renomear run", RenameRun); var openChronicle = Button("Abrir pasta do jogo", OpenChronicle); var recap = Button("Ver recap", ShowRecap);
        foreach (var button in new[] { createRun, renameRun, openChronicle, recap }) runButtons.Children.Add(button);
        body.Children.Add(runButtons);
        body.Children.Add(memoryText);
        var memoryButtons = new WrapPanel(); memoryButtons.Children.Add(Button("Lembrar fato do jogador", RecordPlayerMemory)); memoryButtons.Children.Add(Button("Criar promessa", RecordPromise)); body.Children.Add(memoryButtons);
        webResearch.IsChecked = webEnabled; spoilerPolicy.SelectedItem = policy;
        webResearch.Checked += (_, _) => SaveResearchPreferences(); webResearch.Unchecked += (_, _) => SaveResearchPreferences(); spoilerPolicy.SelectionChanged += (_, _) => SaveResearchPreferences();
        body.Children.Add(webResearch); body.Children.Add(spoilerPolicy);
        Text("Pesquisa web envia apenas o nome confirmado do jogo e a pergunta. Imagens, journals e dados pessoais nunca são enviados.", 12);
        Text("Pergunte sobre a imagem atual", 16);
        body.Children.Add(question);
        var askButtons = new WrapPanel(); askButtons.Children.Add(ask); askButtons.Children.Add(cancelAsk); body.Children.Add(askButtons);
        body.Children.Add(answer);
        Text("Ask/Talk experimental: sua pergunta envia somente a imagem atual para o modelo local configurado. Nada é enviado à internet ou gravado. A resposta é descartada se a captura mudar ou encerrar.", 12);
        Text("Durante a revisão, a captura continua. Pausar ou encerrar descarta também o replay. As imagens são amostras, não um vídeo contínuo.", 12);
        Text("Salvar relatório registra apenas dados da sessão e os controles usados; imagens não entram no arquivo.", 12);
        Text("Prévia reduzida · até 2 imagens por segundo · histórico de até 8 imagens. Mudança visual não significa diálogo, perigo ou decisão identificada.", 12);
        Text("Use janela ou borderless. Conteúdo protegido, HDR e tela cheia exclusiva ainda não foram homologados. Minimizar ou fechar o jogo encerra a captura.", 12);
        capture.Frame += (pixels, width, height, _, _) =>
        {
            if (review is null) ShowImage(pixels, width, height);
        };
        capture.Updated += Update;
        targets.SelectionChanged += (_, _) => { if (!capture.Running && targets.SelectedItem is CaptureTarget target) gameName.Text = target.Title.Trim(); };
        runs.SelectionChanged += (_, _) => SwitchRun();
        ask.Click += async (_, _) => await AskAsync();
        cancelAsk.Click += (_, _) => askCancellation?.Cancel();
        Closing += async (_, e) =>
        {
            if (canClose) return;
            e.Cancel = true;
            if (closing) return;
            closing = true; IsEnabled = false;
            await Shutdown();
        };
        Refresh(); Update();
        Button Add(string label, Func<Task> action)
        {
            var button = new Button { Content = label, Padding = new Thickness(12, 8, 12, 8), Margin = new Thickness(0, 10, 8, 0) };
            button.Click += async (_, _) =>
            {
                if (busy) return;
                busy = true; Update();
                try { await action(); }
                catch (Exception ex) { status.Text = ex.Message; }
                finally { busy = false; Update(false); }
            };
            buttons.Children.Add(button); return button;
        }
    }
    private void Refresh() { targets.ItemsSource = CaptureNative.Targets(); targets.SelectedIndex = -1; }
    private void Update() => Update(true);
    private void Update(bool text)
    {
        if (text) status.Text = capture.Status;
        if (!capture.Running) { preview.Source = null; review = null; askCancellation?.Cancel(); EndChronicleSession(); }
        replay.IsEnabled = !busy && capture.Running && capture.History.Count > 0 && review is null;
        previous.IsEnabled = !busy && review is not null && reviewIndex > 0;
        next.IsEnabled = !busy && review is not null && reviewIndex < review.Length - 1;
        live.IsEnabled = !busy && review is not null;
        if (review is null) reviewStatus.Text = capture.Running ? "Ao vivo" : "Sem imagens em memória.";
        targets.IsEnabled = refresh.IsEnabled = start.IsEnabled = !busy && !capture.Running && !paused;
        pause.IsEnabled = !busy && (capture.Running || paused); pause.Content = paused ? "Retomar" : "Pausar";
        stop.IsEnabled = !busy && (capture.Running || paused);
        saveReport.IsEnabled = !busy && capture.Snapshot(replayOpened, pauseUsed, resumeUsed) is not null;
        ask.IsEnabled = !asking && capture.Running && capture.History.Count > 0;
        cancelAsk.IsEnabled = asking;
        question.IsEnabled = !asking;
        runs.IsEnabled = activeGame is not null && !capture.Running;
    }
    private void ShowImage(byte[] pixels, int width, int height)
    {
        var bitmap = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, pixels, width * 4);
        bitmap.Freeze(); preview.Source = bitmap;
    }
    private void ShowReview()
    {
        if (review is null || review.Length == 0) { review = null; return; }
        reviewIndex = Math.Clamp(reviewIndex, 0, review.Length - 1);
        var frame = review[reviewIndex]; ShowImage(frame.Pixels, frame.Width, frame.Height);
        reviewStatus.Text = $"Revisão · imagem {reviewIndex + 1} de {review.Length} · {frame.Time.TotalSeconds:F1}s após iniciar a sessão · captura continua";
    }
    private void ShowLatest()
    {
        var frames = capture.History.Snapshot();
        if (frames.Length > 0) { var frame = frames[^1]; ShowImage(frame.Pixels, frame.Width, frame.Height); }
    }
    private void SaveReport()
    {
        var snapshot = capture.Snapshot(replayOpened, pauseUsed, resumeUsed);
        if (snapshot is null) { status.Text = "Inicie uma captura antes de salvar um relatório."; return; }
        try
        {
            string file = Core.CaptureReport.Save(reportsDirectory, snapshot,
                typeof(CaptureWindow).Assembly.GetName().Version?.ToString() ?? "unknown");
            status.Text = "Relatório salvo em " + Path.GetFileName(file) + ". Imagens não foram salvas.";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            status.Text = "Não consegui salvar o relatório. Confira a permissão da pasta de relatórios.";
        }
    }
    private async Task AskAsync()
    {
        if (asking) return;
        var image = capture.LatestImage();
        if (image is null) { answer.Text = "Inicie a captura e espere uma imagem antes de perguntar."; return; }
        var session = capture.SessionId;
        asking = true; askCancellation = new CancellationTokenSource(); answer.Text = "O Familiar está olhando a cena…"; Update(false);
        try
        {
            var confirmedGame = string.IsNullOrWhiteSpace(gameName.Text) ? capture.TargetTitle : gameName.Text.Trim();
            EnsureChronicle(confirmedGame);
            var retrieval = chronicle.Retrieve(activeGame!, activeRun!, question.Text);
            var researched = false;
            if (ShouldResearch(question.Text) && retrieval.ReturnedCount == 0 && webResearch.IsChecked == true)
            {
                using var web = new DuckDuckGoResearchProvider();
                var research = await chronicle.ResearchGameQuestion(activeGame!, activeRun!, question.Text, SelectedPolicy(), true, web, askCancellation.Token);
                researched = research.Result?.Claims.Length > 0 && !research.FromCache;
                retrieval = chronicle.Retrieve(activeGame!, activeRun!, question.Text);
            }
            var pending = provider.AskAsync(question.Text, image, retrieval.Context, askCancellation.Token);
            askTask = pending;
            var result = await pending;
            if (!capture.IsCurrent(session)) { answer.Text = "A cena mudou ou a captura foi encerrada; resposta descartada."; return; }
            answer.Text = "Jogo confirmado da sessão: " + confirmedGame + "\nRun: " + activeRun!.DisplayName +
                (researched ? "\nPesquisa externa adicionada ao cache; confirme a fonte antes de decisões críticas." : "") + "\n\n" +
                Core.VisionAnswerDisplay.WithoutGameGuesses(result.Text) +
                $"\n\n{result.Model} · {result.Elapsed.TotalSeconds:F1}s";
        }
        catch (OperationCanceledException) { answer.Text = "Pergunta cancelada."; }
        catch (Exception ex) { answer.Text = ex.Message; }
        finally { askTask = null; askCancellation?.Dispose(); askCancellation = null; asking = false; Update(false); }
    }
    private Button Button(string label, Action action)
    {
        var button = new System.Windows.Controls.Button { Content = label, Padding = new Thickness(10, 7, 10, 7), Margin = new Thickness(0, 4, 8, 4) };
        button.Click += (_, _) => { try { action(); } catch (Exception ex) { chronicleStatus.Text = ex.Message; } };
        return button;
    }
    private void OpenChronicleSession()
    {
        var name = string.IsNullOrWhiteSpace(gameName.Text) ? selected?.Title ?? "" : gameName.Text.Trim();
        EnsureChronicle(name); EndChronicleSession(); activeChronicleSession = chronicle.StartSession(activeGame!, activeRun!); UpdateChronicleStatus();
    }
    private void EnsureChronicle(string name)
    {
        var game = chronicle.OpenGame(name);
        if (activeGame?.Id != game.Id) { EndChronicleSession(); activeGame = game; activeRun = chronicle.OpenDefaultRun(game); ReloadRuns(); }
        else if (activeRun is null) activeRun = chronicle.OpenDefaultRun(game);
        UpdateChronicleStatus();
    }
    private void ReloadRuns()
    {
        if (activeGame is null) return;
        var available = chronicle.Runs(activeGame); runs.ItemsSource = available; runs.SelectedItem = available.FirstOrDefault(run => run.Id == activeRun?.Id) ?? available.FirstOrDefault();
    }
    private void SwitchRun()
    {
        if (runs.SelectedItem is not Core.ChronicleRun run || activeGame is null || run.Id == activeRun?.Id) return;
        EndChronicleSession(); activeRun = chronicle.OpenRun(activeGame, run.Id, run.DisplayName); UpdateChronicleStatus();
    }
    private void CreateRun()
    {
        if (activeGame is null) { chronicleStatus.Text = "Inicie a captura primeiro para confirmar o jogo."; return; }
        activeRun = chronicle.CreateRun(activeGame, string.IsNullOrWhiteSpace(runName.Text) ? "Nova run" : runName.Text); runName.Clear(); ReloadRuns(); UpdateChronicleStatus();
    }
    private void RenameRun()
    {
        if (activeGame is null || activeRun is null || string.IsNullOrWhiteSpace(runName.Text)) { chronicleStatus.Text = "Informe um novo nome para a run."; return; }
        activeRun = chronicle.RenameRun(activeGame, activeRun, runName.Text); runName.Clear(); ReloadRuns(); UpdateChronicleStatus();
    }
    private void RecordPlayerMemory()
    {
        if (!ReadyChronicle() || string.IsNullOrWhiteSpace(memoryText.Text)) { chronicleStatus.Text = "Inicie a captura e escreva o fato que quer lembrar."; return; }
        chronicle.RecordPlayerIntent(activeGame!, activeRun!, activeChronicleSession, memoryText.Text); memoryText.Clear(); chronicleStatus.Text = "Fato do jogador adicionado ao Chronicle.";
    }
    private void RecordPromise()
    {
        if (!ReadyChronicle() || string.IsNullOrWhiteSpace(memoryText.Text)) { chronicleStatus.Text = "Escreva a promessa que quer guardar."; return; }
        chronicle.RecordPromise(activeGame!, activeRun!, activeChronicleSession, null, memoryText.Text); memoryText.Clear(); chronicleStatus.Text = "Promessa ativa adicionada ao Chronicle.";
    }
    private void OpenChronicle()
    {
        if (!ReadyChronicle()) { chronicleStatus.Text = "Inicie a captura primeiro."; return; }
        Process.Start(new ProcessStartInfo { FileName = chronicle.GameDirectory(activeGame!), UseShellExecute = true });
    }
    private void ShowRecap()
    {
        if (!ReadyChronicle()) { chronicleStatus.Text = "Inicie a captura primeiro."; return; }
        var state = chronicle.State(activeGame!, activeRun!);
        answer.Text = "Recap — " + activeRun!.DisplayName + "\n\n" + string.Join("\n", state.Memories.TakeLast(12).Select(x => "• [" + x.Source + "] " + x.Summary)) + "\n\nPromessas:\n" + string.Join("\n", state.Promises.Where(x => x.Status == "active").Select(x => "• " + x.Intent));
    }
    private bool ReadyChronicle() => activeGame is not null && activeRun is not null;
    private void EndChronicleSession() { if (activeChronicleSession is not null) { chronicle.EndSession(activeChronicleSession); activeChronicleSession = null; } }
    private void UpdateChronicleStatus() => chronicleStatus.Text = activeGame is null || activeRun is null ? "Chronicle aguardando um jogo." : "Jogo: " + activeGame.DisplayName + " · Run: " + activeRun.DisplayName;
    private Core.SpoilerPolicy SelectedPolicy() => spoilerPolicy.SelectedItem is Core.SpoilerPolicy policy ? policy : Core.SpoilerPolicy.hint;
    private void SaveResearchPreferences() => saveResearchPreferences(webResearch.IsChecked == true, SelectedPolicy());
    private static bool ShouldResearch(string question) => new[] { "quest", "missão", "consequ", "escolha", "lore", "personagem", "fação", "item", "achievement", "troféu", "missable", "boss", "fraqueza", "timer", "direção", "direções", "rota", "como chegar", "onde fica", "onde estou" }.Any(term => question.Contains(term, StringComparison.OrdinalIgnoreCase));
    internal bool ReviewingForTest => review is not null;
    internal int ReviewIndexForTest => reviewIndex;
    internal void ReplayForTest() => replay.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
    internal void NextForTest() => next.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
    internal void LiveForTest() => live.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
    internal Task Shutdown() => closeTask ??= FinishClose();
    private async Task FinishClose()
    {
        // Stop can finish synchronously when idle. Always leave the Closing event
        // before calling Close again. Share this task with application shutdown.
        await Task.Yield();
        askCancellation?.Cancel();
        if (askTask is not null) { try { await askTask; } catch { } }
        await capture.Stop(); EndChronicleSession(); provider.Dispose();
        canClose = true; Close();
    }
    internal WindowCapture SessionForTest => capture;
    internal void SaveReportForTest() => SaveReport();
    internal void SelectForTest(CaptureTarget target) { targets.ItemsSource = new[] { target }; targets.SelectedIndex = 0; }
    internal void StartForTest() => start.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
    internal void PauseForTest() => pause.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
    internal void RenderPreview(string file)
    {
        UpdateLayout();
        var bitmap = new RenderTargetBitmap((int)ActualWidth, (int)ActualHeight, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(this);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = System.IO.File.Create(file); encoder.Save(stream);
    }
}
