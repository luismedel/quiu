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
        }

        public HttpServer (Context app)
            : this (app,
                    app.Config.Get<string> ("server_host", DEFAULT_HOST)!,
                    app.Config.Get<int>("server_port", DEFAULT_PORT)!,
                    app.CancellationToken)
        {
        }

        void GetItem(NameValueCollection segments, HttpListenerRequest request, HttpListenerResponse response)
        {
            var guid = GetRequiredArgument<Guid> (segments, "guid", Guid.TryParse);
            Int64 offset = GetRequiredArgument<Int64> (segments, "offset", Int64.TryParse);

            var channel = App.GetChannel (guid);
            if (channel == null)
                throw new HttpNotFoundException ();

            var data = channel.Fetch (offset);

            SendJsonResponse (response, 200, new { payload = System.Text.Encoding.UTF8.GetString (data!) });
        }

        void GetItems (NameValueCollection segments, HttpListenerRequest request, HttpListenerResponse response)
        {
            var guid = GetRequiredArgument<Guid> (segments, "guid", Guid.TryParse);
            Int64 offset = GetRequiredArgument<Int64> (segments, "offset", Int64.TryParse);
            int count = GetRequiredArgument<int> (segments, "count", int.TryParse);

            var channel = App.GetChannel (guid);
            if (channel == null)
                throw new HttpNotFoundException ();

            var data = channel.Fetch (offset, count);
            var processor = (byte[] d) => new { payload = System.Text.Encoding.UTF8.GetString (d) };

            SendJsonResponse (response, 200, data, processor);
        }

        void AppendData (NameValueCollection segments, HttpListenerRequest request, HttpListenerResponse response)
        {
            var guid = GetRequiredArgument<Guid> (segments, "guid", Guid.TryParse);

            var channel = App.GetChannel (guid);
            if (channel == null)
                throw new HttpNotFoundException ();

            using (var sr = new StreamReader (request.InputStream))
            {
                int processed = 0;

                try
                {
                    string? input;
                    while ((input = sr.ReadLine ()) != null)
                    {
                        channel.Append (System.Text.Encoding.UTF8.GetBytes (input));
                        processed++;
                    }

                    SendJsonResponse (response, 201, new { processed = processed, error = false });
                }
                catch (Exception ex)
                {
                    SendJsonResponse (response, 255, new { processed = processed, error = true, description = ex.Message });
                }
            }
        }

        void CreateChannel (NameValueCollection segments, HttpListenerRequest request, HttpListenerResponse response)
        {
            var args = GetBodyArguments (request);
            var guid = GetArgument<Guid> (args, "guid", Guid.Empty, Guid.TryParse);

            var channel = guid == Guid.Empty ? App.AddChannel () : App.AddChannel (guid);
            SendJsonResponse (response, 201, new { guid = channel.Guid });
        }

        void DropChannel (NameValueCollection segments, HttpListenerRequest request, HttpListenerResponse response)
        {
            var guid = this.GetRequiredArgument<Guid> (segments, "guid", Guid.TryParse);
            var prune = this.GetArgument<bool> (request.QueryString, "prune", false, bool.TryParse);

            var channel = App.GetChannel (guid);
            if (channel == null)
                throw new HttpNotFoundException ();

            App.DropChannel (channel, pruneData: prune);

            SendJsonResponse (response, 200, string.Empty);
        }
    }
}