using System.Collections.Specialized;
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
    public async Task Test_Append ()
    {
        var chn = Utils.CreateChannel (App);

        var resp =  await DoPost ($"/channel/{chn.Guid}", GetTestInput ());
        Assert.Equal (201, (int) resp.StatusCode);

        var json = await ContentStringAsync (resp);
        Utils.AssertJsonValue (json, "processed", 1);
        Utils.AssertJsonValue (json, "commited", 1);
        Utils.AssertJsonValue (json, "error", false);

        // Verify backend side
        Assert.Equal (1, (int) chn.LastOffset);
    }

    [Fact]
    public async Task Test_AppendNoWait ()
    {
        var chn = Utils.CreateChannel (App);

        var headers = new NameValueCollection ();
        headers["X-Quiu-NoWait"] = "1";

        var resp = await DoPost ($"/channel/{chn.Guid}", GetTestInput (), headers);
        Assert.InRange ((int) resp.StatusCode, 201, 202);

        var json = await ContentStringAsync (resp);
        Utils.AssertJsonValue (json, "processed", 1);
        Utils.AssertJsonValue (json, "error", false);

        // Wait backend to sync
        Thread.Sleep (1000);

        // Verify backend side
        Assert.Equal (1, (int) chn.LastOffset);
    }

    [Fact]
    public async Task Test_AppendMany ()
    {
        const int COUNT = 10;

        var chn = Utils.CreateChannel (App);

        var buffer = string.Join ('\n', Enumerable.Range (0, COUNT).Select (i => GetTestInput (i)));

        var resp = await DoPost ($"/channel/{chn.Guid}", buffer);
        Assert.Equal (201, (int) resp.StatusCode);
        var json = await ContentStringAsync (resp);
        Utils.AssertJsonValue (json, "processed", COUNT);
        Utils.AssertJsonValue (json, "commited", COUNT);
        Utils.AssertJsonValue (json, "error", false);

        // Verify backend side
        Assert.Equal (10, (int) chn.LastOffset);
    }

    [Fact]
    public async Task Test_AppendManyNoWait ()
    {
        const int COUNT = 10;

        var chn = Utils.CreateChannel (App);

        var buffer = string.Join ('\n', Enumerable.Range (0, COUNT).Select (i => GetTestInput (i)));

        var headers = new NameValueCollection ();
        headers["X-Quiu-NoWait"] = "1";

        var resp = await DoPost ($"/channel/{chn.Guid}", buffer, headers);
        Assert.InRange ((int) resp.StatusCode, 201, 202);

        var json = await ContentStringAsync (resp);
        //Console.WriteLine (resp.Content.ToString());
        Utils.AssertJsonValue (json, "processed", COUNT);
        Utils.AssertJsonValue (json, "error", false);

        // Wait backend to sync
        Thread.Sleep (1000);

        // Verify backend side
        Assert.Equal (10, (int) chn.LastOffset);
    }

    [Fact]
    public async Task Test_Fetch ()
    {
        const int COUNT = 100;

        var chn = Utils.CreateChannel (App);

        // Append 100 test items
        var buffer = string.Join ('\n', Enumerable.Range (0, COUNT).Select (i => GetTestInput (i + 1)));
        await DoPost ($"/channel/{chn.Guid}", buffer);

        // Pick a random one
        var offset = new Random ().NextInt64 (COUNT);
        var resp = await DoGet ($"/channel/{chn.Guid}/{offset}");
        Assert.Equal (200, (int) resp.StatusCode);
        Utils.AssertJsonValue (await ContentStringAsync (resp), "payload", GetTestInput (offset));
    }

    [Fact]
    public async Task Test_FetchMany ()
    {
        const int COUNT = 100;
        var chn = Utils.CreateChannel (App);

        // Append 100 test items
        var buffer = string.Join ('\n', Enumerable.Range (0, COUNT).Select (i => GetTestInput (i + 1)));
        await DoPost ($"/channel/{chn.Guid}", buffer);

        // Pick 10 from a random offset (not near the end)
        var offset = new Random ().Next (1, COUNT - 11);
        var resp = await DoGet ($"/channel/{chn.Guid}/{offset}/10");
        Assert.Equal (200, (int) resp.StatusCode);
        var items = (await ContentStringAsync (resp)).Split ('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal (10, items.Length);

        for (int i = 0; i < items.Length; i++)
            Utils.AssertJsonValue (items[i], "payload", GetTestInput (offset + i));

        // Pick 10 from the end (not enough items)
        offset = COUNT - 5;
        resp = await DoGet ($"/channel/{chn.Guid}/{offset}/10");
        Assert.Equal (200, (int) resp.StatusCode);
        items = (await ContentStringAsync (resp)).Split ('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal (6, items.Length);

        for (int i = 0; i < items.Length; i++)
            Utils.AssertJsonValue (items[i], "payload", GetTestInput (offset + i));
    }

    static string GetTestInput (Int64 suffix = 0) => $"Input text {suffix}";
}