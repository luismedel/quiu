using System;
using System.Net;
using System.IO;
using System.Threading.Channels;
using System.Diagnostics;

namespace quiu.http
{
    public class QuiuServer : HttpServer
    {
        public const int DEFAULT_PORT = 27812;

        public QuiuServer (QuiuContext app)
            : base(app.Config.Get<string> ("server_host")!, app.Config.Get<int>("server_port", DEFAULT_PORT)!)
        {
            _app = app;

            _app.Shutdown += AppShutdown;

            RegisterRoute ("POST", "/channel/%guid", AppendData);
            RegisterRoute ("GET", "/channel/%guid/%offset", GetItem);
            RegisterRoute ("GET", "/channel/%guid/%offset/%count", GetItems);
        }

        async void GetItem(Dictionary<string, string> args, HttpListenerRequest request, HttpListenerResponse response)
        {
            var guid = GetRequiredUrlArgument<Guid> (args, "guid", Guid.TryParse);
            Int64 offset = GetRequiredUrlArgument<Int64> (args, "offset", Int64.TryParse);

            var channel = _app.GetChannel (guid);
            if (channel == null)
            {
                await SendResponseAsync (response, 404);
                return;
            }

            var data = await channel.FetchAsync (offset);

            await SendJsonResponseAsync (response, 200, new { timestamp = new DateTime(data.Timestamp).ToString("u"), payload = Serializer.ToText(data.Value) });
        }

        async void GetItems (Dictionary<string, string> args, HttpListenerRequest request, HttpListenerResponse response)
        {
            var guid = GetRequiredUrlArgument<Guid> (args, "guid", Guid.TryParse);
            Int64 offset = GetRequiredUrlArgument<Int64> (args, "offset", Int64.TryParse);
            int count = GetRequiredUrlArgument<int> (args, "count", int.TryParse);

            var channel = _app.GetChannel (guid);
            if (channel == null)
            {
                await SendResponseAsync (response, 404);
                return;
            }

            var data = channel.FetchAsync (offset, count);
            var processor = (Data d) => new { timestamp = new DateTime (d.Timestamp).ToString ("u"), payload = Serializer.ToText (d.Value) };

            await SendJsonResponseAsync (response, 200, data, processor);
        }

        async void AppendData (Dictionary<string, string> args, HttpListenerRequest request, HttpListenerResponse response)
        {
            var guid = GetRequiredUrlArgument<Guid> (args, "guid", Guid.TryParse);

            var channel = _app.GetChannel (guid);
            if (channel == null)
            {
                await SendResponseAsync (response, 404);
                return;
            }

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

        private void AppShutdown (object? sender, EventArgs e)
        {
            Stop ();
        }

        [Conditional ("DEBUG")]
        void LogDebug (string message, params object[] args) => Logger.Debug ($"[QuiuServer] {message}", args);
        void LogInfo (string message, params object[] args) => Logger.Info ($"[QuiuServer] {message}", args);
        void LogWarning (string message, params object[] args) => Logger.Warning ($"[QuiuServer] {message}", args);
        void LogError (string message, params object[] args) => Logger.Error ($"[QuiuServer] {message}", args);

        readonly QuiuContext _app;
   }
}