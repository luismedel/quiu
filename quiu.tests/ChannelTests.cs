using quiu.core;

namespace quiu.tests;

public class ChannelTests
    : IDisposable
{
    public ChannelTests ()
    {
        _app = Utils.InitApp ();
    }

    public void Dispose ()
    {
        Utils.DisposeApp (_app);
    }

    [Fact]
    public void Test_CreateChannel ()
    {
        Assert.True(File.Exists(Utils.CreateChannel (_app).Storage.Path));
        Assert.True (File.Exists (Utils.CreateChannel (_app, Guid.NewGuid ()).Storage.Path));
    }

    [Fact]
    public void Test_DropChannel ()
    {
        // Only deop channel
        var chn = Utils.CreateChannel (_app);
        _app.DropChannel (chn);
        Assert.True (File.Exists (chn.Storage.Path));

        // Drop channel and prune data
        var chn2 = Utils.CreateChannel (_app);
        _app.DropChannel (chn2, true);
        Assert.False (File.Exists (chn2.Storage.Path));
    }

    [Fact]
    public void Test_Append ()
    {
        var data = Serializer.FromText (GetTestInput ());

        var chn = Utils.CreateChannel (_app);
        var cmdSelectMaxRowId = chn.Storage.PrepareCommand ("select max(rowid) from data_t");

        // Append 1 item
        chn.Append (data);
        Assert.Equal (1, chn.Storage.ExecuteScalar<int> (cmdSelectMaxRowId));

        for (int i = 0; i < 10; i++)
            chn.Append (data);
        Assert.Equal (11, chn.Storage.ExecuteScalar<int> (cmdSelectMaxRowId));
    }

    [Fact]
    public void Test_Fetch ()
    {
        var chn = Utils.CreateChannel (_app);
        chn.Append (Serializer.FromText (GetTestInput ()));
        var data = chn.Fetch (1);
        Assert.Equal (GetTestInput (), Serializer.ToText (data.Value!));
    }

    [Fact]
    public void Test_FetchMany ()
    {
        var chn = Utils.CreateChannel (_app);

        for (int i = 0; i < 100; i++)
            chn.Append (Serializer.FromText(GetTestInput (i + 1)));

        var offset = new Random ().Next (1, 89);
        var items = chn.Fetch (offset, 10).ToArray ();
        Assert.Equal (10, items.Length);

        for (int i = 0; i < items.Length; i++)
            Assert.Equal (GetTestInput (offset + i), Serializer.ToText (items[i].Value));

        offset = 95;
        items = chn.Fetch (offset, 10).ToArray ();
        Assert.Equal (6, items.Length);

        for (int i = 0; i < items.Length; i++)
            Assert.Equal (GetTestInput(offset + i), Serializer.ToText (items[i].Value));
    }

    static string GetTestInput (int suffix = 0) => $"Input text {suffix}";

    readonly Context _app;
}