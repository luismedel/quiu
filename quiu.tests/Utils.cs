using System;
using System.Text.Json;
using quiu.core;

namespace quiu.tests
{
    public static class Utils
    {
        public static Context InitApp ()
        {
            var datadir = Path.Combine (Path.GetTempPath (), $"quiu/tests-{DateTime.Now.ToString ("yyyy-MM-dd-hh-mm-ss-ff")}-{Guid.NewGuid().ToString("N")}");
            System.IO.Directory.CreateDirectory (datadir);

            Config cfg = new Config ();
            cfg["data_dir"] = datadir;

            return new Context (cfg);
        }

        public static void DisposeApp (Context app)
        {
            app.Dispose ();
            System.IO.Directory.Delete (app.Config.Get<string> ("data_dir")!, true);
        }

        public static Channel CreateChannel (Context app, Guid? guid = null)
        {
            var chnGuid = guid ?? Guid.NewGuid ();
            return app.AddChannel (chnGuid);
        }

        public static void AssertJsonValue<T>(JsonDocument json, string selector, T expected)
        {
            var elem = json.GetPropertyBySelector (selector);
            Assert.NotNull (elem);

            var value = elem!.Value.GetRawText ();
            if (value.Length > 1 && value[0] == '"')
                value = value.Substring (1, value.Length - 2);

            Assert.Equal (expected!, Convert.ChangeType (value, typeof (T)));
        }

        public static void AssertJsonValue<T> (string json, string selector, T expected) => AssertJsonValue<T> (JsonDocument.Parse (json), selector, expected);
    }
}
