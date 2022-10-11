using System.Net;
using quiu.http;

namespace quiu.tests;

public abstract class ServerTestsBase<T>
    : IDisposable
where T: HttpServer
{
    protected abstract string ServerHost { get; }

    protected QuiuContext App => _app;
    protected T Server => _server;

    public ServerTestsBase ()
    {
        _app = Utils.InitApp ();
        _server = InitServer ();

        _server.Start ();
    }

    protected abstract T InitServer ();

    public void Dispose ()
    {
        Utils.DisposeApp (_app);
    }

    protected async Task<HttpResponseMessage> DoGet (string path)
    {
        var url = $"{this.ServerHost}{path}";

        using (var client = new HttpClient ())
            return await client.GetAsync (url);
    }

    protected async Task<HttpResponseMessage> DoPost (string path, string data)
    {
        var url = $"{this.ServerHost}{path}";

        using (var client = new HttpClient ())
           return await client.PostAsync (url, new StringContent (data, System.Text.Encoding.UTF8));
    }

    protected async Task<HttpResponseMessage> DoDelete (string path)
    {
        var url = $"{this.ServerHost}{path}";

        using (var client = new HttpClient ())
            return await client.DeleteAsync (url);
    }

    protected async Task<string> ContentStringAsync (HttpResponseMessage response) => await response.Content.ReadAsStringAsync ();

    readonly QuiuContext _app;
    readonly T _server;
}