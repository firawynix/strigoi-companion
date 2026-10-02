using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using Strigoi.Companion.Core;

namespace Strigoi.Companion;

internal enum FamiliarPresentation { Idle, Watching, Thinking, Researching, CanHelp, Warning, Passive }

// One quiet owner for automatic capture, Chronicle and Ask/Talk. Quick HUDs and
// the Control Center call this object; neither owns a second inference pipeline.
internal sealed class FamiliarCoordinator : IDisposable
{
    private readonly WindowCapture capture = new();
    private readonly GameChronicleService chronicle;
    private readonly LocalVisionProvider provider;
    private readonly DispatcherTimer detector = new() { Interval = TimeSpan.FromSeconds(1.5) };
    private readonly Action<CompanionSettings> updateSettings;
    private readonly bool workMode;
    private readonly string profileDirectory;
    private ChronicleGame? game;
    private ChronicleRun? run;
    private ChronicleSession? session;
    private CaptureTarget? target;
    private CancellationTokenSource? askCancellation;
    private CancellationTokenSource? watchCancellation;
    private bool watchActive;
    private DateTimeOffset lastCandidate, lastReaction;
    private int watchCandidates, watchCalls, watchReactions, watchDiscarded, watchCooldowns;
    private bool detecting;

    internal FamiliarCoordinator(string profileDirectory, CompanionSettings settings, Action<CompanionSettings> updateSettings, bool workMode = false)
    {
        this.profileDirectory = profileDirectory;
        this.updateSettings = updateSettings;
        this.workMode = workMode;
        chronicle = new GameChronicleService(profileDirectory, workMode);
        provider = new LocalVisionProvider(new VisionRuntime(settings.LocalVlmEndpoint, settings.LocalVlmModel), workMode);
        detector.Tick += async (_, _) => await DetectAsync();
        capture.Updated += () => { if (!capture.Running) EndSession(); Changed?.Invoke(); };
        capture.Frame += (_, _, _, difference, changed) => QueueWatch(difference, changed);
    }

    internal event Action? Changed;
    internal event Action<string>? Balloon;
    internal event Action<FamiliarPresentation>? Presentation;
    internal CompanionSettings Settings { get; private set; } = new();
    internal bool Following => Settings.FollowGameEnabled;
    internal bool AutoDetecting => Settings.AutoDetectGame;
    internal bool Capturing => capture.Running;
    internal bool Watching => Settings.WatchEnabled;
    internal string WatchStatus => $"Candidatos {watchCandidates} · análises {watchCalls} · reações {watchReactions} · descartes {watchDiscarded} · cooldown {watchCooldowns}";
    internal string GameName => game?.DisplayName ?? (workMode ? "Nenhum projeto" : "Nenhum jogo");
    internal string RunName => run?.DisplayName ?? "—";
    internal string LastTalkOutput { get; private set; } = "";
    internal IReadOnlyList<ChronicleRun> Runs => game is null ? [] : chronicle.Runs(game);
    internal CapturedMoment[] ReplayFrames() => capture.History.Snapshot();
    internal VisionImage? LatestImage() => capture.LatestImage();
    internal string SaveReport()
    {
        var snapshot = capture.Snapshot(false, false, false) ?? throw new InvalidOperationException("Inicie uma captura antes de salvar um relatório.");
        return CaptureReport.Save(Path.Combine(profileDirectory, "reports"), snapshot, GetType().Assembly.GetName().Version?.ToString() ?? "unknown");
    }
    internal void RecordNote(string note)
    {
        if (game is null || run is null || string.IsNullOrWhiteSpace(note)) return;
        chronicle.RecordEvent(game, run, session, "work_note", note.Trim(), ChronicleSource.user_stated, 1);
        Changed?.Invoke();
    }
    internal string Status => capture.Running ? capture.Status : Following ? (workMode ? "Escolha uma janela de trabalho" : "Aguardando um jogo") : "Acompanhamento pausado";

    internal void Start(CompanionSettings settings)
    {
        Settings = settings.Normalize();
        if (Settings.FollowGameEnabled && Settings.AutoDetectGame) detector.Start();
        Presentation?.Invoke(Settings.FollowGameEnabled ? FamiliarPresentation.CanHelp : FamiliarPresentation.Passive);
    }
    internal void SetFollowing(bool enabled)
    {
        Settings = Settings with { FollowGameEnabled = enabled };
        updateSettings(Settings);
        if (enabled && Settings.AutoDetectGame) { detector.Start(); Balloon?.Invoke("Acompanhando jogos."); Presentation?.Invoke(FamiliarPresentation.CanHelp); }
        else { detector.Stop(); _ = StopAsync(); Balloon?.Invoke("Acompanhamento pausado."); Presentation?.Invoke(FamiliarPresentation.Passive); }
        Changed?.Invoke();
    }
    internal void SetWebResearch(bool enabled) { Settings = Settings with { WebResearchEnabled = enabled }; updateSettings(Settings); Changed?.Invoke(); }
    internal void SetAutoDetect(bool enabled)
    {
        Settings = Settings with { AutoDetectGame = enabled };
        updateSettings(Settings);
        if (enabled && Following) detector.Start(); else detector.Stop();
        Changed?.Invoke();
    }
    internal void SetWatch(bool enabled) { Settings = Settings with { WatchEnabled = enabled }; updateSettings(Settings); if (!enabled) watchCancellation?.Cancel(); Balloon?.Invoke(enabled ? "Vou observar com discrição." : "Volto a apenas acompanhar."); Changed?.Invoke(); }
    internal void SetSpoilerPolicy(SpoilerPolicy policy) { Settings = Settings with { SpoilerPolicy = policy }; updateSettings(Settings); Changed?.Invoke(); }
    internal void SetModelStartup(ModelStartupMode mode) { Settings = Settings with { ModelStartupMode = mode }; updateSettings(Settings); Changed?.Invoke(); }

    private async Task DetectAsync()
    {
        if (detecting || !Following || !Settings.AutoDetectGame) return;
        detecting = true;
        try
        {
            // Quick HUDs are explicit Companion interactions, not a signal that the
            // game vanished. Keeping the capture alive also preserves Ask/Talk.
            if (CaptureNative.IsOwnWindow(Native.GetForegroundWindow())) return;
            var foreground = workMode ? CaptureNative.Targets().FirstOrDefault(item => item.Handle == Native.GetForegroundWindow()) : CaptureNative.ForegroundEligible();
            if (foreground is null)
            {
                if (capture.Running && Native.GetForegroundWindow() != target?.Handle) await StopAsync();
                return;
            }
            if (capture.Running && target?.Handle == foreground.Handle) return;
            if (capture.Running) await StopAsync();
            await StartForTargetAsync(foreground, true);
        }
        catch (Exception ex) { Balloon?.Invoke("Não consegui enxergar o jogo. Clique para escolher a janela."); Presentation?.Invoke(FamiliarPresentation.Warning); Debug.WriteLine(ex); }
        finally { detecting = false; Changed?.Invoke(); }
    }

    internal async Task StartForTargetAsync(CaptureTarget selected, bool automatic)
    {
        if (!workMode && !CaptureNative.IsLikelyGame(selected) && automatic) return;
        target = selected;
        var opened = chronicle.OpenGame(selected.Title.Trim());
        bool known = chronicle.Runs(opened).Count > 0;
        game = opened;
        var remembered = Settings.LastRunByGame.TryGetValue(opened.Id, out var runId) ? chronicle.Runs(opened).FirstOrDefault(x => x.Id == runId) : null;
        run = remembered ?? chronicle.MostRecentlyUsedRun(opened);
        RememberRun();
        if (Settings.AutoStartChronicle) session = chronicle.StartSession(opened, run);
        Balloon?.Invoke(known ? $"Bem-vindo de volta. Retomando {run.DisplayName}." : workMode ? "Primeiro contato. Vou criar a memória deste projeto." : "Primeiro contato. Vou criar a memória deste jogo.");
        Balloon?.Invoke($"Encontrei: {opened.DisplayName}.");
        Presentation?.Invoke(FamiliarPresentation.Watching);
        capture.Start(selected);
        if (Settings.ModelStartupMode == ModelStartupMode.AutomaticOnGameDetection) _ = WarmAsync();
        await Task.CompletedTask;
    }
    private async Task WarmAsync()
    {
        try { Balloon?.Invoke("Acordando meu cérebro..."); Presentation?.Invoke(FamiliarPresentation.Thinking); await provider.WarmAsync(CancellationToken.None); Balloon?.Invoke("Pronto."); Presentation?.Invoke(FamiliarPresentation.Watching); }
        catch { Balloon?.Invoke("Meu cérebro local não está pronto. Posso tentar quando você perguntar."); Presentation?.Invoke(FamiliarPresentation.CanHelp); }
    }
    internal async Task StopAsync()
    {
        askCancellation?.Cancel();
        watchCancellation?.Cancel();
        if (capture.Running) await capture.Stop("Acompanhamento aguardando um jogo.");
        EndSession(); target = null;
        if (Following) Presentation?.Invoke(FamiliarPresentation.CanHelp);
        Changed?.Invoke();
    }
    private void QueueWatch(double difference, bool changed)
    {
        // Capture's .12 threshold is intentionally strict for replay telemetry.
        // Watch may inspect a materially smaller motion change, still no more than
        // one candidate every ten seconds and always through the same VLM queue.
        if (!changed && difference < .06) return;
        if (!Settings.WatchEnabled || !Following || !capture.Running || watchActive || target is null || Native.GetForegroundWindow() != target.Handle) return;
        if (DateTimeOffset.UtcNow - lastCandidate < TimeSpan.FromSeconds(10)) { watchCooldowns++; return; }
        var frames = capture.History.Snapshot(); if (frames.Length < 2) return;
        lastCandidate = DateTimeOffset.UtcNow; watchCandidates++;
        var before = frames[^2]; var after = frames[^1]; _ = RunWatchAsync(new VisionImage(before.Pixels, before.Width, before.Height), new VisionImage(after.Pixels, after.Width, after.Height), capture.SessionId);
    }
    private async Task RunWatchAsync(VisionImage before, VisionImage after, Guid sessionId)
    {
        watchActive = true; watchCancellation = new CancellationTokenSource();
        try
        {
            watchCalls++; var result = await provider.WatchAsync(before, after, watchCancellation.Token);
            if (result is null || !capture.IsCurrent(sessionId) || !Settings.WatchEnabled) { watchDiscarded++; return; }
            if (!result.ShouldReact || result.Importance < 2 || result.Confidence < .70) { watchDiscarded++; return; }
            if (DateTimeOffset.UtcNow - lastReaction < TimeSpan.FromSeconds(45)) { watchCooldowns++; return; }
            lastReaction = DateTimeOffset.UtcNow; watchReactions++;
            var line = workMode ? result.Summary : result.Event == "possible_player_death" ? "Ah. Outra morte. Estou começando a achar que isso faz parte da estratégia." : result.Event == "possible_victory" ? "POR TODAS AS NOITES— você viu o que acabou de fazer?!" : "Bonito. Admito que foi bonito.";
            Balloon?.Invoke(line); Presentation?.Invoke(result.Importance == 3 ? FamiliarPresentation.Warning : FamiliarPresentation.CanHelp);
            if (result.ChronicleWorthiness >= 2 && result.Confidence >= .80 && game is not null && run is not null)
                chronicle.RecordEvent(game, run, session, result.Event, result.Summary, ChronicleSource.observed, result.Confidence);
        }
        catch (OperationCanceledException) { }
        catch { watchDiscarded++; }
        finally { watchActive = false; watchCancellation?.Dispose(); watchCancellation = null; Changed?.Invoke(); }
    }
    internal async Task<string> AskAsync(string question, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(question)) throw new InvalidOperationException("Escreva uma pergunta para o Familiar.");
        if (game is null || run is null)
        {
            Presentation?.Invoke(FamiliarPresentation.Thinking);
            try
            {
                var answer = await provider.ChatAsync(question, RememberedCampaigns(), cancellationToken);
                return "Conversando com o Familiar\n\n" + answer.Text + $"\n\n{answer.Model} · {answer.Elapsed.TotalSeconds:F1}s";
            }
            finally { Presentation?.Invoke(FamiliarPresentation.CanHelp); }
        }
        if (!workMode && AsksGameIdentity(question)) return $"Sim. Estou acompanhando **{game.DisplayName}**, na run **{run.DisplayName}**. Posso ajudar com a cena atual, recap e escolhas.";
        var image = capture.LatestImage();
        if (image is null) throw new InvalidOperationException(workMode ? "Ainda não tenho uma imagem da janela. Espere um instante ou selecione-a novamente." : "Ainda não tenho uma imagem do jogo. Espere um instante ou use a seleção manual.");
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken); askCancellation = linked;
        var frameSession = capture.SessionId;
        Presentation?.Invoke(FamiliarPresentation.Thinking);
        try
        {
            var retrieval = chronicle.Retrieve(game, run, question);
            var researched = false;
            if (Settings.WebResearchEnabled && (workMode || ShouldResearch(question)) && retrieval.ReturnedCount == 0)
            {
                Presentation?.Invoke(FamiliarPresentation.Researching); Balloon?.Invoke("Pesquisando...");
                using var web = new DuckDuckGoResearchProvider(workMode);
                var result = await chronicle.ResearchGameQuestion(game, run, question, Settings.SpoilerPolicy, true, web, linked.Token);
                researched = result.Result?.Claims.Length > 0 && !result.FromCache;
                retrieval = chronicle.Retrieve(game, run, question);
            }
            if (Settings.ModelStartupMode == ModelStartupMode.Manual) throw new InvalidOperationException("O modelo está em modo manual. Ative-o no Control Center.");
            var answer = await provider.AskAsync(question, image, retrieval.Context, linked.Token);
            if (!capture.IsCurrent(frameSession)) throw new OperationCanceledException("A cena mudou.");
            return $"{GameName} · {RunName}" + (researched ? "\nPesquisa externa adicionada ao cache." : "") + "\n\n" + (workMode ? answer.Text : VisionAnswerDisplay.WithoutGameGuesses(answer.Text)) + $"\n\n{answer.Model} · {answer.Elapsed.TotalSeconds:F1}s";
        }
        finally { askCancellation = null; Presentation?.Invoke(Following ? FamiliarPresentation.Watching : FamiliarPresentation.Passive); }
    }
    internal void CancelAsk() => askCancellation?.Cancel();
    internal void RememberTalk(string text) { if (!string.IsNullOrWhiteSpace(text)) LastTalkOutput = text; }
    internal void RefocusGame()
    {
        if (target is not null && CaptureNative.Matches(target)) Native.SetForegroundWindow(target.Handle);
    }
    internal string Recap()
    {
        if (game is null || run is null) return "Ainda não há um jogo em acompanhamento.";
        var state = chronicle.State(game, run);
        var memories = state.Memories.TakeLast(6).Select(x => "• " + x.Summary);
        var promises = state.Promises.Where(x => x.Status == "active").Select(x => "• " + x.Intent);
        return $"RUN: {run.DisplayName}\n\nPromessas\n{string.Join('\n', promises.DefaultIfEmpty("• Nenhuma promessa ativa."))}\n\nÚltimos acontecimentos\n{string.Join('\n', memories.DefaultIfEmpty("• Ainda não há acontecimentos registrados."))}";
    }
    internal void SwitchRun(string id)
    {
        if (game is null) return;
        var next = chronicle.Runs(game).FirstOrDefault(x => x.Id == id); if (next is null) return;
        EndSession(); run = chronicle.OpenRun(game, next.Id, next.DisplayName); session = Settings.AutoStartChronicle ? chronicle.StartSession(game, run) : null; RememberRun(); Balloon?.Invoke($"Run ativa: {run.DisplayName}."); Changed?.Invoke();
    }
    internal void CreateRun(string name)
    {
        if (game is null) return;
        EndSession(); run = chronicle.CreateRun(game, string.IsNullOrWhiteSpace(name) ? "Nova run" : name); session = Settings.AutoStartChronicle ? chronicle.StartSession(game, run) : null; RememberRun(); Balloon?.Invoke($"Nova run: {run.DisplayName}."); Changed?.Invoke();
    }
    internal void RenameRun(string name)
    {
        if (game is null || run is null || string.IsNullOrWhiteSpace(name)) return;
        run = chronicle.RenameRun(game, run, name); RememberRun(); Changed?.Invoke();
    }
    internal void OpenGameFolder() { if (game is not null) Process.Start(new ProcessStartInfo { FileName = chronicle.GameDirectory(game), UseShellExecute = true }); }
    internal void OpenManualFallback() => Balloon?.Invoke("Abra o Control Center para escolher a janela manualmente.");
    internal string RememberedCampaigns()
    {
        var lines = new List<string>();
        foreach (var rememberedGame in chronicle.Games().Take(6))
            foreach (var rememberedRun in chronicle.Runs(rememberedGame).OrderByDescending(x => x.LastOpenedAt).Take(2))
            {
                lines.Add($"[memória local] {rememberedGame.DisplayName} · {rememberedRun.DisplayName}");
                lines.AddRange(chronicle.State(rememberedGame, rememberedRun).Memories.TakeLast(3).Select(x => "- " + x.Summary));
            }
        return string.Join('\n', lines.Take(24));
    }
    private void RememberRun()
    {
        if (game is null || run is null) return;
        var copy = new System.Collections.Generic.Dictionary<string, string>(Settings.LastRunByGame, StringComparer.OrdinalIgnoreCase) { [game.Id] = run.Id };
        Settings = Settings with { LastRunByGame = copy }; updateSettings(Settings);
    }
    private void EndSession() { if (session is not null) { chronicle.EndSession(session); session = null; } }
    private static bool ShouldResearch(string question) => new[] { "quest", "missão", "consequ", "escolha", "lore", "personagem", "fação", "item", "achievement", "troféu", "missable", "boss", "fraqueza", "timer", "direção", "direções", "rota", "como chegar", "onde fica", "onde estou" }.Any(term => question.Contains(term, StringComparison.OrdinalIgnoreCase));
    private static bool AsksGameIdentity(string question)
    {
        var value = question.ToLowerInvariant();
        return (value.Contains("conhece") || value.Contains("qual") || value.Contains("nome")) && (value.Contains("jogo") || value.Contains("game") || value.Contains("título"));
    }
    public void Dispose() { detector.Stop(); askCancellation?.Cancel(); _ = capture.Stop(); EndSession(); provider.Dispose(); }
}
