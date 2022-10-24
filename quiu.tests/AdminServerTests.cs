using System.Net;
using System.Text.Json;
using quiu.core;
using quiu.http;

namespace quiu.tests;

public class AdminServerTests
    : HttpServerTestsBase<HttpAdminServer>
{
    protected override Config InitConfig ()
    {
        var result = new Config ();
        result["admin_server_host"] = ServerHost;
        result["admin_server_port"] = ServerPort;

        return result;
    }

    protected override HttpAdminServer InitServer () => new HttpAdminServer (App);

    [Fact]
    public async Task Test_CreateChannel()
    {
        var resp = await this.DoPost ("/channel/new", string.Empty);
        Assert.Equal (201, (int) resp.StatusCode);

        var doc = JsonDocument.Parse (await this.ContentStringAsync (resp));
        var prop = doc.GetPropertyBySelector ("guid");
        Assert.NotNull (prop);
        Assert.False (String.IsNullOrEmpty(prop!.Value.GetString ()));

        var guid = Guid.Parse (prop!.Value.GetString ()!);
        Assert.NotNull (App.GetChannel (guid));
    }

    [Fact]
    public async Task Test_DropChannelNoPrune ()
    {
        var resp = await this.DoPost ("/channel/new", string.Empty);
        var result = JsonDocument.Parse (await this.ContentStringAsync (resp));
        var guid = Guid.Parse (result!.GetPropertyBySelector("guid")!.Value.GetString()!);

        var channel = App.GetChannel (guid);
        Assert.NotNull (channel);

        var dataPath = channel!.StoragePath;

        resp = await this.DoDelete ($"/channel/{guid}");
        Assert.Equal (200, (int) resp.StatusCode);

        Assert.Null (App.GetChannel (guid));
        Assert.True (Directory.Exists (dataPath));
    }

    [Fact]
    public async Task Test_DropChannelPrune ()
    {
        var resp = await this.DoPost ("/channel/new", string.Empty);
        var result = JsonDocument.Parse (await this.ContentStringAsync (resp));
        var guid = Guid.Parse (result!.GetPropertyBySelector ("guid")!.Value.GetString ()!);

        var channel = App.GetChannel (guid);
        Assert.NotNull (channel);

        var dataPath = channel!.StoragePath;

        resp = await this.DoDelete ($"/channel/{guid}?prune=true");
        Assert.Equal (200, (int) resp.StatusCode);

        Assert.Null (App.GetChannel (guid));
        Assert.False (File.Exists (dataPath));
    }
}