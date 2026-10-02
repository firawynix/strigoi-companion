using Strigoi.Companion.Core;

int count = 0;
void Check(bool condition, string name)
{
    if (!condition) throw new Exception("FAILED: " + name);
    count++; Console.WriteLine("PASS: " + name);
}
var left = new WorkArea(-1920, -100, 1920, 1040);
var restored = Placement.Restore(left, 240, 288, .5, .75);
var saved = Placement.Save(left, 240, 288, restored.X, restored.Y);
Check(Math.Abs(saved.X - .5) < .001 && Math.Abs(saved.Y - .75) < .001, "negative monitor coordinates roundtrip");
Check(Placement.Restore(new(0, 0, 100, 100), 400, 400, 1, 1) == (0, 0), "oversized window clamps to origin");
Check(Placement.Save(left, 240, 288, 9000, -9000) == (1, 0), "offscreen saved position clamps");
var normalized = new CompanionSettings { Scale = double.NaN, X = double.PositiveInfinity, Y = -5, Hotkey = 0, Mode = (InteractionMode)123 }.Normalize();
Check(normalized.Scale == 1 && normalized.X == .85 && normalized.Y == 0 && normalized.Hotkey == 121 && normalized.Mode == InteractionMode.Interactive, "invalid settings recover");
Check(new CompanionSettings { Hotkey = 123 }.Normalize().Hotkey == 121, "F12 reserved by Windows migrates to F10");
Check(new CompanionSettings { Version = 1 }.Normalize().Version == 2 && new CompanionSettings().FollowGameEnabled && new CompanionSettings().ModelStartupMode == ModelStartupMode.AutomaticOnGameDetection, "familiar-first settings migrate with automatic follow default");
Check(new CompanionSettings { Version = 1, Mode = InteractionMode.Locked }.Normalize().Mode == InteractionMode.Interactive, "legacy locked default migrates to clickable Familiar");
Check(FamiliarAutomation.IsEligible(new ForegroundCandidate(42, "mygame", "My Game"), 1) &&
      FamiliarAutomation.IsEligible(new ForegroundCandidate(42, "Dawnwalker-Win64-Shipping", "The Blood of Dawnwalker"), 1) &&
      !FamiliarAutomation.IsEligible(new ForegroundCandidate(42, "Strigoi.Companion", "Strigoi Companion"), 1) &&
      !FamiliarAutomation.IsEligible(new ForegroundCandidate(42, "chrome", "A browser"), 1) &&
      !FamiliarAutomation.IsEligible(new ForegroundCandidate(42, "opera", "Opera browser"), 1) &&
      !FamiliarAutomation.IsEligible(new ForegroundCandidate(42, "word", "Documento"), 1) &&
      !FamiliarAutomation.IsEligible(new ForegroundCandidate(42, "mygame", "Small game", GameSized: false), 1) &&
      !FamiliarAutomation.IsEligible(new ForegroundCandidate(1, "mygame", "My Game"), 1), "foreground policy ignores companion and non-games");
Check(!VisionAnswerDisplay.WithoutGameGuesses("É um jogo chamado Outro Jogo.\nHá um corredor metálico.").Contains("Outro Jogo") &&
      VisionAnswerDisplay.WithoutGameGuesses("É um jogo chamado Outro Jogo.\nHá um corredor metálico.").Contains("corredor") &&
      VisionAnswerDisplay.WithoutGameGuesses("O jogo mostra um menu e uma porta.").Contains("menu"), "visual answer cannot replace confirmed game");
Check(FamiliarPersona.Default.Contains("não sabe") && FamiliarPersona.Default.Contains("proveniência") && FamiliarPersona.Default.Contains("spoilers"), "persona preserves unknown, provenance and spoiler constraints");
Check(WatchAssessmentParser.TryParse("{\"event\":\"notable_gameplay_event\",\"importance\":2,\"confidence\":0.8,\"summary\":\"Impacto visual relevante.\",\"shouldReact\":true,\"chronicleWorthiness\":2}", out var watch) && watch.Importance == 2 && !WatchAssessmentParser.TryParse("{\"event\":\"three_kills\"}", out _), "watch accepts only bounded structured events");
var chronicleDirectory = Path.Combine(Path.GetTempPath(), "strigoi-chronicle-" + Guid.NewGuid().ToString("N"));
try
{
    var chronicles = new GameChronicleService(chronicleDirectory);
    var game = chronicles.OpenGame("Chronicle Test™");
    Check(game.Id == chronicles.OpenGame("Chronicle Test").Id && File.Exists(Path.Combine(chronicles.GameDirectory(game), "game.json")), "game folder reuses normalized identity");
    Check(chronicles.Games().Any(x => x.Id == game.Id), "remembered games can be listed for Familiar chat");
    var mainRun = chronicles.OpenDefaultRun(game);
    Check(mainRun.DisplayName == game.DisplayName, "each game default run is named after its game");
    var technicalTitle = chronicles.OpenGame("Baldur's Gate 3 (3840x2160) - (DX11) - (6 + 6 WT)");
    Check(technicalTitle.DisplayName == "Baldur's Gate 3" && chronicles.OpenDefaultRun(technicalTitle).DisplayName == "Baldur's Gate 3", "technical window suffix does not become game or run name");
    var legacy = chronicles.OpenGame("Legacy Game");
    chronicles.OpenRun(legacy, "default", "Run principal");
    Check(chronicles.MostRecentlyUsedRun(legacy).DisplayName == "Legacy Game", "generic legacy default run migrates in place");
    var secondRun = chronicles.CreateRun(game, "Run alternativa");
    Check(chronicles.Runs(game).Count == 2, "multiple runs are created");
    var session = chronicles.StartSession(game, mainRun);
    chronicles.RecordDialogue(game, mainRun, session, "Conheceu Selene na base rebelde.", ["Selene"]);
    chronicles.RecordPlayerIntent(game, mainRun, session, "O jogador quer manter Selene viva.", ["Selene"]);
    var promise = chronicles.RecordPromise(game, mainRun, session, "Selene", "keep_alive");
    chronicles.EndSession(session);
    Check(File.Exists(session.JournalPath) && File.ReadAllText(session.JournalPath).Contains("Conheceu Selene"), "session journal records significant events");
    var eventLines = File.ReadAllLines(Path.Combine(chronicles.GameDirectory(game), "runs", mainRun.Id, "events.jsonl"));
    Check(eventLines.All(line => System.Text.Json.JsonDocument.Parse(line).RootElement.GetProperty("source").GetString() is not null), "events jsonl is valid and sourced");
    Check(chronicles.State(game, mainRun).Promises.Any(x => x.Id == promise.Id && x.Status == "active"), "promises persist separately");
    chronicles.RecordEvent(game, mainRun, null, "unknown_note", "Possível mistério sem confirmação.", ChronicleSource.unknown, .1, ["Artifact"]);
    Check(!chronicles.State(game, mainRun).Memories.Any(x => x.Summary.Contains("mistério")), "unknown never becomes consolidated fact");
    var retrieval = chronicles.Retrieve(game, mainRun, "Quem é Selene e quero ela viva?");
    Check(retrieval.Context.Contains("Selene") && retrieval.ReturnedCount <= 8 && retrieval.ContextCharacters <= 3500, "retrieval is relevant and bounded");
    Check(!chronicles.Retrieve(game, secondRun, "Selene").Context.Contains("Selene"), "runs remain isolated");
    var reopened = new GameChronicleService(chronicleDirectory).OpenGame("Chronicle Test");
    var reopenedRun = new GameChronicleService(chronicleDirectory).OpenDefaultRun(reopened);
    Check(new GameChronicleService(chronicleDirectory).Retrieve(reopened, reopenedRun, "Selene").Context.Contains("Selene"), "memory survives restart");
    var provider = new FakeResearchProvider();
    var disabled = chronicles.ResearchGameQuestion(game, mainRun, "Selene mission return", SpoilerPolicy.hint, false, provider, CancellationToken.None).GetAwaiter().GetResult();
    Check(disabled.Result is null && provider.Calls == 0, "web research respects disabled setting");
    var empty = chronicles.ResearchGameQuestion(game, mainRun, "Unknown route", SpoilerPolicy.hint, true, new EmptyResearchProvider(), CancellationToken.None).GetAwaiter().GetResult();
    Check(empty.Result is null && !empty.FromCache, "empty web response is not cached as knowledge");
    var researched = chronicles.ResearchGameQuestion(game, mainRun, "Selene mission return", SpoilerPolicy.hint, true, provider, CancellationToken.None).GetAwaiter().GetResult();
    Check(researched.Result is not null && !researched.FromCache && provider.Calls == 1 && provider.Last!.Context.Length <= 900 && provider.Last.SpoilerPolicy == SpoilerPolicy.hint, "web research receives minimized sourced context");
    var cached = chronicles.ResearchGameQuestion(game, mainRun, "Selene mission return", SpoilerPolicy.hint, true, provider, CancellationToken.None).GetAwaiter().GetResult();
    Check(cached.FromCache && provider.Calls == 1, "knowledge cache prevents duplicate research");
    Check(chronicles.Knowledge(game, mainRun).All(x => x.Source == ChronicleSource.externally_confirmed), "external knowledge preserves provenance");
    var conflicts = new ResearchResult([], [
        new KnowledgeClaim("a", "Selene", "Returns after mission.", ChronicleSource.inferred, .8, DateTimeOffset.UtcNow, ["source-a"], "selene-return"),
        new KnowledgeClaim("b", "Selene", "Does not return after mission.", ChronicleSource.inferred, .7, DateTimeOffset.UtcNow, ["source-b"], "selene-return")]);
    chronicles.RecordExternalKnowledge(game, mainRun, conflicts);
    Check(chronicles.Knowledge(game, mainRun).Count(x => x.ConflictGroup == "selene-return") == 2, "conflicting external claims are retained");
}
finally { if (Directory.Exists(chronicleDirectory)) Directory.Delete(chronicleDirectory, true); }
var directory = Path.Combine(Path.GetTempPath(), "strigoi-checks-" + Guid.NewGuid().ToString("N"));
try
{
    var store = new SettingsStore(directory);
    Check(store.Load().Scale == 1, "first-run defaults");
    var preferences = new CompanionSettings { Scale = 1.5, X = .2, Monitor = "DISPLAY2", Onboarded = true };
    store.Save(preferences); var loaded = store.Load(); Check(loaded.Scale == preferences.Scale && loaded.X == preferences.X && loaded.Monitor == preferences.Monitor && loaded.Onboarded == preferences.Onboarded && loaded.Version == 2, "persisted settings roundtrip");
    store.Save(preferences with { Scale = 2 });
    Check(File.Exists(Path.Combine(directory, "settings.json.bak")), "atomic replacement retains backup");
    File.WriteAllText(Path.Combine(directory, "settings.json"), "broken {");
    Check(store.Load().Scale == 1 && store.Warning is not null, "corrupt JSON recovers without crash");
    File.WriteAllText(Path.Combine(directory, "settings.json"), "{\"Version\":999}");
    Check(store.Load().Scale == 1, "unknown schema recovers");
}
finally { Directory.Delete(directory, true); }
var clip = new Clip(0, new[] { 0, 1, 2 }, 100, false, null);
Check(AnimationFrame.At(clip, 900, false) == 2, "one-shot stays on final frame");
Check(AnimationFrame.At(clip with { Loop = true }, 400, false) == 1, "loop wraps");
Check(AnimationFrame.At(clip, 900, true) == 0, "reduced motion fixes pose");
if (args.Length == 1)
{
    var manifest = FamiliarManifest.Load(args[0]);
    Check(manifest.Clips.Count == 9, "all phase-one states have clips");
    int[] used = [7,8,8,4,5,8,6,6,6,8,8];
    Check(manifest.Clips.Values.All(c => c.Frames.All(f => f < used[c.Row])), "clips never address unused cells");
    Check(manifest.Clips["warning"].Fallback is not null && manifest.Clips["sleep"].Fallback is not null, "missing artwork explicitly declared");
}
Console.WriteLine($"{count} checks passed.");

RuntimeSample Sample(double seconds, double cpu = 0, long? privateWorkingSet = 100, int activations = 0) => new(seconds, cpu, 200, 150, privateWorkingSet, 30, activations, 10);
Check(Stability.Summarize([]).Status == "insufficient-data", "no samples never passes stability");
var shortRun = Stability.Summarize([Sample(0), Sample(5, 2), Sample(15, 4)]);
Check(shortRun.Status == "short-sample" && !shortRun.MeetsThirtyMinuteDuration, "short run never claims thirty minute gate");
Check(Math.Abs(shortRun.MeanCpuPercent!.Value - 50d / 15) < .0001, "CPU average is weighted by sample interval");
Check(Stability.Summarize([Sample(0), Sample(5, privateWorkingSet: null)]).PeakPrivateWorkingSetBytes is null, "missing memory reading remains unavailable");
var complete = Enumerable.Range(0, 361).Select(i => Sample(i * 5)).ToArray();
Check(Stability.Summarize(complete).Status == "duration-complete", "continuous thirty minute series recognized");
complete[^1] = Sample(1800, activations: 1);
Check(Stability.Summarize(complete).Status == "duration-complete" && Stability.Summarize(complete).ObservedActivations == 1, "activation is observed without assuming focus disruption");
Check(Stability.Summarize([Sample(0), Sample(1800)]).Status == "needs-review", "large sampling gap is not a clean run");
var history = new VisualChanges();
byte[] dark = new byte[16], light = Enumerable.Repeat((byte)255, 16).ToArray();
Check(!history.Add(dark, 2, 2, TimeSpan.Zero).Changed, "first frame has no comparison");
Check(!history.Add(dark, 2, 2, TimeSpan.FromSeconds(1)).Changed, "identical images are stable");
Check(history.Add(light, 2, 2, TimeSpan.FromSeconds(2)).Changed, "large visual difference emits event");
Check(!history.Add(dark, 2, 2, TimeSpan.FromSeconds(2.5)).Changed, "events have cooldown");
for (int i = 0; i < 100; i++) history.Add(dark, 2, 2, TimeSpan.FromSeconds(i + 4));
Check(history.Count == 8 && history.Bytes == 128, "ring evicts old memory");
Check(!history.Add(new byte[32], 4, 2, TimeSpan.FromSeconds(110)).Changed, "resize resets comparison");
var snapshot = history.Snapshot();
Check(snapshot[^1].Width == 4 && snapshot[^1].Height == 2 && snapshot[^1].Time == TimeSpan.FromSeconds(110), "replay retains dimensions and timestamps across resize");
snapshot[^1].Pixels[0] = 200;
Check(history.Snapshot()[^1].Pixels[0] == 0, "replay cannot mutate live history");
history.Clear();
Check(snapshot.Length == 8, "frozen replay survives ring updates until owner releases it");
Check(history.Count == 0 && history.Bytes == 0 && history.Events == 0, "stop clears image history and events");
Console.WriteLine($"TOTAL: {count} checks passed.");

sealed class FakeResearchProvider : IWebResearchProvider
{
    public int Calls { get; private set; }
    public ResearchRequest? Last { get; private set; }
    public Task<ResearchResult> ResearchGameQuestion(ResearchRequest request, CancellationToken cancellationToken)
    {
        Calls++; Last = request;
        return Task.FromResult(new ResearchResult(
            [new KnowledgeSource("source-1", "https://example.invalid/selene", "Selene guide", "test", DateTimeOffset.UtcNow)],
            [new KnowledgeClaim("claim-1", "Selene", "Selene can return after the mission.", ChronicleSource.externally_confirmed, .8, DateTimeOffset.UtcNow, ["source-1"])]));
    }
}

sealed class EmptyResearchProvider : IWebResearchProvider
{
    public Task<ResearchResult> ResearchGameQuestion(ResearchRequest request, CancellationToken cancellationToken) => Task.FromResult(new ResearchResult([], []));
}
