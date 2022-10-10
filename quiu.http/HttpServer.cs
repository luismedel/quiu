using System;
using System.Net;
using System.IO;
using System.Threading.Channels;
using System.Diagnostics;
using System.Text.RegularExpressions;
using static quiu.http.QuiuAdminServer;
using System.Collections.Specialized;

namespace quiu.http
{
    public abstract class HttpServer : IDisposable
    {
        struct Route
        {
            public string Method;
            public Regex Regex;
            public string[] ArgNames;
            public Action<Dictionary<string, string>, HttpListenerRequest, HttpListenerResponse> Handler;
        }

        struct RouteMatch
        {
            public Dictionary<string, string> Arguments;
            public Action<Dictionary<string, string>, HttpListenerRequest, HttpListenerResponse> Handler;
        }

        protected delegate bool TypeConvertDelegate<T> (ReadOnlySpan<char> input, out T output);

        public string Endpoint { get; private set; }
        public bool IsRunning => _listener.IsListening;

        public HttpServer (string host, int port)
        {
            this.Endpoint = $"http://{host}:{port}/";

            _listener = new HttpListener ();
            _listener.Prefixes.Add (this.Endpoint);
        }

        public virtual void Start ()
        {
            _listener.Start ();

            RegisterTask (ServerLoop ());
        }

        public virtual void Stop ()
        {
            _listener.Stop ();
        }

        async Task ServerLoop ()
        {
            while (_listener.IsListening)
            {
                try
                {
                    var ctx = await _listener.GetContextAsync ();
                    await HandleClient (ctx.Request, ctx.Response);
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine (ex.Message);
                }
            }
        }

        void RegisterTask (Task task)
        {
            lock (_runningTasks)
            {
                if (_runningTasks.Count == _runningTasks.Capacity)
                    _runningTasks.RemoveAll (t => t.IsCompleted || t.IsCompletedSuccessfully || t.IsCanceled || t.IsFaulted);

                _runningTasks.Add (task);
            }
        }

        public void RegisterRoute (string method, string pattern, Action<Dictionary<string, string>, HttpListenerRequest, HttpListenerResponse> handler, bool optionalEndSlash = true)
        {
            const string ARG_PATTERN = @"%([^/]+)";

            var argNames = Regex.Matches (pattern, ARG_PATTERN)
                                .Select (m => m.Groups[1].Value)
                                .ToArray ();

            var patternSuffix = optionalEndSlash ? "/?" : String.Empty;
            pattern = Regex.Replace ($"^{Regex.Escape (pattern)}{patternSuffix}$", ARG_PATTERN, @"([^/]+)");

            _routes.Add (new Route { Method = method, Regex = new Regex (pattern), ArgNames = argNames, Handler = handler });
        }

        RouteMatch? GetRoute (string method, Uri? uri)
        {
            if (uri == null)
                return null;

            foreach (var r in _routes)
            {
                if (!r.Method.Equals (method, StringComparison.InvariantCultureIgnoreCase))
                    continue;

                var m = r.Regex.Match (uri.AbsolutePath);
                if (!m.Success)
                    continue;

                Dictionary<string, string> args = new Dictionary<string, string> (StringComparer.InvariantCultureIgnoreCase);
                for (int i = 1; i < m.Groups.Count; i++)
                    args[r.ArgNames[i - 1]] = m.Groups[i].Value;

                return new RouteMatch { Arguments = args, Handler = r.Handler };
            }

            return null;
        }

        T GetArgument<T> (string key, string? value, T @default, TypeConvertDelegate<T>? convertfn = null)
        {
            if (string.IsNullOrEmpty (value))
                return @default;

            if (convertfn == null)
                return (T) Convert.ChangeType (value, typeof (T));

            if (!convertfn.Invoke (value, out T converted))
                throw new HttpRequiredParamException ($"Invalid value for '{key}'");

            return converted;
        }

        T GetRequiredArgument<T> (string key, string? value, TypeConvertDelegate<T>? convertfn = null)
        {
            if (string.IsNullOrEmpty (value))
                throw new HttpRequiredParamException ($"Missing value for '{key}'");

            if (convertfn == null)
                return (T) Convert.ChangeType (value, typeof (T));

            if (!convertfn.Invoke (value, out T converted))
                throw new HttpRequiredParamException ($"Invalid value for '{key}'");

            return converted;
        }

        protected T GetUrlArgument<T> (Dictionary<string, string> args, string key, T @default, TypeConvertDelegate<T>? convertfn = null)
        {
            args.TryGetValue (key, out var value);
            return GetArgument<T> (key, value, @default, convertfn);
        }

        protected T GetRequiredUrlArgument<T> (Dictionary<string, string> args, string key, TypeConvertDelegate<T>? convertfn = null)
        {
            args.TryGetValue (key, out var value);
            return GetRequiredArgument<T> (key, value, convertfn);
        }

        protected T GetQueryArgument<T> (NameValueCollection args, string key, T @default, TypeConvertDelegate<T>? convertfn = null)
        {
            return GetArgument<T> (key, args[key], @default, convertfn);
        }

        protected T GetRequiredQueryArgument<T> (NameValueCollection args, string key, TypeConvertDelegate<T>? convertfn = null)
        {
            return GetRequiredArgument<T> (key, args[key], convertfn);
        }

        async Task HandleClient (HttpListenerRequest request, HttpListenerResponse response)
        {
            try
            {
                var tmp = GetRoute (request.HttpMethod, request.Url);
                if (tmp == null)
                {
                    await SendResponseAsync (response, 404);
                    return;
                }

                var match = tmp.Value;
                await Task.Run (() => match.Handler.Invoke (match.Arguments, request, response));
            }
            catch (HttpListenerException ex)
            {
                await SendJsonResponseAsync (response, ex.ErrorCode, new { error = true, message = ex.Message });
            }
            catch (Exception ex)
            {
                await SendJsonResponseAsync (response, 500, new { error = true, message = ex.Message });
            }
        }

        protected async Task SendResponseAsync (HttpListenerResponse response, int statusCode, string? content = null)
        {
            response.StatusCode = statusCode;
            if (content != null)
            {
                using (var sw = new StreamWriter (response.OutputStream))
                {
                    sw.AutoFlush = true;
                    await sw.WriteAsync (content);
                }
            }
        }

        protected async Task SendResponseAsync (HttpListenerResponse response, int statusCode, IEnumerable<string> data)
        {
            response.StatusCode = statusCode;
            using (var sw = new StreamWriter (response.OutputStream))
            {
                sw.AutoFlush = false;
                foreach (var s in data)
                    await sw.WriteAsync (s);
                sw.Flush ();
            }
        }

        protected async Task SendJsonResponseAsync (HttpListenerResponse response, int statusCode, object data)
        {
            response.Headers.Add (HttpResponseHeader.ContentType, "application/json");
            await SendResponseAsync (response, statusCode, System.Text.Json.JsonSerializer.Serialize (data));
        }

        protected async Task SendJsonResponseAsync<T> (HttpListenerResponse response, int statusCode, IAsyncEnumerable<T> data, Func<T, object> selector)
        {
            response.Headers.Add (HttpResponseHeader.ContentType, "application/json");

            response.StatusCode = statusCode;
            using (var sw = new StreamWriter (response.OutputStream))
            {
                sw.AutoFlush = true;
                await foreach (var d in data)
                    await sw.WriteLineAsync (System.Text.Json.JsonSerializer.Serialize(selector(d)));
            }
        }

        public virtual void Dispose ()
        {
            Stop ();
        }

        [Conditional ("DEBUG")]
        void LogDebug (string message, params object[] args) => Logger.Debug ($"[HttpServer] {message}", args);
        void LogInfo (string message, params object[] args) => Logger.Info ($"[HttpServer] {message}", args);
        void LogWarning (string message, params object[] args) => Logger.Warning ($"[HttpServer] {message}", args);
        void LogError (string message, params object[] args) => Logger.Error ($"[HttpServer] {message}", args);

        readonly List<Route> _routes = new List<Route> ();

        readonly List<Task> _runningTasks = new List<Task> ();
        readonly HttpListener _listener;
    }
}