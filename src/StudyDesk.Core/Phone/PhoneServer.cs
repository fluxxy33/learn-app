using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using StudyDesk.Core.Models;
using StudyDesk.Core.Services;

namespace StudyDesk.Core.Phone;

/// <summary>
/// Petit serveur HTTP local (Wi-Fi) qui sert l'app de révision au téléphone.
/// Utilise TcpListener : pas besoin de droits administrateur. Accès protégé par un jeton présent dans le QR code.
/// </summary>
public sealed class PhoneServer : IDisposable
{
    private readonly ProfileStore _store;
    private readonly Func<Func<object?>, Task<object?>> _invoke;
    private TcpListener? _listener;
    private CancellationTokenSource? _cts;

    public int Port { get; private set; }
    public string Token { get; }
    public bool IsRunning => _listener != null;
    public string? Address { get; private set; }
    public string? Url => Address == null ? null : $"http://{Address}:{Port}/?t={Token}";
    public DateTime? LastSync { get; private set; }

    /// <summary>Déclenché après une synchronisation (nombre de révisions, nombre de quiz).</summary>
    public event Action<int, int>? Synced;
    public event Action<string>? Log;

    /// <param name="invoke">Exécute une fonction sur le fil de l'interface (accès aux données en toute sécurité).</param>
    public PhoneServer(ProfileStore store, Func<Func<object?>, Task<object?>>? invoke = null, string? token = null)
    {
        _store = store;
        _invoke = invoke ?? (f => Task.FromResult(f()));
        Token = token ?? Convert.ToHexString(RandomNumberGenerator.GetBytes(6)).ToLowerInvariant();
    }

    public void Start(int port, string? address = null)
    {
        Stop();
        Address = address ?? NetworkHelper.GetLocalAddresses().FirstOrDefault() ?? "127.0.0.1";
        _listener = new TcpListener(IPAddress.Any, port);
        _listener.Start();
        Port = ((IPEndPoint)_listener.LocalEndpoint).Port;
        _cts = new CancellationTokenSource();
        _ = AcceptLoop(_listener, _cts.Token);
    }

    public void Stop()
    {
        _cts?.Cancel();
        try { _listener?.Stop(); } catch { /* déjà arrêté */ }
        _listener = null;
    }

    public void Dispose() => Stop();

    private async Task AcceptLoop(TcpListener listener, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            TcpClient client;
            try { client = await listener.AcceptTcpClientAsync(ct); }
            catch { break; }
            _ = Task.Run(() => Handle(client, ct), ct);
        }
    }

    private async Task Handle(TcpClient client, CancellationToken ct)
    {
        using (client)
        {
            try
            {
                client.ReceiveTimeout = 10000;
                var stream = client.GetStream();
                var req = await HttpRequest.ReadAsync(stream, ct);
                if (req == null) return;
                HttpResponse res;
                try { res = await Route(req); }
                catch (Exception ex)
                {
                    Log?.Invoke(ex.ToString());
                    res = HttpResponse.Text(500, "Erreur : " + ex.Message);
                }
                await res.WriteAsync(stream, ct);
            }
            catch (Exception ex)
            {
                Log?.Invoke(ex.Message);
            }
        }
    }

    private async Task<HttpResponse> Route(HttpRequest req)
    {
        if (req.Method == "OPTIONS") return HttpResponse.Text(204, "");
        var path = req.Path;
        if (path == "/favicon.ico") return HttpResponse.Text(404, "");
        if (req.Query.GetValueOrDefault("t") != Token)
            return HttpResponse.Html(403, "<h1>Accès refusé</h1><p>Scanne à nouveau le QR code affiché dans StudyDesk.</p>");

        switch (path)
        {
            case "/":
            case "/index.html":
                return HttpResponse.Html(200, WebApp.Build(new { mode = "live", token = Token }));

            case "/api/data":
                var json = (string)(await _invoke(() => PhonePayload.BuildJson(_store, DateTime.Now)))!;
                return HttpResponse.Json(200, json);

            case "/api/sync" when req.Method == "POST":
                var sync = JsonSerializer.Deserialize<SyncRequest>(req.Body, Json.Web) ?? new SyncRequest();
                var result = (string)(await _invoke(() =>
                {
                    var (r, q) = PhoneSync.Apply(_store, sync);
                    LastSync = DateTime.Now;
                    Synced?.Invoke(r, q);
                    return JsonSerializer.Serialize(new { ok = true, reviews = r, quizzes = q, data = PhonePayload.Build(_store, DateTime.Now) }, Json.Web);
                }))!;
                return HttpResponse.Json(200, result);

            case "/offline.html":
                var html = (string)(await _invoke(() => WebApp.BuildOffline(_store, $"http://{Address}:{Port}", Token)))!;
                var r2 = HttpResponse.Html(200, html);
                r2.Headers["Content-Disposition"] = $"attachment; filename=\"StudyDesk-{Sanitize(_store.Profile.Name)}.html\"";
                return r2;
        }

        if (path.StartsWith("/api/doc/"))
        {
            var id = path["/api/doc/".Length..];
            var html = (string?)await _invoke(() =>
                _store.GetItem(id) is { } item ? PhonePayload.ReadableHtml(_store, item, m => $"/media/{Uri.EscapeDataString(m)}?t={Token}") : null);
            return html == null ? HttpResponse.Text(404, "Introuvable") : HttpResponse.Html(200, html);
        }

        if (path.StartsWith("/media/"))
        {
            var name = Path.GetFileName(Uri.UnescapeDataString(path["/media/".Length..]));
            var file = _store.MediaPath(name);
            if (!File.Exists(file)) return HttpResponse.Text(404, "Introuvable");
            var res = new HttpResponse(200, MimeType(name), await File.ReadAllBytesAsync(file));
            res.Headers["Cache-Control"] = "max-age=86400";
            return res;
        }

        return HttpResponse.Text(404, "Introuvable");
    }

    public static string Sanitize(string s) => string.Concat(s.Select(c => char.IsLetterOrDigit(c) ? c : '-'));

    public static string MimeType(string name) => Path.GetExtension(name).ToLowerInvariant() switch
    {
        ".png" => "image/png",
        ".jpg" or ".jpeg" => "image/jpeg",
        ".gif" => "image/gif",
        ".webp" => "image/webp",
        ".bmp" => "image/bmp",
        _ => "application/octet-stream"
    };
}

/// <summary>Construit la page de l'app téléphone à partir du modèle embarqué.</summary>
public static class WebApp
{
    private static string? _template;

    public static string Template
    {
        get
        {
            if (_template != null) return _template;
            using var s = Assembly.GetExecutingAssembly().GetManifestResourceStream("StudyDesk.Core.Web.phone.html")!;
            using var r = new StreamReader(s);
            return _template = r.ReadToEnd();
        }
    }

    public static string Build(object boot) =>
        Template.Replace("/*__BOOT__*/", "window.SD_BOOT = " + JsonSerializer.Serialize(boot, Json.Web).Replace("</", "<\\/") + ";");

    /// <summary>Fichier HTML autonome : données, cours et images inclus. Fonctionne sans connexion.</summary>
    public static string BuildOffline(ProfileStore store, string? serverUrl, string? token)
    {
        var media = new Dictionary<string, string>();
        string MediaData(string name)
        {
            if (media.ContainsKey(name)) return "sdmedia:" + name;
            var path = store.MediaPath(name);
            if (File.Exists(path))
                media[name] = $"data:{PhoneServer.MimeType(name)};base64," + Convert.ToBase64String(File.ReadAllBytes(path));
            return "sdmedia:" + name;
        }

        foreach (var item in store.ItemsOf(ItemKind.Deck).Concat(store.ItemsOf(ItemKind.Quiz)))
            foreach (var m in store.MediaReferences(item)) MediaData(m);

        var docs = new Dictionary<string, string>();
        foreach (var item in store.Library.Items.Where(i => i.Kind is ItemKind.Document or ItemKind.Slideshow))
        {
            var html = PhonePayload.ReadableHtml(store, item, MediaData);
            if (html != null) docs[item.Id] = html;
        }

        return Build(new
        {
            mode = "offline",
            token,
            server = serverUrl,
            data = PhonePayload.Build(store, DateTime.Now),
            docs,
            media
        });
    }
}

public static class NetworkHelper
{
    /// <summary>Adresses IPv4 locales utilisables sur le réseau Wi-Fi / Ethernet, les plus probables en premier.</summary>
    public static List<string> GetLocalAddresses()
    {
        var list = new List<(string Ip, int Score)>();
        try
        {
            foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (ni.OperationalStatus != OperationalStatus.Up) continue;
                if (ni.NetworkInterfaceType is NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel) continue;
                var props = ni.GetIPProperties();
                bool hasGateway = props.GatewayAddresses.Any(g => g.Address.AddressFamily == AddressFamily.InterNetwork && !g.Address.Equals(IPAddress.Any));
                var name = (ni.Name + " " + ni.Description).ToLowerInvariant();
                bool virtualAdapter = name.Contains("virtual") || name.Contains("vmware") || name.Contains("hyper-v") || name.Contains("vbox") || name.Contains("wsl") || name.Contains("docker");
                foreach (var ua in props.UnicastAddresses)
                {
                    if (ua.Address.AddressFamily != AddressFamily.InterNetwork) continue;
                    var ip = ua.Address.ToString();
                    if (ip.StartsWith("169.254.") || ip.StartsWith("127.")) continue;
                    int score = 0;
                    if (hasGateway) score += 10;
                    if (!virtualAdapter) score += 5;
                    if (ni.NetworkInterfaceType == NetworkInterfaceType.Wireless80211) score += 3;
                    if (ip.StartsWith("192.168.")) score += 2;
                    list.Add((ip, score));
                }
            }
        }
        catch { /* pas d'interface réseau */ }
        return list.OrderByDescending(x => x.Score).Select(x => x.Ip).Distinct().ToList();
    }
}

internal sealed class HttpRequest
{
    public string Method { get; init; } = "GET";
    public string Path { get; init; } = "/";
    public Dictionary<string, string> Query { get; init; } = new();
    public string Body { get; init; } = "";

    public static async Task<HttpRequest?> ReadAsync(NetworkStream stream, CancellationToken ct)
    {
        var buffer = new byte[8192];
        var data = new MemoryStream();
        int headerEnd = -1;
        while (headerEnd < 0)
        {
            int n = await stream.ReadAsync(buffer, ct);
            if (n <= 0) return null;
            data.Write(buffer, 0, n);
            headerEnd = IndexOf(data.GetBuffer(), (int)data.Length, "\r\n\r\n"u8);
            if (data.Length > 64 * 1024 && headerEnd < 0) return null;
        }
        var raw = data.ToArray();
        var head = Encoding.ASCII.GetString(raw, 0, headerEnd);
        var lines = head.Split("\r\n");
        var first = lines[0].Split(' ');
        if (first.Length < 2) return null;
        int contentLength = 0;
        bool chunked = false;
        foreach (var l in lines.Skip(1))
        {
            var i = l.IndexOf(':');
            if (i <= 0) continue;
            var name = l[..i].Trim();
            var value = l[(i + 1)..].Trim();
            if (name.Equals("Content-Length", StringComparison.OrdinalIgnoreCase)) int.TryParse(value, out contentLength);
            if (name.Equals("Transfer-Encoding", StringComparison.OrdinalIgnoreCase) && value.Contains("chunked", StringComparison.OrdinalIgnoreCase)) chunked = true;
        }
        if (contentLength > 20 * 1024 * 1024) return null;
        var body = new MemoryStream();
        body.Write(raw, headerEnd + 4, raw.Length - headerEnd - 4);
        if (chunked)
        {
            // Lire jusqu'au dernier morceau (0\r\n\r\n), puis décoder
            while (body.Length < 20 * 1024 * 1024 && !EndsWithFinalChunk(body))
            {
                int n = await stream.ReadAsync(buffer, ct);
                if (n <= 0) break;
                body.Write(buffer, 0, n);
            }
            body = DecodeChunked(body.ToArray());
        }
        else
        {
            while (body.Length < contentLength)
            {
                int n = await stream.ReadAsync(buffer, ct);
                if (n <= 0) break;
                body.Write(buffer, 0, n);
            }
        }

        var target = first[1];
        var q = target.IndexOf('?');
        var path = q >= 0 ? target[..q] : target;
        var query = new Dictionary<string, string>();
        if (q >= 0)
            foreach (var kv in target[(q + 1)..].Split('&', StringSplitOptions.RemoveEmptyEntries))
            {
                var p = kv.Split('=', 2);
                query[Uri.UnescapeDataString(p[0])] = p.Length > 1 ? Uri.UnescapeDataString(p[1].Replace('+', ' ')) : "";
            }
        return new HttpRequest { Method = first[0].ToUpperInvariant(), Path = path, Query = query, Body = Encoding.UTF8.GetString(body.ToArray()) };
    }

    private static int IndexOf(byte[] buf, int len, ReadOnlySpan<byte> pattern) =>
        buf.AsSpan(0, len).IndexOf(pattern);

    private static bool EndsWithFinalChunk(MemoryStream ms)
    {
        var b = ms.GetBuffer().AsSpan(0, (int)ms.Length);
        return b.Length >= 5 && (b.EndsWith("\r\n0\r\n\r\n"u8) || (b.Length == 5 && b.SequenceEqual("0\r\n\r\n"u8)));
    }

    private static MemoryStream DecodeChunked(byte[] data)
    {
        var result = new MemoryStream();
        int pos = 0;
        while (pos < data.Length)
        {
            int lineEnd = data.AsSpan(pos).IndexOf("\r\n"u8);
            if (lineEnd < 0) break;
            var sizeText = Encoding.ASCII.GetString(data, pos, lineEnd).Split(';')[0].Trim();
            if (!int.TryParse(sizeText, System.Globalization.NumberStyles.HexNumber, null, out var size) || size == 0) break;
            pos += lineEnd + 2;
            if (pos + size > data.Length) size = data.Length - pos;
            result.Write(data, pos, size);
            pos += size + 2;
        }
        return result;
    }
}

internal sealed class HttpResponse
{
    public int Status { get; }
    public string ContentType { get; }
    public byte[] Body { get; }
    public Dictionary<string, string> Headers { get; } = new();

    public HttpResponse(int status, string contentType, byte[] body)
    {
        Status = status;
        ContentType = contentType;
        Body = body;
    }

    public static HttpResponse Html(int status, string html) => new(status, "text/html; charset=utf-8", Encoding.UTF8.GetBytes(html));
    public static HttpResponse Json(int status, string json) => new(status, "application/json; charset=utf-8", Encoding.UTF8.GetBytes(json));
    public static HttpResponse Text(int status, string text) => new(status, "text/plain; charset=utf-8", Encoding.UTF8.GetBytes(text));

    public async Task WriteAsync(NetworkStream stream, CancellationToken ct)
    {
        var reason = Status switch { 200 => "OK", 204 => "No Content", 403 => "Forbidden", 404 => "Not Found", _ => "Error" };
        var sb = new StringBuilder();
        sb.Append($"HTTP/1.1 {Status} {reason}\r\n");
        sb.Append($"Content-Type: {ContentType}\r\n");
        sb.Append($"Content-Length: {Body.Length}\r\n");
        sb.Append("Access-Control-Allow-Origin: *\r\n");
        sb.Append("Access-Control-Allow-Methods: GET, POST, OPTIONS\r\n");
        sb.Append("Access-Control-Allow-Headers: Content-Type\r\n");
        sb.Append("Access-Control-Allow-Private-Network: true\r\n");
        if (!Headers.ContainsKey("Cache-Control")) sb.Append("Cache-Control: no-store\r\n");
        foreach (var (k, v) in Headers) sb.Append($"{k}: {v}\r\n");
        sb.Append("Connection: close\r\n\r\n");
        await stream.WriteAsync(Encoding.UTF8.GetBytes(sb.ToString()), ct);
        await stream.WriteAsync(Body, ct);
        await stream.FlushAsync(ct);
    }
}
