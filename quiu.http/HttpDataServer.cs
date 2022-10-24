using System;
using System.Net;
using System.IO;
using System.Threading.Channels;
using System.Diagnostics;

using quiu.core;

namespace quiu.http
{
    public class HttpDataServer : HttpServerBase
    {
        public const string DEFAULT_HOST = "localhost";
        public const int DEFAULT_PORT = 27812;

        public HttpDataServer (Context app, string host, int port, CancellationToken cancellationToken)
            : base (app, host, port, cancellationToken)
        {
            RegisterRoute ("POST", "/channel/%guid", AppendData);
            RegisterRoute ("GET", "/channel/%guid/%offset", GetItem);
            RegisterRoute ("GET", "/channel/%guid/%offset/%count", GetItems);
        }

        public HttpDataServer (Context app)
            : this (app,
                    app.Config.Get<string> ("server_host", DEFAULT_HOST)!,
                    app.Config.Get<int>("server_port", DEFAULT_PORT)!,
                    app.CancellationToken)
        {
        }

        void GetItem(Dictionary<string, string> args, HttpListenerRequest request, HttpListenerResponse response)
        {
            var guid = GetRequiredUrlArgument<Guid> (args, "guid", Guid.TryParse);
            Int64 offset = GetRequiredUrlArgument<Int64> (args, "offset", Int64.TryParse);

            var channel = App.GetChannel (guid);
            if (channel == null)
                throw new HttpNotFoundException ();

            var data = channel.Fetch (offset);

            SendJsonResponse (response, 200, new { payload = System.Text.Encoding.UTF8.GetString (data!) });
        }

        void GetItems (Dictionary<string, string> args, HttpListenerRequest request, HttpListenerResponse response)
        {
            var guid = GetRequiredUrlArgument<Guid> (args, "guid", Guid.TryParse);
            Int64 offset = GetRequiredUrlArgument<Int64> (args, "offset", Int64.TryParse);
            int count = GetRequiredUrlArgument<int> (args, "count", int.TryParse);

            var channel = App.GetChannel (guid);
            if (channel == null)
                throw new HttpNotFoundException ();

            var data = channel.Fetch (offset, count);
            var processor = (byte[] d) => new { payload = System.Text.Encoding.UTF8.GetString (d) };

            SendJsonResponse (response, 200, data, processor);
        }

        void AppendData (Dictionary<string, string> args, HttpListenerRequest request, HttpListenerResponse response)
        {
            var guid = GetRequiredUrlArgument<Guid> (args, "guid", Guid.TryParse);

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
   }
}