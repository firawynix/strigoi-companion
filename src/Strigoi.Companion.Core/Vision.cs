using System.Text.RegularExpressions;

namespace Strigoi.Companion.Core;

public sealed record VisionRuntime(string Endpoint, string Model)
{
    public const string DefaultEndpoint = "http://127.0.0.1:11434";
    public const string DefaultModel = "qwen3.5:2b";

    public static VisionRuntime Default { get; } = new(DefaultEndpoint, DefaultModel);

    public VisionRuntime Normalize()
    {
        var endpoint = string.IsNullOrWhiteSpace(Endpoint) ? DefaultEndpoint : Endpoint.Trim();
        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var parsed) || parsed.Scheme != Uri.UriSchemeHttp ||
            (parsed.Host != "127.0.0.1" && parsed.Host != "localhost" && parsed.Host != "::1") ||
            !string.IsNullOrEmpty(parsed.UserInfo) || parsed.AbsolutePath is not "/" and not "") endpoint = DefaultEndpoint;
        return this with { Endpoint = endpoint, Model = string.IsNullOrWhiteSpace(Model) ? DefaultModel : Model.Trim() };
    }
}

public sealed record VisionImage(byte[] Pixels, int Width, int Height)
{
    public VisionImage Copy() => this with { Pixels = (byte[])Pixels.Clone() };
}

public sealed record VisionAnswer(string Text, TimeSpan Elapsed, string Model);
public sealed record WatchAssessment(string Event, int Importance, double Confidence, string Summary, bool ShouldReact, int ChronicleWorthiness);

public static class WatchAssessmentParser
{
    private static readonly string[] Events = ["possible_player_death", "dialogue_detected", "achievement_or_reward", "combat_high_activity", "possible_victory", "notable_gameplay_event", "work_progress", "work_notification", "work_error", "unknown"];
    public static bool TryParse(string text, out WatchAssessment assessment)
    {
        assessment = null!;
        try
        {
            var value = text.Trim().Trim('`').Replace("json\n", "", StringComparison.OrdinalIgnoreCase);
            using var doc = System.Text.Json.JsonDocument.Parse(value); var root = doc.RootElement;
            var eventName = root.GetProperty("event").GetString() ?? "";
            var summary = root.GetProperty("summary").GetString()?.Trim() ?? "";
            var importance = root.GetProperty("importance").GetInt32();
            var confidence = root.GetProperty("confidence").GetDouble();
            var react = root.GetProperty("shouldReact").GetBoolean(); var worthiness = root.GetProperty("chronicleWorthiness").GetInt32();
            if (!Events.Contains(eventName, StringComparer.Ordinal) || string.IsNullOrWhiteSpace(summary) || importance is < 0 or > 3 || worthiness is < 0 or > 3 || confidence is < 0 or > 1) return false;
            assessment = new(eventName, importance, confidence, summary[..Math.Min(500, summary.Length)], react, worthiness); return true;
        }
        catch (System.Text.Json.JsonException) { return false; }
        catch (KeyNotFoundException) { return false; }
    }
}

// Presentation only. Callers keep provenance, spoiler policy and all GameSense
// decisions outside this text so the persona can never override evidence.
public static class FamiliarPersona
{
    public const string Default = "Você é o Familiar do Strigoi Companion: um pequeno vampiro gótico, inteligente, teatral, espirituoso e levemente sarcástico que acompanha a aventura do jogador. Fale no idioma do jogador. Use humor naturalmente, não em toda frase; em momentos sérios, seja sério. Seja genuinamente empolgado com feitos épicos. Evite caricatura e nunca use a persona para mudar, esconder ou inventar fatos, confiança, recomendações, proveniência, memória, spoilers ou decisões. Quando algo não for conhecido, admita claramente que não sabe. Fatos e fontes sempre vencem o roleplay. Respostas normalmente curtas.";
}

public static partial class VisionAnswerDisplay
{
    private static readonly Regex GameIdentity = GameIdentityRegex();

    // Game identity belongs to the capture session selected by the player, not to
    // a visual model that can confidently hallucinate a familiar title.
    public static string WithoutGameGuesses(string text)
    {
        var kept = text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Where(line => !GameIdentity.IsMatch(line.Trim())).ToArray();
        return kept.Length == 0
            ? "A descrição visual foi descartada porque tentou identificar o jogo. Use o nome confirmado acima."
            : string.Join(Environment.NewLine, kept);
    }

    [GeneratedRegex(@"\b(jogo|game)\s+(chamado|called|intitulado)|\b(nome|título)\s+(do\s+)?(jogo|game)\b|\bé\s+(o|um)\s+(jogo|game)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex GameIdentityRegex();
}
