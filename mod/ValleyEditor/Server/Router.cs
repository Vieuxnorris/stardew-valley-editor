using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace ValleyEditor.Server;

/// <summary>An API call after routing.</summary>
/// <param name="Method">The HTTP method, upper case.</param>
/// <param name="Path">The request path, without query string.</param>
/// <param name="Params">Values captured by <c>{name}</c> segments of the route template.</param>
/// <param name="Query">The query string values.</param>
/// <param name="Body">The parsed JSON body, if any.</param>
internal sealed record ApiRequest(string Method, string Path, IReadOnlyDictionary<string, string> Params, NameValueCollection Query, JToken? Body)
{
    /// <summary>The body as a JSON object, or a 400 error.</summary>
    public JObject BodyObject => this.Body as JObject ?? throw new ApiException(400, "A JSON object body is required.");
}

/// <summary>An error returned to the client with a specific HTTP status.</summary>
internal sealed class ApiException : Exception
{
    public int Status { get; }

    public ApiException(int status, string message)
        : base(message)
    {
        this.Status = status;
    }
}

/// <summary>Handles one API route; the result is serialised to JSON.</summary>
internal delegate Task<object?> ApiHandler(ApiRequest request);

/// <summary>Matches requests against route templates like <c>/api/npcs/{name}</c>.</summary>
internal sealed class Router
{
    private readonly List<(string Method, string[] Segments, ApiHandler Handler)> routes = new();

    public Router Map(string method, string template, ApiHandler handler)
    {
        this.routes.Add((method.ToUpperInvariant(), Split(template), handler));
        return this;
    }

    public Router Get(string template, ApiHandler handler) => this.Map("GET", template, handler);
    public Router Post(string template, ApiHandler handler) => this.Map("POST", template, handler);
    public Router Patch(string template, ApiHandler handler) => this.Map("PATCH", template, handler);
    public Router Put(string template, ApiHandler handler) => this.Map("PUT", template, handler);
    public Router Delete(string template, ApiHandler handler) => this.Map("DELETE", template, handler);

    /// <summary>Find the handler for a request.</summary>
    /// <exception cref="ApiException">No route matches (404), or it matches with another method (405).</exception>
    public (ApiHandler Handler, Dictionary<string, string> Params) Resolve(string method, string path)
    {
        string[] segments = Split(path);
        bool pathMatched = false;
        foreach (var route in this.routes)
        {
            var values = Match(route.Segments, segments);
            if (values is null)
                continue;
            pathMatched = true;
            if (route.Method == method)
                return (route.Handler, values);
        }

        throw pathMatched
            ? new ApiException(405, $"Method {method} not allowed on {path}.")
            : new ApiException(404, $"No API route for {path}.");
    }

    private static Dictionary<string, string>? Match(string[] template, string[] path)
    {
        if (template.Length != path.Length)
            return null;

        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < template.Length; i++)
        {
            string t = template[i];
            if (t.StartsWith('{') && t.EndsWith('}'))
                values[t[1..^1]] = Uri.UnescapeDataString(path[i]);
            else if (!string.Equals(t, path[i], StringComparison.OrdinalIgnoreCase))
                return null;
        }
        return values;
    }

    private static string[] Split(string path) => path.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
}
