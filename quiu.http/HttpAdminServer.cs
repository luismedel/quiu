using System;
using System.Net;
using System.IO;
using System.Threading.Channels;
using System.Diagnostics;

using quiu.core;

namespace quiu.http
{
    public class HttpAdminServer : HttpServerBase
    {
        public const string DEFAULT_HOST = "localhost";
        public const int DEFAULT_PORT = 2781;

        public HttpAdminServer (Context app, string host, int port, CancellationToken cancellationToken)
            : base (app, host, port, cancellationToken)
        {
            RegisterRoute ("POST", "/channel/new", CreateChannel);
            RegisterRoute ("DELETE", "/channel/%guid", DropChannel);
        }

        public HttpAdminServer (Context app)
            : this (app,
                    app.Config.Get<string> ("admin_server_host", "localhost")!,
                    app.Config.Get<int>("admin_server_port", DEFAULT_PORT)!,
                    app.CancellationToken)
        {
        }

        void CreateChannel (Dictionary<string, string> args, HttpListenerRequest request, HttpListenerResponse response)
        {
            var channel = App.AddChannel ();
            SendJsonResponse (response, 201, new { guid = channel.Guid });
        }

        void DropChannel (Dictionary<string, string> args, HttpListenerRequest request, HttpListenerResponse response)
        {
            var guid = this.GetRequiredUrlArgument<Guid> (args, "guid", Guid.TryParse);
            var prune = this.GetQueryArgument<bool> (request.QueryString, "prune", false, bool.TryParse);

            var channel = App.GetChannel (guid);
            if (channel == null)
                throw new HttpNotFoundException ();

            App.DropChannel (channel, pruneData: prune);

            SendJsonResponse (response, 200, string.Empty);
        }
    }
}