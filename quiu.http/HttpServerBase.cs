﻿using System;
using System.Net;
using System.IO;
using System.Threading.Channels;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Collections.Specialized;
using quiu.core;
using YamlDotNet.Core.Tokens;

namespace quiu.http
{
    public abstract class HttpServerBase : IDisposable
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

        protected Context App => _app;
        protected CancellationToken CancellationToken => _cts.Token;

        protected delegate bool TypeConvertDelegate<T> (ReadOnlySpan<char> input, out T output);

        public string Endpoint { get; private set; }
        public bool IsRunning => _running;

        public HttpServerBase (Context app, string host, int port, CancellationToken cancellationToken)
        {
            this.Endpoint = $"http://{host}:{port}/";

            _app = app;
            _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            _listener = new HttpListener ();
            _listener.Prefixes.Add (this.Endpoint);

            _className = this.GetType ().Name;
        }

        public Task? RunLoop ()
        {
            if (_running)
                return null;

            _running = true;

            LogInfo ("Starting server...");
            _listener.Start ();

            LogInfo ($" - Listening on {this.Endpoint}");

            return InnerLoop ();
        }

        public virtual void Stop ()
        {
            if (!_running)
                return;

            _running = false;

            LogInfo ("Stopping server...");
            _listener.Stop ();

            if (_cts.IsCancellationRequested)
            {
                LogInfo ("Cancelling pending operations...");
                _cts.Cancel ();
            }

            LogInfo ("Cleaning up watchdog task...");
            _watchdogCts?.Cancel ();
            _watchdogCts?.Dispose ();
            _watchdogTask?.Dispose ();
        }

        async Task InnerLoop ()
        {
            // GetContextAsync does not allows cancellation.
            // We need to mimic it using a cancellable task to act as
            // a watchdog for external cancellation.
            _watchdogCts = CancellationTokenSource.CreateLinkedTokenSource (_cts.Token);
            _watchdogTask = Task.Delay (Timeout.Infinite, _watchdogCts.Token);

            try
            {
                while (_running && !_cts.IsCancellationRequested)
                {
                    try
                    {
                        var listenerTask = _listener.GetContextAsync ();

                        var completedTask = await Task.WhenAny (listenerTask, _watchdogTask);
                        if (completedTask == _watchdogTask)
                        {
                            LogDebug ("Watchdog task cancelled.");
                            break;
                        }

                        var ctx = listenerTask.Result;
                        LogTrace ($"Handling connection from {ctx.Request.RemoteEndPoint}...");
                        await HandleClientAsync (ctx);
                    }
                    catch (Exception ex)
                    {
                        LogError (ex.Message);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                LogInfo ("External cancellation detected");
            }
            catch (Exception ex)
            {
                LogError (ex.Message);
                throw;
            }
            finally
            {
                Stop ();
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

        async Task HandleClientAsync (HttpListenerContext ctx)
        {
            try
            {
                var tmp = GetRoute (ctx.Request.HttpMethod, ctx.Request.Url);
                if (tmp == null)
                {
                    LogTrace ($"No registered route found for {ctx.Request.Url}");

                    await SendResponseAsync (ctx.Response, 404);
                    return;
                }

                var match = tmp.Value;
                await Task.Run (() => match.Handler.Invoke (match.Arguments, ctx.Request, ctx.Response));
            }
            catch (HttpListenerException ex)
            {
                await SendJsonResponseAsync (ctx.Response, ex.ErrorCode, new { error = true, message = ex.Message });
            }
            catch (Exception ex)
            {
                await SendJsonResponseAsync (ctx.Response, 500, new { error = true, message = ex.Message });
            }
        }

        protected async Task SendResponseAsync (HttpListenerResponse response, int statusCode, string? content = null)
        {
            LogTrace ($" -> {statusCode}: {content}");

            response.StatusCode = statusCode;
            using (var sw = new StreamWriter (response.OutputStream))
            {
                sw.AutoFlush = true;
                await sw.WriteAsync ((content ?? String.Empty).ToCharArray (), this.CancellationToken);
            }
        }

        protected async Task SendResponseAsync (HttpListenerResponse response, int statusCode, IEnumerable<string> data)
        {
            response.StatusCode = statusCode;
            using (var sw = new StreamWriter (response.OutputStream))
            {
                sw.AutoFlush = false;
                foreach (var line in data)
                    await sw.WriteAsync (line.ToCharArray (), this.CancellationToken);
                sw.Flush ();
            }
        }

        protected async Task SendJsonResponseAsync (HttpListenerResponse response, int statusCode, object? data)
        {
            response.Headers.Add (HttpResponseHeader.ContentType, "application/json");
            await SendResponseAsync (response, statusCode, data != null ? System.Text.Json.JsonSerializer.Serialize (data) : string.Empty);
        }

        protected async Task SendJsonResponseAsync<T> (HttpListenerResponse response, int statusCode, IAsyncEnumerable<T> data, Func<T, object> selector)
        {
            response.Headers.Add (HttpResponseHeader.ContentType, "application/json");

            response.StatusCode = statusCode;
            using (var sw = new StreamWriter (response.OutputStream))
            {
                sw.AutoFlush = true;
                await foreach (var d in data)
                {
                    var line = System.Text.Json.JsonSerializer.Serialize (selector (d));
                    await sw.WriteLineAsync (line.ToCharArray (), this.CancellationToken);
                }
            }
        }

        public virtual void Dispose () => this.Stop ();

        [Conditional ("DEBUG")]
        protected void LogDebug (string message, params object[] args) => Logger.Debug ($"[{_className}] {message}", args);
        [Conditional ("TRACE")]
        protected void LogTrace (string message, params object[] args) => Logger.Trace ($"[{_className}] {message}", args);
        protected void LogInfo (string message, params object[] args) => Logger.Info ($"[{_className}] {message}", args);
        protected void LogWarning (string message, params object[] args) => Logger.Warning ($"[{_className}] {message}", args);
        protected void LogError (string message, params object[] args) => Logger.Error ($"[{_className}] {message}", args);

        bool _running;

        CancellationTokenSource? _watchdogCts;
        Task? _watchdogTask;

        readonly Context _app;
        readonly CancellationTokenSource _cts;
        readonly HttpListener _listener;
        readonly List<Route> _routes = new List<Route> ();

        readonly string _className;
    }
}