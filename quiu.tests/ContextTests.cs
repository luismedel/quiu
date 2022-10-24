using System;
using quiu.core;

namespace quiu.tests;

public class ContextTests
{
    [Fact]
    public void Test_IndexFile()
    {
        const int TEST_CHANNEL_COUNT = 5;

        Guid[] guids = Enumerable.Range (0, TEST_CHANNEL_COUNT)
                                 .Select (i => Guid.NewGuid ())
                                 .ToArray ();

        string dataDir;
        string indexFilePath;

        var config1 = new Config ();
        config1.Set ("restore_channels", false);
        using (var ctx1 = Utils.InitApp (initialConfig: config1, pathSuffix: "index-test"))
        {
            dataDir = ctx1.DataDirectory;
            indexFilePath = ctx1.IndexFilePath;

            foreach (var g in guids)
                ctx1.AddChannel (g);
        }

        var config2 = new Config ();
        config2.Set ("data_dir", dataDir);
        config2.Set ("restore_channels", true);
        var ctx2 = Utils.InitApp (initialConfig: config2, pathSuffix: "index-test");

        Assert.Equal (indexFilePath, ctx2.IndexFilePath);
        Assert.Equal (TEST_CHANNEL_COUNT, ctx2.Channels.Length);

        var errors = ctx2.Channels.Where (c => Array.IndexOf (guids, c.Guid) == -1)
                                  .ToArray ();
        Assert.Empty (errors);

        Directory.Delete (dataDir, true);
    }
}

