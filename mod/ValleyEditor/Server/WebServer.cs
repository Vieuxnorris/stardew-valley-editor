using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using StardewModdingAPI;

namespace ValleyEditor.Server;

/// <summary>A local HTTP server that serves the web UI and the JSON API.</summary>
/// <remarks>
/// Security: it only listens on localhost, every API call needs the per-session token in the
/// <c>X-Editor-Token</c> header, and calls from another origin are refused, so other web pages
/// open in the browser can't drive the game.
/// </remarks>
internal sealed class WebServer : IDisposable
{
    /// <summary>How long an API call may wait for the game thread.</summary>
    private static readonly TimeSpan ApiTimeout = TimeSpan.FromSeconds(5);

    private static readonly JsonSerializerSettings JsonSettings = new()
    {
        ContractResolver = new CamelCasePropertyNamesContractResolver(),
        NullValueHandling = NullValueHandling.Include,
    };

    private static readonly Dictionary<string, string> ContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        [".html"] = "text/html; charset=utf-8",
        [".js"] = "text/javascript; charset=utf-8",
        [".css"] = "text/css; charset=utf-8",
        [".json"] = "application/json; charset=utf-8",
        [".svg"] = "image/svg+xml",
        [".png"] = "image/png",
        [".ico"] = "image/x-icon",
        [".woff2"] = "font/woff2",
    };

    private readonly HttpListener listener = new();
    private readonly Router router;
    private readonly string webRoot;
    private readonly int port;
    private readonly IMonitor monitor;

    /// <summary>The per-session secret required on API calls.</summary>
    public string Token { get; } = Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();

    /// <summary>The URL to open in a browser, token included.</summary>
    public string Url => $"http://localhost:{this.port}/?token={this.Token}";

    public WebServer(int port, string webRoot, Router router, IMonitor monitor)
    {
        this.port = port;
        this.webRoot = Path.GetFullPath(webRoot);
        this.router = router;
        this.monitor = monitor;
    }

    public void Start()
    {
        // "localhost" (not 127.0.0.1) so http.sys accepts the prefix without an admin URL reservation
        this.listener.Prefixes.Add($"http://localhost:{this.port}/");
        this.listener.Start();
        _ = Task.Run(this.AcceptLoop);
    }

    public void Dispose()
    {
        if (this.listener.IsListening)
            this.listener.Stop();
        this.listener.Close();
    }

    private async Task AcceptLoop()
    {
        while (this.listener.IsListening)
        {
            HttpListenerContext context;
            try
            {
                context = await this.listener.GetContextAsync();
            }
            catch (Exception ex) when (ex is HttpListenerException or ObjectDisposedException or InvalidOperationException)
            {
                break; // stopped
            }

            _ = Task.Run(() => this.Handle(context));
        }
    }

    private async Task Handle(HttpListenerContext context)
    {
        HttpListenerResponse response = context.Response;
        try
        {
            response.Headers["Cache-Control"] = "no-store";
            string path = context.Request.Url?.AbsolutePath ?? "/";
            if (path.StartsWith("/api/", StringComparison.OrdinalIgnoreCase))
                await this.HandleApi(context.Request, response, path);
            else
                await this.ServeStatic(response, path);
        }
        catch (Exception ex)
        {
            this.monitor.Log($"Unhandled error serving {context.Request.Url}: {ex}", LogLevel.Error);
            try
            {
                await WriteJson(response, 500, new { error = ex.Message });
            }
            catch
            {
                // the response was already sent or the client disconnected
            }
        }
        finally
        {
            response.Close();
        }
    }

    private async Task HandleApi(HttpListenerRequest request, HttpListenerResponse response, string path)
    {
        try
        {
            string? origin = request.Headers["Origin"];
            if (origin != null && origin != $"http://localhost:{this.port}" && origin != $"http://127.0.0.1:{this.port}")
                throw new ApiException(403, "Cross-origin requests are not allowed.");

            if (!TokensEqual(request.Headers["X-Editor-Token"], this.Token))
                throw new ApiException(401, "Missing or invalid editor token. Open the editor with the URL shown in the SMAPI console (command: editor).");

            var (handler, values) = this.router.Resolve(request.HttpMethod.ToUpperInvariant(), path);
            JToken? body = await ReadBody(request);
            var apiRequest = new ApiRequest(request.HttpMethod.ToUpperInvariant(), path, values, request.QueryString, body);

            Task<object?> work = handler(apiRequest);
            if (await Task.WhenAny(work, Task.Delay(ApiTimeout)) != work)
                throw new ApiException(503, "The game did not respond in time. Is a save loaded and the game running?");

            await WriteJson(response, 200, await work);
        }
        catch (ApiException ex)
        {
            await WriteJson(response, ex.Status, new { error = ex.Message });
        }
    }

    private async Task ServeStatic(HttpListenerResponse response, string path)
    {
        string relative = Uri.UnescapeDataString(path).TrimStart('/');
        if (relative.Length == 0)
            relative = "index.html";

        string file = Path.GetFullPath(Path.Combine(this.webRoot, relative));
        if (!file.StartsWith(this.webRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            response.StatusCode = 403;
            return;
        }

        // client-side routes fall back to the app shell
        if (!File.Exists(file) && !Path.HasExtension(relative))
            file = Path.Combine(this.webRoot, "index.html");

        if (!File.Exists(file))
        {
            response.StatusCode = 404;
            return;
        }

        response.ContentType = ContentTypes.GetValueOrDefault(Path.GetExtension(file), "application/octet-stream");
        byte[] bytes = await File.ReadAllBytesAsync(file);
        response.ContentLength64 = bytes.Length;
        await response.OutputStream.WriteAsync(bytes);
    }

    private static async Task<JToken?> ReadBody(HttpListenerRequest request)
    {
        if (!request.HasEntityBody)
            return null;

        using var reader = new StreamReader(request.InputStream, request.ContentEncoding);
        string text = await reader.ReadToEndAsync();
        if (string.IsNullOrWhiteSpace(text))
            return null;

        try
        {
            return JToken.Parse(text);
        }
        catch (JsonException ex)
        {
            throw new ApiException(400, $"Invalid JSON body: {ex.Message}");
        }
    }

    private static async Task WriteJson(HttpListenerResponse response, int status, object? value)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(value, JsonSettings));
        response.StatusCode = status;
        response.ContentType = "application/json; charset=utf-8";
        response.ContentLength64 = bytes.Length;
        await response.OutputStream.WriteAsync(bytes);
    }

    private static bool TokensEqual(string? given, string expected)
    {
        return given != null
            && CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(given), Encoding.ASCII.GetBytes(expected));
    }
}
