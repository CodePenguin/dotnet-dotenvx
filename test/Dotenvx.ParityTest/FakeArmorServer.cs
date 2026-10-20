using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json.Nodes;

namespace Dotenvx.ParityTest;

/// <summary>One request the fake Armor server received (headers reduced to the ones dotenvx sets).</summary>
public record RecordedRequest(
    string Method, string Path, string? Authorization, string? ContentType, string? Accept, string? DeviceHeader,
    string Body)
{
    /// <summary>The body re-serialized compactly (key order preserved), for stable comparison.</summary>
    public string NormalizedBody => Body.Length == 0 ? "" : JsonNode.Parse(Body)?.ToJsonString() ?? "";

    public override string ToString()
    {
        return $"{Method} {Path} auth={Authorization} ct={ContentType} accept={Accept} device={DeviceHeader} body={NormalizedBody}";
    }
}

public record FakeResponse(int Status, string Body, string ContentType = "application/json");

/// <summary>
/// A local stand-in for the Armor API (https://armor.dotenvx.com) shared by the Node and .NET CLI runs of a parity test:
/// it records every request and answers from a programmable handler.
/// </summary>
public sealed class FakeArmorServer : IDisposable
{
    private readonly HttpListener _listener = new();
    private readonly Thread _thread;

    public FakeArmorServer()
    {
        var probe = new TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        int port = ((IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop();

        BaseUrl = $"http://127.0.0.1:{port}";
        _listener.Prefixes.Add(BaseUrl + "/");
        _listener.Start();
        _thread = new Thread(Loop) { IsBackground = true };
        _thread.Start();
    }

    public string BaseUrl { get; }

    public List<RecordedRequest> Requests { get; } = [];

    /// <summary>Answers a request; the default is an empty JSON object.</summary>
    public Func<RecordedRequest, FakeResponse> Handler { get; set; } = _ => new FakeResponse(200, "{}");

    public void Dispose()
    {
        if (_listener.IsListening)
        {
            _listener.Stop();
        }

        _listener.Close();
    }

    private void Loop()
    {
        while (_listener.IsListening)
        {
            HttpListenerContext context;
            try
            {
                context = _listener.GetContext();
            }
            catch (Exception)
            {
                return;
            }

            try
            {
                using var reader = new StreamReader(context.Request.InputStream, Encoding.UTF8);
                var request = new RecordedRequest(
                    context.Request.HttpMethod,
                    context.Request.Url!.PathAndQuery,
                    context.Request.Headers["Authorization"],
                    context.Request.Headers["Content-Type"],
                    context.Request.Headers["Accept"],
                    context.Request.Headers["dotenvx-device-public-key"],
                    reader.ReadToEnd());

                lock (Requests)
                {
                    Requests.Add(request);
                }

                FakeResponse response = Handler(request);
                byte[] bytes = Encoding.UTF8.GetBytes(response.Body);
                context.Response.StatusCode = response.Status;
                context.Response.ContentType = response.ContentType;
                context.Response.ContentLength64 = bytes.Length;
                context.Response.OutputStream.Write(bytes);
                context.Response.OutputStream.Close();
            }
            catch (Exception)
            {
                try
                {
                    context.Response.Abort();
                }
                catch (ObjectDisposedException)
                {
                    // the listener was disposed while this request was in flight (test teardown)
                }
            }
        }
    }

    public string[] RequestLog()
    {
        lock (Requests)
        {
            return [.. Requests.Select(request => request.ToString())];
        }
    }

    public void Clear()
    {
        lock (Requests)
        {
            Requests.Clear();
        }
    }
}
