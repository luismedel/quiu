using System;
using System.Net;
using System.IO;

namespace quiu.server
{
    public abstract class Server : IDisposable
    {
        public bool IsRunning => _listener.IsListening;

        public Server ()
        {
            _listener = new HttpListener ();
            _listener.Prefixes.Add ("http://*:8080/");
        }

        public void Start ()
        {
            _listener.Start ();

            Spawn (ServerLoop);
            Spawn (HandlerLoop);
        }

        public void Stop ()
        {
            _listener.Stop ();
        }

        async void ServerLoop ()
        {
            while (_listener.IsListening)
            {
                try
                {
                    var ctx = await _listener.GetContextAsync ();
                    Console.WriteLine (ctx);
                    if (!TryProcessRequest (ctx))
                        EnqueueRequest (ctx);
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine (ex.Message);
                }
            }
        }

        async void HandlerLoop ()
        {
            while (_listener.IsListening)
            {
                if (_pendingRequests.Count == 0)
                {
                    await Task.Delay (100);
                    continue;
                }

                HttpListenerContext ctx;

                lock (_pendingRequests)
                {
                    if (_pendingRequests.Count == 0)
                        continue;

                    ctx = _pendingRequests.Dequeue ();
                }

                Spawn (() => HandleClient (ctx.Request, ctx.Response));
            }
        }

        void EnqueueRequest(HttpListenerContext ctx)
        {
            lock (_pendingRequests)
                _pendingRequests.Enqueue (ctx);
        }

        bool TryProcessRequest (HttpListenerContext ctx)
        {
            if (_runningTasks.Count == _runningTasks.Capacity)
                return false;

            lock(_runningTasks)
            {
                if (_runningTasks.Count == _runningTasks.Capacity)
                    return false;

                Spawn (() => HandleClient (ctx.Request, ctx.Response));
            }

            return true;
        }

        void Spawn (Action action)
        {
            lock (_runningTasks)
            {
                if (_runningTasks.Count == _runningTasks.Capacity)
                    _runningTasks.RemoveAll (t => t.IsCompleted || t.IsCompletedSuccessfully || t.IsCanceled || t.IsFaulted);

                var t = new Task (action);
                t.Start ();

                _runningTasks.Add (t);
            }
        }

        public abstract void HandleClient (HttpListenerRequest request, HttpListenerResponse response);

        public void Dispose ()
        {
            Stop ();
        }

        readonly List<Task> _runningTasks = new List<Task> (1024);
        readonly Queue<HttpListenerContext> _pendingRequests = new Queue<HttpListenerContext> ();
        readonly HttpListener _listener;
    }
}