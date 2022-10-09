using System;
using System.Net;
using System.IO;
using System.Threading.Channels;
using System.Diagnostics;

namespace quiu.http
{
    public class QuiuAdminServer : HttpServer
    {
        public const int DEFAULT_PORT = 2781;

        public QuiuAdminServer (QuiuContext app)
            : base(app.Config.Get<string> ("admin_server_host")!, app.Config.Get<int>("admin_server_port", DEFAULT_PORT)!)
        {
            _app = app;

            _app.Shutdown += AppShutdown;

            RegisterRoute ("POST", "/channel/new", CreateChannel);
        }

        async void CreateChannel (Dictionary<string, string> args, HttpListenerRequest request, HttpListenerResponse response)
        {
            var channel = _app.AddChannel ();
            await SendJsonResponseAsync (response, 201, new { guid = channel.Guid });
        }

        private void AppShutdown (object? sender, EventArgs e)
        {
            Stop ();
        }

        [Conditional ("DEBUG")]
        void LogDebug (string message, params object[] args) => Logger.Debug ($"[QuiuAdminServer] {message}", args);
        void LogInfo (string message, params object[] args) => Logger.Info ($"[QuiuAdminServer] {message}", args);
        void LogWarning (string message, params object[] args) => Logger.Warning ($"[QuiuAdminServer] {message}", args);
        void LogError (string message, params object[] args) => Logger.Error ($"[QuiuAdminServer] {message}", args);

        readonly QuiuContext _app;
   }
}