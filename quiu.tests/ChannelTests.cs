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
        Assert.True(Directory.Exists(Utils.CreateChannel (_app).StoragePath));
        Assert.True (Directory.Exists (Utils.CreateChannel (_app, Guid.NewGuid ()).StoragePath));
    }

    [Fact]
    public void Test_DropChannel ()
    {
        // Only deop channel
        var chn = Utils.CreateChannel (_app);
        _app.DropChannel (chn);
        Assert.True (Directory.Exists (chn.StoragePath));

        // Drop channel and prune data
        var chn2 = Utils.CreateChannel (_app);
        _app.DropChannel (chn2, true);
        Assert.False (Directory.Exists (chn2.StoragePath));
    }

    [Fact]
    public void Test_Append ()
    {
        var data = Serializer.FromText (GetTestInput ());

        var chn = Utils.CreateChannel (_app);

        // Append 1 item
        chn.Append (data);
        Assert.Equal (1, (int) chn.LastOffset);

        for (int i = 0; i < 10; i++)
            chn.Append (data);
        Assert.Equal (11, (int) chn.LastOffset);
    }

    [Fact]
    public void Test_Fetch ()
    {
        var chn = Utils.CreateChannel (_app);
        chn.Append (Serializer.FromText (GetTestInput ()));
        var data = chn.Fetch (1);
        Assert.Equal (GetTestInput (), Serializer.ToText (data!));
    }

    [Fact]
    public void Test_FetchMany ()
    {
        var chn = Utils.CreateChannel (_app);

        for (int i = 0; i < 100; i++)
            chn.Append (Serializer.FromText(GetTestInput (i + 1)));

        var offset = (Int64) new Random ().Next (1, 89);
        var items = chn.Fetch (offset, 10).ToArray ();
        Assert.Equal (10, items.Length);

        for (int i = 0; i < items.Length; i++)
            Assert.Equal (GetTestInput ((int) offset + i), Serializer.ToText (items[i]));

        offset = 95;
        items = chn.Fetch (offset, 10).ToArray ();
        Assert.Equal (6, items.Length);

        for (int i = 0; i < items.Length; i++)
            Assert.Equal (GetTestInput((int) offset + i), Serializer.ToText (items[i]));
    }

    static string GetTestInput (int suffix = 0) => $"Input text {suffix}";

    readonly Context _app;
}