using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Text;

namespace Dotenvx.Interop.DotenvxJs.Helpers;

/// <summary>A response from <see cref="ArmorHttp.Request" />: status code and the raw body text.</summary>
internal record HttpResult(int StatusCode, string Body);

/// <summary>A failed request; <see cref="Code" /> is Node's `error.code` (`ECONNREFUSED`, `ENOTFOUND`, `ERR_INVALID_URL`, ...).</summary>
internal class HttpRequestFailedException(string code, string message, Exception? cause = null) : Exception(message, cause)
{
    public string Code { get; } = code;
}

/// <remarks>
/// Reference source: dotenvx/src/lib/helpers/http.js (`request` from @dotenvx/tooling, an undici wrapper) and
/// dotenvx/src/lib/helpers/isNetworkError.js. Network failures carry undici's own messages
/// (`connect ECONNREFUSED 127.0.0.1:1`, `getaddrinfo ENOTFOUND host`) since the CLIs print them.
/// </remarks>
internal static class ArmorHttp
{
    private static readonly HashSet<string> NetworkErrorCodes =
    [
        "ARMOR_UNREACHABLE", "EAI_AGAIN", "EPERM", "EACCES", "ECONNRESET", "ECONNREFUSED", "ENOTFOUND", "ENETDOWN",
        "ENETUNREACH", "EHOSTDOWN", "EHOSTUNREACH", "EPIPE", "ETIMEDOUT", "UND_ERR_CONNECT_TIMEOUT", "UND_ERR_HEADERS_TIMEOUT",
        "UND_ERR_BODY_TIMEOUT", "UND_ERR_SOCKET"
    ];

    /// <summary>The code of a request that was aborted by its own deadline (the AbortController of armorRequest.js).</summary>
    public const string AbortedCode = "ABORT_ERR";

    private static readonly AsyncLocal<TimeSpan?> Deadline = new();

    private static readonly Func<string, string, IReadOnlyDictionary<string, string>?, string?, HttpResult>
        DefaultRequest = Send;

    /// <summary>(url, method, headers, body) => response. Swappable so unit tests never touch the network.</summary>
    public static Func<string, string, IReadOnlyDictionary<string, string>?, string?, HttpResult> Request
    {
        get;
        internal set;
    } = DefaultRequest;

    internal static void ResetToDefaults()
    {
        Request = DefaultRequest;
    }

    /// <summary>
    /// helpers/http.js `http(url, opts)`: the request, with a connectivity failure turned into
    /// `[CODE] unable to connect to {origin}. Check your connection and that the server is running.` (code: the error's, else its
    /// cause's, else NETWORK_ERROR). Every dotenvx call to the Armor service goes through it.
    /// </summary>
    public static HttpResult Http(string url, string method, IReadOnlyDictionary<string, string>? headers, string? body)
    {
        try
        {
            return Request(url, method, headers, body);
        }
        catch (Exception error) when (IsNetworkError(error))
        {
            string code = CodeOf(error) ?? CodeOf(error.InnerException) ?? "NETWORK_ERROR";
            string origin = new Uri(url).GetLeftPart(UriPartial.Authority);

            throw new HttpRequestFailedException(code,
                $"[{code}] unable to connect to {origin}. Check your connection and that the server is running.", error);
        }
    }

    private static string? CodeOf(Exception? error)
    {
        return error switch
        {
            HttpRequestFailedException failed => failed.Code,
            Dotenvx.Providers.ProviderException provider => provider.Code,
            DotenvxException dotenvx => dotenvx.Code,
            _ => null
        };
    }

    /// <summary>Runs a request with a deadline for the whole exchange, body included (armorRequest.js aborts after 30 seconds).</summary>
    internal static HttpResult RequestWithin(TimeSpan deadline, string url, string method,
        IReadOnlyDictionary<string, string>? headers, string? body)
    {
        Deadline.Value = deadline;
        try
        {
            return Request(url, method, headers, body);
        }
        finally
        {
            Deadline.Value = null;
        }
    }

    /// <summary>
    /// isNetworkError.js of @dotenvx/providers: the error, or any error behind it (its cause, the errors of an aggregate),
    /// carries a connectivity code.
    /// </summary>
    public static bool IsNetworkError(Exception? error)
    {
        if (error is null)
        {
            return false;
        }

        string? code = CodeOf(error);

        return (code is not null && NetworkErrorCodes.Contains(code)) ||
               IsNetworkError(error.InnerException) ||
               (error is ArmorUnreachableException unreachable && IsNetworkError(unreachable.Cause)) ||
               (error is AggregateException aggregate && aggregate.InnerExceptions.Any(IsNetworkError));
    }

    private static HttpResult Send(string url, string method, IReadOnlyDictionary<string, string>? headers, string? body)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out Uri? uri) || uri.Scheme is not ("http" or "https"))
        {
            throw new HttpRequestFailedException("ERR_INVALID_URL", "Invalid URL");
        }

        TimeSpan? deadline = Deadline.Value;
        using var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false })
        {
            Timeout = deadline ?? TimeSpan.FromMinutes(5)
        };
        using var request = new HttpRequestMessage(new HttpMethod(method), uri);

        if (body is not null)
        {
            request.Content = new StringContent(body, new UTF8Encoding(false));
            request.Content.Headers.ContentType = null;
        }

        // undici sends a Content-Type header even on a body-less GET (getAccount.js does); HttpClient only can through content
        if (request.Content is null && headers is not null &&
            headers.Keys.Any(name => name.Equals("Content-Type", StringComparison.OrdinalIgnoreCase)))
        {
            request.Content = new ByteArrayContent([]);
        }

        foreach ((string name, string value) in headers ?? new Dictionary<string, string>())
        {
            if (name.Equals("Content-Type", StringComparison.OrdinalIgnoreCase) && request.Content is not null)
            {
                request.Content.Headers.ContentType = MediaTypeHeaderValue.Parse(value);
            }
            else
            {
                request.Headers.TryAddWithoutValidation(name, value);
            }
        }

        try
        {
            using HttpResponseMessage response = client.Send(request);

            return new HttpResult((int)response.StatusCode, response.Content.ReadAsStringAsync().GetAwaiter().GetResult());
        }
        catch (HttpRequestException error) when (error.InnerException is SocketException socket)
        {
            throw socket.SocketErrorCode switch
            {
                SocketError.ConnectionRefused => new HttpRequestFailedException("ECONNREFUSED",
                    $"connect ECONNREFUSED {Endpoint(uri)}"),
                SocketError.HostNotFound or SocketError.TryAgain => new HttpRequestFailedException("ENOTFOUND",
                    $"getaddrinfo ENOTFOUND {uri.Host}"),
                SocketError.NetworkUnreachable => new HttpRequestFailedException("ENETUNREACH",
                    $"connect ENETUNREACH {Endpoint(uri)}"),
                SocketError.TimedOut => new HttpRequestFailedException("ETIMEDOUT", $"connect ETIMEDOUT {Endpoint(uri)}"),
                SocketError.ConnectionReset => new HttpRequestFailedException("ECONNRESET", "read ECONNRESET"),
                _ => new HttpRequestFailedException("UND_ERR_SOCKET", "other side closed")
            };
        }
        catch (TaskCanceledException)
        {
            throw deadline is not null
                ? new HttpRequestFailedException(AbortedCode, "This operation was aborted")
                : new HttpRequestFailedException("UND_ERR_BODY_TIMEOUT", "Body Timeout Error");
        }
    }

    private static string Endpoint(Uri uri)
    {
        string host = uri.HostNameType == UriHostNameType.Dns && uri.Host == "localhost" ? "127.0.0.1" : uri.Host;

        return $"{host}:{uri.Port}";
    }
}
