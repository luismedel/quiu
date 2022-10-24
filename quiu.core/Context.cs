using System;
using System.Runtime.Loader;
using System.Threading;
using System.Threading.Channels;

namespace quiu.core
{
    public class Context: IDisposable
    {
        const int DEFAULT_RUNNING_TASKS_THRESHOLD = 1024;
        static readonly int DEFAULT_SHUTDOWN_WAIT = 2000;

        const string DEFAULT_DATADIR = "$HOME/var/quiu/";

        public Config Config => _config;
        public Task[] RunningTasks => _runningTasks.Where (t => !t.IsCompleted).ToArray ();

        public CancellationToken CancellationToken => _cts.Token;

        public string IndexFilePath { get; private set; }
        public string DataDirectory { get; private set; }

        public Channel[] Channels => _channels.Values.ToArray ();

        public Context(Config config)
        {
            _config = config;
            _cts = new CancellationTokenSource ();

            _runningTasksThreshold = config.Get<int> ("running_tasks_threshold", DEFAULT_RUNNING_TASKS_THRESHOLD);

            DataDirectory = _config.Get<string> ("data_dir", DEFAULT_DATADIR)!;
            IndexFilePath = Path.Combine (DataDirectory, "channels.index");

            if (_config.Get<bool> ("autorecover_channels", true))
                this.RecoverChannels ();
        }

        void RecoverChannels ()
        {
            Logger.Info ($"Reading channel index from {IndexFilePath}...");
            if (!File.Exists (IndexFilePath))
            {
                Logger.Info ($" - File does not exist. Skipping...");
                return;
            }

            foreach (var line in File.ReadAllLines(IndexFilePath))
            {
                var parts = line.Split (';', 2);
                if (!Guid.TryParse (parts[0], out var guid))
                    continue;

                Logger.Info ($"Recovering channel {guid}...");
                AddChannelInternal (guid, _channels);
                Logger.Info ($"Done.");
            }
        }

        void WriteIndex ()
        {
            Logger.Info ($"Writing channel index to {IndexFilePath}...");
            File.WriteAllLines (IndexFilePath, _channels.Values.Select (c => $"{c.Guid};{c.Name}"));
            Logger.Info ("Done.");
        }

        public string GetFullPath (string relativePath) => Path.GetFullPath (Path.Combine (DataDirectory, relativePath));

        public string EnsurePathExists (string relativePath)
        {
            var path = Path.GetFullPath (Path.Combine (DataDirectory, relativePath));

            Logger.Info($"Ensuring {path} exists...");

            var exists = Directory.CreateDirectory (path).Exists;
            Logger.Info ($" - {path} exists? {exists}");

            return path;
        }

        public Channel AddChannelInternal (Guid guid, Dictionary<Guid, Channel> dest)
        {
            if (dest.TryGetValue (guid, out var existing))
            {
                Logger.Warning ($" - Channel {guid} already exists.");
                return existing;
            }

            var result = new Channel (this, guid);
            dest.Add (guid, result);

            return result;
        }

        public Channel AddChannel (Guid guid)
        {
            Channel result;

            Logger.Info ($"Creating channel {guid}...");
            lock (_channels)
            {
                result = AddChannelInternal (guid, _channels);

                var index = _channels.Values.Select (c => $"{c.Guid};{c.Name}");
                File.WriteAllLines (IndexFilePath, index);
            }
            Logger.Info ($"Done.");

            return result;
        }

        public Channel AddChannel () => AddChannel (Guid.NewGuid());

        public Channel? GetChannel (Guid guid) => _channels.TryGetValue(guid, out var result) ? result : null;

        public bool DropChannel (Channel channel, bool pruneData = false)
        {
            lock (_channels)
            {
                if (!_channels.Remove (channel.Guid))
                    return false;
            }

            channel.Dispose ();

            try
            {
                if (pruneData)
                    channel.PruneData();
            }
            catch (IOException)
            {
                return false;
            }

            return true;
        }

        public void EnqueueTask (Task? task, string taskName)
        {
            if (task == null || task.IsCompleted)
                return;

            lock (_runningTasks)
            {
                Logger.Trace ($"Enqueuing task '{taskName}'.");
                _runningTasks.Add (task);

                if (_runningTasks.Count >= _runningTasksThreshold)
                {
                    int removed = _runningTasks.RemoveAll (t => t.IsCompleted);

                    if (removed > 0)
                        Logger.Info ($"Removed {removed} completed tasks.");
                    else
                        Logger.Warning ($"{_runningTasks.Count} running (threshold={_runningTasksThreshold})");
                }
            }
        }

        public void ShutDown ()
        {
            if (_shutDown)
                return;

            Logger.Info ("Shutting down...");
            _shutDown = true;

            lock (_channels)
            {
                var channels = _channels.Values;
                if (channels.Count > 0)
                {
                    Logger.Info ($"Releasing {channels.Count} channels...");
                    foreach (var chn in channels)
                    {
                        Logger.Info ($" - {chn.Guid}");
                        chn.Dispose ();
                    }

                    _channels.Clear ();
                }
            }

            this.WaitTasks ();

            if (!_cts.IsCancellationRequested)
            {
                Logger.Info ("Requesting any remaining task cancellation...");
                _cts.Cancel ();
            }
        }

        void WaitTasks ()
        {
            var tasks = this.RunningTasks;

            if (tasks.Length == 0)
                return;

            var timeout = this.Config.Get<int> ("shutdown_wait", DEFAULT_SHUTDOWN_WAIT);

            Logger.Info ($"Waiting for {tasks.Length} task(s) to finish before {timeout}ms...");
            try { Task.WaitAll (tasks, timeout); }
            catch (OperationCanceledException) { }
        }

        public void Dispose () => Task.Run (() => this.ShutDown ()).GetAwaiter().GetResult();

        bool _shutDown = false;

        readonly Config _config;
        readonly CancellationTokenSource _cts;

        readonly int _runningTasksThreshold;

        readonly object _indexLock = new object ();

        readonly List<Task> _runningTasks = new List<Task> ();
        readonly Dictionary<Guid, Channel> _channels = new Dictionary<Guid, Channel> ();
    }
}

