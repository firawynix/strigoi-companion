using System;
using System.Net.Http;
using System.Net;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Strigoi.Companion.Core;

namespace Strigoi.Companion;

// This provider receives only game + question. It never receives pixels, journals,
// player identity, or the local memory package passed to the VLM.
internal sealed partial class DuckDuckGoResearchProvider : IWebResearchProvider, IDisposable
{
    private readonly bool workMode;
    internal DuckDuckGoResearchProvider(bool workMode = false) => this.workMode = workMode;
    private readonly HttpClient client = new() { BaseAddress = new Uri("https://api.duckduckgo.com/"), Timeout = TimeSpan.FromSeconds(12) };

    public async Task<ResearchResult> ResearchGameQuestion(ResearchRequest request, CancellationToken cancellationToken)
    {
        if (!workMode && request.SpoilerPolicy == SpoilerPolicy.blind) return new ResearchResult([], []);
        var suffix = workMode ? "" : request.SpoilerPolicy switch { SpoilerPolicy.hint => " spoiler free hint", SpoilerPolicy.light => " guide", _ => " full consequences" };
        var game = CleanGameName(request.Game);
        var searchTerms = (game + " " + request.Query + suffix);
        var query = Uri.EscapeDataString(searchTerms[..Math.Min(500, searchTerms.Length)]);
        using var response = await client.GetAsync("?q=" + query + "&format=json&no_html=1&skip_disambig=1", cancellationToken);
        if (response.IsSuccessStatusCode)
        {
            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
            var root = document.RootElement;
            var text = root.TryGetProperty("AbstractText", out var abstractText) ? abstractText.GetString()?.Trim() : null;
            var url = root.TryGetProperty("AbstractURL", out var abstractUrl) ? abstractUrl.GetString()?.Trim() : null;
            var title = root.TryGetProperty("Heading", out var heading) ? heading.GetString()?.Trim() : null;
            if (!string.IsNullOrWhiteSpace(text) && !string.IsNullOrWhiteSpace(url) && IsWebUri(url, out var sourceUri))
            {
                var source = new KnowledgeSource(Guid.NewGuid().ToString("N"), sourceUri.AbsoluteUri, string.IsNullOrWhiteSpace(title) ? game : title, "duckduckgo-instant-answer", DateTimeOffset.UtcNow);
                var claim = new KnowledgeClaim(Guid.NewGuid().ToString("N"), game, text[..Math.Min(2400, text.Length)], ChronicleSource.externally_confirmed, .65, DateTimeOffset.UtcNow, [source.Id]);
                return new ResearchResult([source], [claim]);
            }
        }

        // The instant-answer endpoint is often empty for quests. Fall back to the
        // public result snippets; only game + question are still sent.
        using var page = await client.GetAsync(new Uri("https://html.duckduckgo.com/html/?q=" + query), cancellationToken);
        if (!page.IsSuccessStatusCode) return new ResearchResult([], []);
        return ParseResultSnippets(game, await page.Content.ReadAsStringAsync(cancellationToken));
    }

    private static ResearchResult ParseResultSnippets(string game, string html)
    {
        var sources = new List<KnowledgeSource>();
        var claims = new List<KnowledgeClaim>();
        foreach (Match result in ResultRegex().Matches(html))
        {
            var title = Plain(result.Groups["title"].Value);
            var snippet = Plain(result.Groups["snippet"].Value);
            var href = WebUtility.HtmlDecode(result.Groups["url"].Value).Trim();
            if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(snippet) || !TryResultUri(href, out var uri)) continue;
            var source = new KnowledgeSource(Guid.NewGuid().ToString("N"), uri.AbsoluteUri, title, "duckduckgo-search-snippet", DateTimeOffset.UtcNow);
            sources.Add(source);
            claims.Add(new KnowledgeClaim(Guid.NewGuid().ToString("N"), game,
                $"Resultado de pesquisa — {title}: {snippet[..Math.Min(700, snippet.Length)]}", ChronicleSource.externally_confirmed, .45, DateTimeOffset.UtcNow, [source.Id]));
            if (claims.Count == 3) break;
        }
        return new ResearchResult(sources.ToArray(), claims.ToArray());
    }

    private static bool TryResultUri(string value, out Uri uri)
    {
        uri = null!;
        if (value.StartsWith("//", StringComparison.Ordinal)) value = "https:" + value;
        if (!Uri.TryCreate(value, UriKind.Absolute, out var candidate)) return false;
        if (candidate.Host.Equals("duckduckgo.com", StringComparison.OrdinalIgnoreCase) && candidate.AbsolutePath.StartsWith("/l/", StringComparison.OrdinalIgnoreCase))
        {
            var destination = candidate.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries)
                .Select(part => part.Split('=', 2)).FirstOrDefault(pair => pair.Length == 2 && pair[0].Equals("uddg", StringComparison.OrdinalIgnoreCase))?[1];
            destination = string.IsNullOrWhiteSpace(destination) ? null : Uri.UnescapeDataString(destination.Replace('+', ' '));
            if (!string.IsNullOrWhiteSpace(destination) && Uri.TryCreate(destination, UriKind.Absolute, out var decoded)) candidate = decoded;
        }
        if (candidate.Scheme is not ("http" or "https")) return false;
        uri = candidate; return true;
    }
    private static bool IsWebUri(string value, out Uri uri) => Uri.TryCreate(value, UriKind.Absolute, out uri!) && uri.Scheme is "http" or "https";
    private static string Plain(string html) => Regex.Replace(WebUtility.HtmlDecode(Regex.Replace(html, "<.*?>", " ")), @"\s+", " ").Trim();
    private static string CleanGameName(string name) => Regex.Replace(name, @"\s*\(\d{3,5}x\d{3,5}\).*?$", "").Trim();
    [GeneratedRegex("result__a[^>]*href=\\\"(?<url>[^\\\"]+)\\\"[^>]*>(?<title>.*?)</a>.*?result__snippet[^>]*>(?<snippet>.*?)</", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex ResultRegex();

    public void Dispose() => client.Dispose();
}
