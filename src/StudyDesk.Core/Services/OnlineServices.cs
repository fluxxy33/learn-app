using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace StudyDesk.Core.Services;

public sealed record OnlineImage(string Title, string ThumbUrl, string Url, string Author, string License, string PageUrl)
{
    public string Credit => $"{CleanTitle} — {Author}, {License}, via Wikimedia Commons";
    public string CleanTitle => Regex.Replace(Path.GetFileNameWithoutExtension(Title.Replace("File:", "").Replace("Fichier:", "")), "[_]", " ");
}

public sealed record WikiSummary(string Title, string Extract, string? ThumbnailUrl, string PageUrl);

/// <summary>
/// Fonctions en ligne facultatives : images libres de Wikimedia Commons et résumés Wikipédia.
/// Aucune donnée personnelle n'est envoyée, seulement le texte recherché.
/// </summary>
public sealed class OnlineServices : IDisposable
{
    private readonly HttpClient _http;

    public OnlineServices(HttpMessageHandler? handler = null)
    {
        _http = handler == null ? new HttpClient() : new HttpClient(handler);
        _http.Timeout = TimeSpan.FromSeconds(20);
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("StudyDesk/1.0 (application d'apprentissage hors ligne)");
    }

    public async Task<List<OnlineImage>> SearchImagesAsync(string query, CancellationToken ct = default)
    {
        var url = "https://commons.wikimedia.org/w/api.php?action=query&format=json&generator=search&gsrnamespace=6&gsrlimit=30" +
                  "&prop=imageinfo&iiprop=url|extmetadata|mime&iiurlwidth=320&gsrsearch=" +
                  Uri.EscapeDataString(query + " filetype:bitmap");
        using var doc = JsonDocument.Parse(await _http.GetStringAsync(url, ct));
        var list = new List<OnlineImage>();
        if (!doc.RootElement.TryGetProperty("query", out var q) || !q.TryGetProperty("pages", out var pages)) return list;
        foreach (var page in pages.EnumerateObject().Select(p => p.Value)
                     .OrderBy(p => p.TryGetProperty("index", out var i) ? i.GetInt32() : 999))
        {
            if (!page.TryGetProperty("imageinfo", out var infos) || infos.GetArrayLength() == 0) continue;
            var info = infos[0];
            var mime = info.TryGetProperty("mime", out var m) ? m.GetString() ?? "" : "";
            if (!mime.StartsWith("image/") || mime.Contains("svg") || mime.Contains("tiff")) continue;
            string Meta(string key)
            {
                if (info.TryGetProperty("extmetadata", out var em) && em.TryGetProperty(key, out var v) && v.TryGetProperty("value", out var val))
                    return Regex.Replace(val.GetString() ?? "", "<.*?>", "").Trim();
                return "";
            }
            list.Add(new OnlineImage(
                page.GetProperty("title").GetString() ?? "",
                info.TryGetProperty("thumburl", out var t) ? t.GetString() ?? "" : info.GetProperty("url").GetString() ?? "",
                info.GetProperty("url").GetString() ?? "",
                Meta("Artist") is { Length: > 0 } a ? (a.Length > 80 ? a[..80] + "…" : a) : "Auteur inconnu",
                Meta("LicenseShortName") is { Length: > 0 } l ? l : "voir la page",
                info.TryGetProperty("descriptionurl", out var d) ? d.GetString() ?? "" : ""));
        }
        return list;
    }

    public Task<byte[]> DownloadAsync(string url, CancellationToken ct = default) => _http.GetByteArrayAsync(url, ct);

    public async Task<List<string>> SearchWikipediaAsync(string query, CancellationToken ct = default)
    {
        var url = "https://fr.wikipedia.org/w/api.php?action=opensearch&format=json&limit=10&namespace=0&search=" + Uri.EscapeDataString(query);
        using var doc = JsonDocument.Parse(await _http.GetStringAsync(url, ct));
        return doc.RootElement[1].EnumerateArray().Select(e => e.GetString() ?? "").Where(s => s.Length > 0).ToList();
    }

    public async Task<WikiSummary?> GetWikipediaSummaryAsync(string title, CancellationToken ct = default)
    {
        var url = "https://fr.wikipedia.org/api/rest_v1/page/summary/" + Uri.EscapeDataString(title.Replace(' ', '_'));
        var res = await _http.GetAsync(url, ct);
        if (!res.IsSuccessStatusCode) return null;
        var json = await res.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
        string? thumb = json.TryGetProperty("thumbnail", out var th) && th.TryGetProperty("source", out var src) ? src.GetString() : null;
        string page = json.TryGetProperty("content_urls", out var cu) && cu.TryGetProperty("desktop", out var dk) && dk.TryGetProperty("page", out var pg)
            ? pg.GetString() ?? "" : "https://fr.wikipedia.org/wiki/" + Uri.EscapeDataString(title);
        return new WikiSummary(
            json.TryGetProperty("title", out var t) ? t.GetString() ?? title : title,
            json.TryGetProperty("extract", out var e) ? e.GetString() ?? "" : "",
            thumb, page);
    }

    public void Dispose() => _http.Dispose();
}
