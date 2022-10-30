using System;
using System.Net;
using System.IO;
using System.Threading.Channels;
using System.Diagnostics;

using quiu.core;
using System.Collections.Specialized;

namespace quiu.http
{
    public class HttpServer : HttpServerBase
    {
        public const string DEFAULT_HOST = "localhost";
        public const int DEFAULT_PORT = 27812;

        public HttpServer (Context app, string host, int port, CancellationToken cancellationToken)
            : base (app, host, port, cancellationToken)
        {
            RegisterRoute ("POST", "/channel/%guid", AppendData);
            RegisterRoute ("GET", "/channel/%guid/%offset", GetItem);
            RegisterRoute ("GET", "/channel/%guid/%offset/%count", GetItems);

            RegisterRoute ("POST", "/admin/channel/new", CreateChannel);
            RegisterRoute ("DELETE", "/admin/channel/%guid", DropChannel);

            _wal = new WAL ();
            _wal.Start();
        }

        public HttpServer (Context app)
            : this (app,
                    app.Config.Get<string> ("server_host", DEFAULT_HOST)!,
                    app.Config.Get<int>("server_port", DEFAULT_PORT)!,
                    app.CancellationToken)
        {
        }

        async Task GetItem(NameValueCollection segments, HttpListenerRequest request, HttpListenerResponse response)
        {
            var guid = GetRequiredArgument<Guid> (segments, "guid", Guid.TryParse);
            Int64 offset = GetRequiredArgument<Int64> (segments, "offset", Int64.TryParse);

            var channel = App.GetChannel (guid);
            if (channel == null)
                throw new HttpNotFoundException ();

            var data = channel.Fetch (offset);

            await response.SendJsonResponseAsync (200, new { payload = System.Text.Encoding.UTF8.GetString (data!) });
        }

        async Task GetItems (NameValueCollection segments, HttpListenerRequest request, HttpListenerResponse response)
        {
            var guid = GetRequiredArgument<Guid> (segments, "guid", Guid.TryParse);
            Int64 offset = GetRequiredArgument<Int64> (segments, "offset", Int64.TryParse);
            int count = GetRequiredArgument<int> (segments, "count", int.TryParse);

            var channel = App.GetChannel (guid);
            if (channel == null)
                throw new HttpNotFoundException ();

            var data = channel.Fetch (offset, count);
            var processor = (byte[] d) => new { payload = System.Text.Encoding.UTF8.GetString (d) };

            await response.SendJsonResponseAsync (200, data, processor);
        }

        async Task AppendData (NameValueCollection segments, HttpListenerRequest request, HttpListenerResponse response)
        {
            var guid = GetRequiredArgument<Guid> (segments, "guid", Guid.TryParse);

            var channel = App.GetChannel (guid);
            if (channel == null)
                throw new HttpNotFoundException ();

            Action? whenPersisted = null;

            int commited = 0;

            var nowait = int.TryParse (request.Headers["X-Quiu-NoWait"], out var v) && v == 1;
            var wait = !nowait;

            List<Task>? waitTasks = null;
            if (wait)
            {
                whenPersisted = () => Interlocked.Increment(ref commited);
                waitTasks = new List<Task> ();
            }

            using (var sr = new StreamReader (request.InputStream))
            {
                int processed = 0;
                try
                {
                    string? input;
                    while ((input = sr.ReadLine ()) != null)
                    {
                        var data = System.Text.Encoding.UTF8.GetBytes (input);
                        var t = _wal.Enqueue ((channel, data), whenPersisted);
                        waitTasks?.Add (t!);

                        processed++;
                    }

                    if (wait)
                    {
                        var t = waitTasks!.Count == 1 ? waitTasks![0] : Task.WhenAll (waitTasks!);
                        await t;
                    }

                    var status = processed == commited ? 201 : 202;
                    await response.SendJsonResponseAsync (status, new { processed = processed, commited = commited, error = false });
                }
                catch (HttpListenerException ex)
                {
                    LogError (ex.Message);
                    await response.SendJsonResponseAsync (ex.ErrorCode, new { processed = processed, commited = commited, error = true, message = ex.Message });
                }
                catch (Exception ex)
                {
                    LogError (ex.Message);
                    await response.SendJsonResponseAsync (500, new { processed = processed, commited = commited, error = true, message = ex.Message });
                }
            }
        }

        async Task CreateChannel (NameValueCollection segments, HttpListenerRequest request, HttpListenerResponse response)
        {
            var args = GetBodyArguments (request);
            var guid = GetArgument<Guid> (args, "guid", Guid.Empty, Guid.TryParse);

            var channel = guid == Guid.Empty ? App.AddChannel () : App.AddChannel (guid);
            await response.SendJsonResponseAsync (201, new { guid = channel.Guid, error = false });
        }

        async Task DropChannel (NameValueCollection segments, HttpListenerRequest request, HttpListenerResponse response)
        {
            var guid = this.GetRequiredArgument<Guid> (segments, "guid", Guid.TryParse);
            var prune = this.GetArgument<bool> (request.QueryString, "prune", false, bool.TryParse);

            var channel = App.GetChannel (guid);
            if (channel == null)
                throw new HttpNotFoundException ();

            App.DropChannel (channel, pruneData: prune);

            await response.SendJsonResponseAsync (200, new { error = false });
        }

        public override void Dispose ()
        {
            _wal.Dispose ();
            base.Dispose ();
        }

        readonly WAL _wal;
    }
}