using System.Collections.Specialized;
using System.Net;

using quiu.core;
using quiu.http;

namespace quiu.tests;

public abstract class HttpServerTestsBase<T>
    : IDisposable
where T: HttpServerBase
{
    protected int ServerPort { get; private set; }
    protected string ServerHost => "localhost";
    protected string ServerRoot => $"http://{ServerHost}:{ServerPort}";

    protected Context App => _app;
    protected T Server => _server;

    public HttpServerTestsBase ()
    {
        ServerPort = new Random ().Next (16536, 32768);

        Config config = InitConfig ();
        _app = Utils.InitApp (initialConfig:config);

        _app.Config["server_host"] = ServerHost;
        _app.Config["server_port"] = ServerPort;
        _server = InitServer ();

        _app.EnqueueTask(_server.RunLoop (), "server loop");
    }

    /// <summary>
    /// Inits the config used by the test context.
    /// </summary>
    /// <returns></returns>
    protected abstract Config InitConfig ();

    /// <summary>
    /// Inits the server instance.
    /// </summary>
    /// <returns></returns>
    protected abstract T InitServer ();

    public virtual void Dispose ()
    {
        Utils.DisposeApp (_app);
    }

    protected async Task<HttpResponseMessage> DoGet (string path, NameValueCollection? headers = null)
    {
        var url = $"{this.ServerRoot}{path}";

        using (var client = new HttpClient ())
        {
            using (var req = new HttpRequestMessage (HttpMethod.Get, url))
            {
                if (headers != null)
                {
                    foreach (var key in headers!.AllKeys)
                        req.Headers.Add (key!, headers[key]);
                }

                return await client.SendAsync (req);
            }
        }
    }

    protected async Task<HttpResponseMessage> DoPost (string path, string data, NameValueCollection? headers = null)
    {
        var url = $"{this.ServerRoot}{path}";

        using (var client = new HttpClient ())
        {
            using (var req = new HttpRequestMessage (HttpMethod.Post, url))
            {
                if (headers != null)
                {
                    foreach (var key in headers!.AllKeys)
                        req.Headers.Add (key!, headers[key]);
                }

                req.Content = new StringContent (data, System.Text.Encoding.UTF8);
                return await client.SendAsync (req);
            }
        }
    }

    protected async Task<HttpResponseMessage> DoDelete (string path)
    {
        var url = $"{this.ServerRoot}{path}";

        using (var client = new HttpClient ())
            using (var req = new HttpRequestMessage (HttpMethod.Delete, url))
                return await client.SendAsync (req);
    }

    protected async Task<string> ContentStringAsync (HttpResponseMessage response) => await response.Content.ReadAsStringAsync ();

    readonly Context _app;
    readonly T _server;
}