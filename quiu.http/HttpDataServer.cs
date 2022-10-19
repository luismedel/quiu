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

        async void GetItem(Dictionary<string, string> args, HttpListenerRequest request, HttpListenerResponse response)
        {
            var guid = GetRequiredUrlArgument<Guid> (args, "guid", Guid.TryParse);
            Int64 offset = GetRequiredUrlArgument<Int64> (args, "offset", Int64.TryParse);

            var channel = App.GetChannel (guid);
            if (channel == null)
                throw new HttpNotFoundException ();

            var data = await channel.FetchAsync (offset);

            await SendJsonResponseAsync (response, 200, new { timestamp = new DateTime(data.Timestamp).ToString("u"), payload = Serializer.ToText(data.Value) });
        }

        async void GetItems (Dictionary<string, string> args, HttpListenerRequest request, HttpListenerResponse response)
        {
            var guid = GetRequiredUrlArgument<Guid> (args, "guid", Guid.TryParse);
            Int64 offset = GetRequiredUrlArgument<Int64> (args, "offset", Int64.TryParse);
            int count = GetRequiredUrlArgument<int> (args, "count", int.TryParse);

            var channel = App.GetChannel (guid);
            if (channel == null)
                throw new HttpNotFoundException ();

            var data = channel.FetchAsync (offset, count);
            var processor = (Data d) => new { timestamp = new DateTime (d.Timestamp).ToString ("u"), payload = Serializer.ToText (d.Value) };

            await SendJsonResponseAsync (response, 200, data, processor);
        }

        async void AppendData (Dictionary<string, string> args, HttpListenerRequest request, HttpListenerResponse response)
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
                    while ((input = await sr.ReadLineAsync ()) != null)
                    {
                        await channel.AppendAsync (System.Text.Encoding.UTF8.GetBytes (input));
                        processed++;
                    }

                    await SendJsonResponseAsync (response, 201, new { processed = processed, error = false });
                }
                catch (Exception ex)
                {
                    await SendJsonResponseAsync (response, 255, new { processed = processed, error = true, description = ex.Message });
                }
            }
        }
   }
}