using System.Net;
using quiu.http;

namespace quiu.tests;

public class ServerTests
    : ServerTestsBase<QuiuServer>
{
    const string SERVER_HOST = $"http://localhost:8081";

    protected override string ServerHost => SERVER_HOST;

    protected override QuiuServer InitServer ()
    {
        App.Config["server_host"] = "*";
        App.Config["server_port"] = "8081";

        return new QuiuServer (App);
    }

    [Fact]
    public async Task Test_Appemd ()
    {
        const string text = "hello, world";

        var chn = Utils.CreateChannel (App);
        var cmdSelectMaxRowId = chn.Storage.PrepareCommand ("select max(rowid) from data_t");

        var resp =  await DoPost ($"/channel/{chn.Guid}", text);
        Assert.Equal (201, (int) resp.StatusCode);

        var json = await ContentStringAsync (resp);
        Utils.AssertJsonValue (json, "processed", 1);
        Utils.AssertJsonValue (json, "error", false);

        // Verify backend side
        Assert.Equal (1, chn.Storage.ExecuteScalar<int> (cmdSelectMaxRowId));
    }

    [Fact]
    public async Task Test_AppendMany ()
    {
        const string text = "hello, world";

        var chn = Utils.CreateChannel (App);
        var cmdSelectMaxRowId = chn.Storage.PrepareCommand ("select max(rowid) from data_t");

        var buffer = string.Join ('\n', Enumerable.Range (0, 10).Select (i => text));

        var resp = await DoPost ($"/channel/{chn.Guid}", buffer);
        Assert.Equal (201, (int) resp.StatusCode);
        var json = await ContentStringAsync (resp);
        Utils.AssertJsonValue (json, "processed", 10);
        Utils.AssertJsonValue (json, "error", false);

        // Verify backend side
        Assert.Equal (10, chn.Storage.ExecuteScalar<int> (cmdSelectMaxRowId));
    }

    [Fact]
    public async Task Test_Fetch ()
    {
        const string text = "hello, world";

        var chn = Utils.CreateChannel (App);

        // Append 100 test items
        var buffer = string.Join ('\n', Enumerable.Range (0, 100).Select (i => $"{text}-{i+1}"));
        await DoPost ($"/channel/{chn.Guid}", buffer);

        // Pick a random one
        var offset = new Random ().NextInt64 (100);
        var resp = await DoGet ($"/channel/{chn.Guid}/{offset}");
        Assert.Equal (200, (int) resp.StatusCode);
        Utils.AssertJsonValue (await ContentStringAsync (resp), "payload", $"{text}-{offset}");
    }

    [Fact]
    public async Task Test_FetchMany ()
    {
        const string text = "hello, world";

        var chn = Utils.CreateChannel (App);

        // Append 100 test items
        var buffer = string.Join ('\n', Enumerable.Range (0, 100).Select (i => $"{text}-{i + 1}"));
        await DoPost ($"/channel/{chn.Guid}", buffer);

        // Pick 10 from a random offset (not near the end)
        var offset = new Random ().Next (1, 89);
        var resp = await DoGet ($"/channel/{chn.Guid}/{offset}/10");
        Assert.Equal (200, (int) resp.StatusCode);
        var items = (await ContentStringAsync (resp)).Split ('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal (10, items.Length);

        for (int i = 0; i < items.Length; i++)
            Utils.AssertJsonValue (items[i], "payload", $"{text}-{offset+i}");

        // Pick 10 from the end (not enough items)
        offset = 95;
        resp = await DoGet ($"/channel/{chn.Guid}/{offset}/10");
        Assert.Equal (200, (int) resp.StatusCode);
        items = (await ContentStringAsync (resp)).Split ('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal (6, items.Length);

        for (int i = 0; i < items.Length; i++)
            Utils.AssertJsonValue (items[i], "payload", $"{text}-{offset + i}");
    }
}