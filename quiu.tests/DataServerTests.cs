using System.Net;
using quiu.core;
using quiu.http;

namespace quiu.tests;

public class DataServerTests
    : HttpServerTestsBase<HttpServer>
{
    protected override Config InitConfig ()
    {
        var result = new Config ();
        result["server_host"] = ServerHost;
        result["server_port"] = ServerPort;

        return result;
    }

    protected override HttpServer InitServer () => new HttpServer (App);

    [Fact]
    public async Task Test_Appemd ()
    {
        var chn = Utils.CreateChannel (App);

        var resp =  await DoPost ($"/channel/{chn.Guid}", GetTestInput ());
        Assert.Equal (201, (int) resp.StatusCode);

        var json = await ContentStringAsync (resp);
        Utils.AssertJsonValue (json, "processed", 1);
        Utils.AssertJsonValue (json, "error", false);

        // Verify backend side
        Assert.Equal (1, (int) chn.LastOffset);
    }

    [Fact]
    public async Task Test_AppendMany ()
    {
        var chn = Utils.CreateChannel (App);

        var buffer = string.Join ('\n', Enumerable.Range (0, 10).Select (i => GetTestInput (i)));

        var resp = await DoPost ($"/channel/{chn.Guid}", buffer);
        Assert.Equal (201, (int) resp.StatusCode);
        var json = await ContentStringAsync (resp);
        Utils.AssertJsonValue (json, "processed", 10);
        Utils.AssertJsonValue (json, "error", false);

        // Verify backend side
        Assert.Equal (10, (int) chn.LastOffset);
    }

    [Fact]
    public async Task Test_Fetch ()
    {
        var chn = Utils.CreateChannel (App);

        // Append 100 test items
        var buffer = string.Join ('\n', Enumerable.Range (0, 100).Select (i => GetTestInput (i + 1)));
        await DoPost ($"/channel/{chn.Guid}", buffer);

        // Pick a random one
        var offset = new Random ().NextInt64 (100);
        var resp = await DoGet ($"/channel/{chn.Guid}/{offset}");
        Assert.Equal (200, (int) resp.StatusCode);
        Utils.AssertJsonValue (await ContentStringAsync (resp), "payload", GetTestInput (offset));
    }

    [Fact]
    public async Task Test_FetchMany ()
    {
        var chn = Utils.CreateChannel (App);

        // Append 100 test items
        var buffer = string.Join ('\n', Enumerable.Range (0, 100).Select (i => GetTestInput (i + 1)));
        await DoPost ($"/channel/{chn.Guid}", buffer);

        // Pick 10 from a random offset (not near the end)
        var offset = new Random ().Next (1, 89);
        var resp = await DoGet ($"/channel/{chn.Guid}/{offset}/10");
        Assert.Equal (200, (int) resp.StatusCode);
        var items = (await ContentStringAsync (resp)).Split ('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal (10, items.Length);

        for (int i = 0; i < items.Length; i++)
            Utils.AssertJsonValue (items[i], "payload", GetTestInput (offset + i));

        // Pick 10 from the end (not enough items)
        offset = 95;
        resp = await DoGet ($"/channel/{chn.Guid}/{offset}/10");
        Assert.Equal (200, (int) resp.StatusCode);
        items = (await ContentStringAsync (resp)).Split ('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal (6, items.Length);

        for (int i = 0; i < items.Length; i++)
            Utils.AssertJsonValue (items[i], "payload", GetTestInput (offset + i));
    }

    static string GetTestInput (Int64 suffix = 0) => $"Input text {suffix}";
}