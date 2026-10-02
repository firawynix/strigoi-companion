using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Strigoi.Companion.Core;

public enum ChronicleSource { observed, user_stated, externally_confirmed, inferred, unknown }
public enum SpoilerPolicy { blind, hint, light, full }

public sealed record ChronicleGame(string Id, string DisplayName, DateTimeOffset CreatedAt, DateTimeOffset LastOpenedAt);
public sealed record ChronicleRun(string Id, string DisplayName, DateTimeOffset CreatedAt, DateTimeOffset LastOpenedAt);
public sealed record ChronicleEvent(string Id, DateTimeOffset Timestamp, string Type, string Summary, string[] Entities,
    ChronicleSource Source, double Confidence, string SessionId, string[] SourceIds);
public sealed record ChroniclePromise(string Id, string? Entity, string Intent, string Status, ChronicleSource Source,
    double Confidence, DateTimeOffset CreatedAt, DateTimeOffset? ResolvedAt);
public sealed record KnowledgeSource(string Id, string Url, string Title, string Provider, DateTimeOffset RetrievedAt);
public sealed record KnowledgeClaim(string Id, string Subject, string Claim, ChronicleSource Source, double Confidence,
    DateTimeOffset RetrievedAt, string[] SourceIds, string? ConflictGroup = null);
public sealed record MemoryItem(string Id, string Kind, string Summary, string[] Entities, ChronicleSource Source,
    double Confidence, DateTimeOffset Timestamp, string[] SourceIds);
public sealed record RunState(string RunId, DateTimeOffset UpdatedAt, MemoryItem[] Memories, ChroniclePromise[] Promises)
{
    public static RunState Empty(string runId) => new(runId, DateTimeOffset.UtcNow, [], []);
}
public sealed record ChronicleSession(string Id, DateTimeOffset StartedAt, DateTimeOffset? EndedAt, string JournalPath);
public sealed record MemoryRetrieval(string Context, int CandidateCount, int ReturnedCount, int ContextCharacters, bool HasExternalKnowledge);
public sealed record ResearchRequest(string Game, string Query, string Context, SpoilerPolicy SpoilerPolicy);
public sealed record ResearchResult(KnowledgeSource[] Sources, KnowledgeClaim[] Claims);

public interface IWebResearchProvider
{
    Task<ResearchResult> ResearchGameQuestion(ResearchRequest request, CancellationToken cancellationToken);
}

// Local-first operational store. Markdown journals are human-readable mirrors;
// JSON and JSONL are the only inputs to retrieval and state updates.
public sealed class GameChronicleService
{
    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };
    private static readonly JsonSerializerOptions Jsonl = new(Json) { WriteIndented = false };
    private readonly string gamesRoot;

    public GameChronicleService(string profileDirectory)
    {
        gamesRoot = Path.Combine(profileDirectory, "games");
        Directory.CreateDirectory(gamesRoot);
    }

    public ChronicleGame OpenGame(string displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName)) throw new ArgumentException("Escolha ou informe o jogo da sessão.");
        var cleanedName = NormalizeGameDisplayName(displayName);
        var id = GameId(cleanedName);
        var existingDirectory = Directory.EnumerateDirectories(gamesRoot)
            .Select(directory => (Directory: directory, Game: ReadOrNull<ChronicleGame>(Path.Combine(directory, "game.json"))))
            .FirstOrDefault(item => item.Game is not null && string.Equals(NormalizeGameDisplayName(item.Game.DisplayName), cleanedName, StringComparison.OrdinalIgnoreCase)).Directory;
        var gameDirectory = existingDirectory ?? Path.Combine(gamesRoot, id);
        Directory.CreateDirectory(gameDirectory);
        var gameFile = Path.Combine(gameDirectory, "game.json");
        ChronicleGame game;
        if (TryRead(gameFile, out ChronicleGame? existing) && existing is not null)
            game = existing with { DisplayName = cleanedName, LastOpenedAt = DateTimeOffset.UtcNow };
        else
            game = new ChronicleGame(id, cleanedName, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
        WriteAtomic(gameFile, game); WriteGameMarkdown(gameDirectory, game);
        Directory.CreateDirectory(Path.Combine(gameDirectory, "runs"));
        return game;
    }

    public IReadOnlyList<ChronicleGame> Games() => Directory.EnumerateDirectories(gamesRoot)
        .Select(folder => ReadOrNull<ChronicleGame>(Path.Combine(folder, "game.json")))
        .Where(game => game is not null).Cast<ChronicleGame>().OrderByDescending(game => game.LastOpenedAt).ToArray();

    public IReadOnlyList<ChronicleRun> Runs(ChronicleGame game)
    {
        var root = RunsDirectory(game);
        if (!Directory.Exists(root)) return [];
        return Directory.EnumerateDirectories(root).Select(folder => ReadOrNull<ChronicleRun>(Path.Combine(folder, "run.json")))
            .Where(run => run is not null).Cast<ChronicleRun>().OrderBy(run => run.CreatedAt).ToArray();
    }

    // Every game's first run carries that game's name. Older generic defaults are
    // upgraded in place, keeping their Chronicle files and their stable id.
    public ChronicleRun OpenDefaultRun(ChronicleGame game)
    {
        var existing = Runs(game).FirstOrDefault(run => run.Id == "default");
        if (existing is not null && string.Equals(existing.DisplayName, "Run principal", StringComparison.OrdinalIgnoreCase))
            return RenameRun(game, existing, game.DisplayName);
        return existing is not null ? OpenRun(game, existing.Id, existing.DisplayName) : OpenRun(game, "default", game.DisplayName);
    }

    public ChronicleRun MostRecentlyUsedRun(ChronicleGame game)
    {
        var latest = Runs(game).OrderByDescending(run => run.LastOpenedAt).FirstOrDefault();
        if (latest is null) return OpenDefaultRun(game);
        return latest.Id == "default" && string.Equals(latest.DisplayName, "Run principal", StringComparison.OrdinalIgnoreCase)
            ? RenameRun(game, latest, game.DisplayName) : latest;
    }

    public ChronicleRun OpenRun(ChronicleGame game, string id, string displayName)
    {
        var safeId = RunId(id);
        var root = Path.Combine(RunsDirectory(game), safeId); Directory.CreateDirectory(root);
        foreach (var child in new[] { "sessions", "knowledge" }) Directory.CreateDirectory(Path.Combine(root, child));
        var file = Path.Combine(root, "run.json");
        ChronicleRun run;
        if (TryRead(file, out ChronicleRun? existing) && existing is not null)
            run = existing with { LastOpenedAt = DateTimeOffset.UtcNow };
        else
            run = new ChronicleRun(safeId, string.IsNullOrWhiteSpace(displayName) ? safeId : displayName.Trim(), DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
        WriteAtomic(file, run);
        if (!File.Exists(Path.Combine(root, "state.json"))) WriteAtomic(Path.Combine(root, "state.json"), RunState.Empty(run.Id));
        WriteRunMarkdown(root, run, State(game, run));
        return run;
    }

    public ChronicleRun CreateRun(ChronicleGame game, string displayName)
    {
        var seed = GameId(displayName); var id = seed; var count = 2;
        while (Directory.Exists(Path.Combine(RunsDirectory(game), id))) id = seed + "-" + count++;
        return OpenRun(game, id, displayName);
    }

    public ChronicleRun RenameRun(ChronicleGame game, ChronicleRun run, string displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName)) throw new ArgumentException("Informe um nome para a run.");
        var updated = run with { DisplayName = displayName.Trim(), LastOpenedAt = DateTimeOffset.UtcNow };
        WriteAtomic(Path.Combine(RunDirectory(game, run), "run.json"), updated); WriteRunMarkdown(RunDirectory(game, run), updated, State(game, updated));
        return updated;
    }

    public ChronicleSession StartSession(ChronicleGame game, ChronicleRun run)
    {
        var now = DateTimeOffset.Now; var id = now.ToString("yyyy-MM-dd_HH-mm-ss", CultureInfo.InvariantCulture) + "-" + Guid.NewGuid().ToString("N")[..8];
        var file = Path.Combine(RunDirectory(game, run), "sessions", id + ".md");
        File.WriteAllText(file, $"# Session — {now:yyyy-MM-dd HH:mm}\n\nGame: {game.DisplayName}\nRun: {run.DisplayName}\nStarted: {now:O}\n\n## Events\n", Encoding.UTF8);
        return new ChronicleSession(id, now, null, file);
    }

    public ChronicleSession EndSession(ChronicleSession session)
    {
        var ended = DateTimeOffset.Now;
        File.AppendAllText(session.JournalPath, $"\nEnded: {ended:O}\n", Encoding.UTF8);
        return session with { EndedAt = ended };
    }

    public ChronicleEvent RecordEvent(ChronicleGame game, ChronicleRun run, ChronicleSession? session, string type, string summary,
        ChronicleSource source, double confidence, IEnumerable<string>? entities = null, IEnumerable<string>? sourceIds = null)
    {
        if (string.IsNullOrWhiteSpace(type) || string.IsNullOrWhiteSpace(summary)) throw new ArgumentException("Evento precisa de tipo e resumo.");
        var item = new ChronicleEvent(Guid.NewGuid().ToString("N"), DateTimeOffset.Now, type.Trim(), summary.Trim(),
            Clean(entities), source, Math.Clamp(confidence, 0, 1), session?.Id ?? "", Clean(sourceIds));
        AppendJsonl(Path.Combine(RunDirectory(game, run), "events.jsonl"), item);
        if (session is not null) AppendJournal(session, item);
        if (source != ChronicleSource.unknown)
        {
            var state = State(game, run);
            var memory = new MemoryItem(item.Id, item.Type, item.Summary, item.Entities, item.Source, item.Confidence, item.Timestamp, item.SourceIds);
            var next = state with { UpdatedAt = DateTimeOffset.UtcNow, Memories = [.. state.Memories.Where(x => x.Id != memory.Id).Append(memory).TakeLast(256)] };
            SaveState(game, run, next);
        }
        return item;
    }

    public ChronicleEvent RecordDialogue(ChronicleGame game, ChronicleRun run, ChronicleSession? session, string summary, IEnumerable<string>? entities = null) =>
        RecordEvent(game, run, session, "dialogue", summary, ChronicleSource.observed, .9, entities);
    public ChronicleEvent RecordDecision(ChronicleGame game, ChronicleRun run, ChronicleSession? session, string summary, IEnumerable<string>? entities = null) =>
        RecordEvent(game, run, session, "dialogue_choice", summary, ChronicleSource.observed, .9, entities);
    public ChronicleEvent RecordQuestUpdate(ChronicleGame game, ChronicleRun run, ChronicleSession? session, string summary, IEnumerable<string>? entities = null) =>
        RecordEvent(game, run, session, "quest_update", summary, ChronicleSource.observed, .9, entities);
    public ChronicleEvent RecordPlayerIntent(ChronicleGame game, ChronicleRun run, ChronicleSession? session, string summary, IEnumerable<string>? entities = null) =>
        RecordEvent(game, run, session, "player_intent", summary, ChronicleSource.user_stated, 1, entities);

    public ChroniclePromise RecordPromise(ChronicleGame game, ChronicleRun run, ChronicleSession? session, string? entity, string intent)
    {
        if (string.IsNullOrWhiteSpace(intent)) throw new ArgumentException("Informe a intenção da promessa.");
        var promise = new ChroniclePromise(Guid.NewGuid().ToString("N"), string.IsNullOrWhiteSpace(entity) ? null : entity.Trim(), intent.Trim(), "active", ChronicleSource.user_stated, 1, DateTimeOffset.Now, null);
        AppendJsonl(Path.Combine(RunDirectory(game, run), "promises.jsonl"), promise);
        var state = State(game, run);
        SaveState(game, run, state with { UpdatedAt = DateTimeOffset.UtcNow, Promises = [.. state.Promises, promise] });
        RecordEvent(game, run, session, "promise", PromiseSummary(promise), ChronicleSource.user_stated, 1, entity is null ? null : [entity]);
        return promise;
    }

    public RunState State(ChronicleGame game, ChronicleRun run) => ReadOrNull<RunState>(Path.Combine(RunDirectory(game, run), "state.json")) ?? RunState.Empty(run.Id);

    public MemoryRetrieval Retrieve(ChronicleGame game, ChronicleRun run, string query, int maxItems = 8, int maxCharacters = 3500)
    {
        var state = State(game, run); var tokens = Terms(query);
        var candidates = state.Memories.Select(item => (Item: item, Score: Score(item.Summary + " " + string.Join(' ', item.Entities), tokens)))
            .Concat(state.Promises.Select(promise => (Item: new MemoryItem(promise.Id, "promise", PromiseSummary(promise), promise.Entity is null ? [] : [promise.Entity], promise.Source, promise.Confidence, promise.CreatedAt, []), Score: Score(PromiseSummary(promise), tokens) + 2)))
            .Concat(Knowledge(game, run).Select(claim => (Item: new MemoryItem(claim.Id, "external_knowledge", claim.Subject + ": " + claim.Claim, [claim.Subject], claim.Source, claim.Confidence, claim.RetrievedAt, claim.SourceIds), Score: Score(claim.Subject + " " + claim.Claim, tokens))));
        var ranked = candidates.Where(x => x.Score > 0).OrderByDescending(x => x.Score).ThenByDescending(x => x.Item.Timestamp).Take(maxItems).Select(x => x.Item).ToArray();
        var lines = new List<string>();
        foreach (var item in ranked)
        {
            var line = $"[{item.Source}] {item.Kind}: {item.Summary}";
            if (lines.Sum(x => x.Length + 1) + line.Length > maxCharacters) break;
            lines.Add(line);
        }
        return new MemoryRetrieval(string.Join('\n', lines), candidates.Count(), lines.Count, lines.Sum(x => x.Length + 1), ranked.Any(x => x.Source == ChronicleSource.externally_confirmed));
    }

    public async Task<(ResearchResult? Result, bool FromCache)> ResearchGameQuestion(ChronicleGame game, ChronicleRun run, string query,
        SpoilerPolicy policy, bool enabled, IWebResearchProvider? provider, CancellationToken cancellationToken)
    {
        var cached = Knowledge(game, run).Where(item => Score(item.Subject + " " + item.Claim, Terms(query)) > 0).ToArray();
        if (cached.Length > 0) return (new ResearchResult([], cached), true);
        if (!enabled || provider is null) return (null, false);
        var local = Retrieve(game, run, query, 4, 900);
        var result = await provider.ResearchGameQuestion(new ResearchRequest(game.DisplayName, query.Trim(), local.Context, policy), cancellationToken);
        if (result.Sources.Length == 0 || result.Claims.Length == 0) return (null, false);
        RecordExternalKnowledge(game, run, result);
        return (result, false);
    }

    public void RecordExternalKnowledge(ChronicleGame game, ChronicleRun run, ResearchResult result)
    {
        foreach (var source in result.Sources) AppendJsonl(Path.Combine(RunDirectory(game, run), "knowledge", "sources.jsonl"), source);
        foreach (var claim in result.Claims)
        {
            var external = claim with { Source = ChronicleSource.externally_confirmed, RetrievedAt = DateTimeOffset.UtcNow };
            AppendJsonl(Path.Combine(RunDirectory(game, run), "knowledge", "knowledge.jsonl"), external);
        }
    }

    public IReadOnlyList<KnowledgeClaim> Knowledge(ChronicleGame game, ChronicleRun run) => ReadJsonl<KnowledgeClaim>(Path.Combine(RunDirectory(game, run), "knowledge", "knowledge.jsonl"));
    public string GameDirectory(ChronicleGame game) => Path.Combine(gamesRoot, game.Id);

    private void SaveState(ChronicleGame game, ChronicleRun run, RunState state)
    {
        WriteAtomic(Path.Combine(RunDirectory(game, run), "state.json"), state);
        WriteRunMarkdown(RunDirectory(game, run), run, state);
    }
    private string RunsDirectory(ChronicleGame game) => Path.Combine(GameDirectory(game), "runs");
    private string RunDirectory(ChronicleGame game, ChronicleRun run) => Path.Combine(RunsDirectory(game), run.Id);
    private static string GameId(string value)
    {
        var normalized = value.Normalize(NormalizationForm.FormD); var builder = new StringBuilder(); var dash = false;
        foreach (var c in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark) continue;
            if (char.IsLetterOrDigit(c)) { builder.Append(char.ToLowerInvariant(c)); dash = false; }
            else if (!dash && builder.Length > 0) { builder.Append('-'); dash = true; }
        }
        return builder.ToString().Trim('-') is { Length: > 0 } id ? id[..Math.Min(id.Length, 80)] : "unknown-game";
    }
    private static string NormalizeGameDisplayName(string value)
    {
        var cleaned = Regex.Replace(value.Trim(), @"\s*\(\d{3,5}\s*[x×]\s*\d{3,5}\).*?$", "", RegexOptions.IgnoreCase);
        return string.IsNullOrWhiteSpace(cleaned) ? value.Trim() : cleaned.Trim();
    }
    private static string RunId(string value) => GameId(value);
    private static string[] Clean(IEnumerable<string>? values) => values?.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).Take(16).ToArray() ?? [];
    private static string PromiseSummary(ChroniclePromise promise) => $"{promise.Intent}" + (promise.Entity is null ? "" : $" — {promise.Entity}") + $" ({promise.Status})";
    private static HashSet<string> Terms(string text) => text.ToLowerInvariant().Split([ ' ', '\r', '\n', '\t', ',', '.', '?', '!', ':', ';', '(', ')', '[', ']' ], StringSplitOptions.RemoveEmptyEntries).Where(x => x.Length >= 3).ToHashSet();
    private static int Score(string text, HashSet<string> terms) => Terms(text).Count(terms.Contains);
    private static void AppendJournal(ChronicleSession session, ChronicleEvent item)
    {
        var entities = item.Entities.Length == 0 ? "" : $"\nEntities: {string.Join(", ", item.Entities)}";
        File.AppendAllText(session.JournalPath, $"\n### {item.Timestamp:HH:mm} — {item.Type}\n\n{item.Summary}\n\nSource: {item.Source}\nConfidence: {item.Confidence:F2}{entities}\n", Encoding.UTF8);
    }
    private static void WriteGameMarkdown(string directory, ChronicleGame game) => File.WriteAllText(Path.Combine(directory, "game.md"), $"# {game.DisplayName}\n\nGame id: `{game.Id}`\nCreated: {game.CreatedAt:O}\n", Encoding.UTF8);
    private static void WriteRunMarkdown(string directory, ChronicleRun run, RunState state)
    {
        var builder = new StringBuilder($"# Current Run — {run.DisplayName}\n\nUpdated: {state.UpdatedAt:O}\n\n## Promises\n");
        foreach (var promise in state.Promises.Where(x => x.Status == "active")) builder.Append("- ").Append(PromiseSummary(promise)).Append(" [").Append(promise.Source).Append("]\n");
        builder.Append("\n## Recent memories\n");
        foreach (var memory in state.Memories.TakeLast(30)) builder.Append("- ").Append(memory.Summary).Append(" [").Append(memory.Source).Append("]\n");
        WriteAtomicText(Path.Combine(directory, "run.md"), builder.ToString());
    }
    private static void AppendJsonl<T>(string path, T value)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var stream = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.Read);
        using var writer = new StreamWriter(stream, new UTF8Encoding(false)); writer.WriteLine(JsonSerializer.Serialize(value, Jsonl)); writer.Flush(); stream.Flush(true);
    }
    private static IReadOnlyList<T> ReadJsonl<T>(string path)
    {
        if (!File.Exists(path)) return [];
        var values = new List<T>();
        foreach (var line in File.ReadLines(path)) try { if (!string.IsNullOrWhiteSpace(line) && JsonSerializer.Deserialize<T>(line, Json) is { } value) values.Add(value); } catch (JsonException) { }
        return values;
    }
    private static bool TryRead<T>(string path, out T? value)
    {
        value = default;
        try { if (!File.Exists(path)) return false; value = JsonSerializer.Deserialize<T>(File.ReadAllText(path), Json); return value is not null; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException) { return false; }
    }
    private static T? ReadOrNull<T>(string path) => TryRead(path, out T? value) ? value : default;
    private static void WriteAtomic<T>(string path, T value) => WriteAtomicText(path, JsonSerializer.Serialize(value, Json));
    private static void WriteAtomicText(string path, string value)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!); var temp = path + ".tmp";
        File.WriteAllText(temp, value, new UTF8Encoding(false)); if (File.Exists(path)) File.Replace(temp, path, path + ".bak", true); else File.Move(temp, path);
    }
}
