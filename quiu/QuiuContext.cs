using System;
using System.Runtime.Loader;
using System.Threading.Channels;

namespace quiu
{
    public class QuiuContext: IDisposable
    {
        //const string DEFAULT_DATADIR = "/var/quiu/";
        const string DEFAULT_DATADIR = "/Users/luis/var/quiu/";

        public QuiuConfig Config => _config;

        public string DataDirectory { get; private set; }

        public event EventHandler? Shutdown;

        public CancellationToken CancellationToken => _cts.Token;

        public QuiuContext(QuiuConfig config)
        {
            _config = config;
            _cts = new CancellationTokenSource ();

            DataDirectory = _config.Get ("data_dir", DEFAULT_DATADIR)!;

            this.Initialize ();
        }

        public void Initialize ()
        {
            AssemblyLoadContext.Default.Unloading += OnAssemblyUnloading;
        }

        void OnShutdown ()
        {
            if (_shuttingDown)
                return;

            _shuttingDown = true;

            Logger.Info ("Shutting down...");
            AssemblyLoadContext.Default.Unloading -= OnAssemblyUnloading;

            _cts.Cancel ();

            this.Shutdown?.Invoke (this, EventArgs.Empty);
            Logger.Info ("Done.");
        }

        void OnAssemblyUnloading (AssemblyLoadContext obj)
        {
            OnShutdown ();
        }

        public string GetFullPath (string relativePath) => Path.GetFullPath (Path.Combine (DataDirectory, relativePath));

        public bool EnsurePathExists (string relativePath)
        {
            Logger.Info($"Ensuring {relativePath} exists...");
            var path = Path.GetFullPath (Path.Combine (DataDirectory, relativePath));
            var result = Directory.CreateDirectory (path).Exists;
            Logger.Info ($" - {path}: {result}");

            return result;
        }

        public Channel AddChannel (Guid guid)
        {
            Logger.Info ($"Creating channel {guid}...");
            if (_channels.TryGetValue(guid , out _))
                throw new InvalidOperationException ($"Channel {guid} already exists.");

            var result = new Channel (this, guid);
            _channels.Add (guid, result);

            Logger.Info ($"Done.");
            return result;
        }

        public Channel AddChannel () => AddChannel (Guid.NewGuid());

        public Channel? GetChannel (Guid guid) => _channels.TryGetValue(guid, out var result) ? result : null;

        public bool DropChannel (Channel channel, bool pruneData = false)
        {
            if (!_channels.Remove (channel.Guid))
                return false;

            channel.Dispose ();

            try
            {
                if (pruneData)
                    File.Delete (channel.Storage.Path);
            }
            catch (IOException)
            {
                return false;
            }

            return true;
        }

        public void Dispose ()
        {
            OnShutdown ();

            _cts.Dispose ();

            foreach (var chn in _channels.Values)
                chn.Dispose ();
            _channels.Clear ();
        }

        bool _shuttingDown = false;

        readonly QuiuConfig _config;
        readonly CancellationTokenSource _cts;

        readonly Dictionary<Guid, Channel> _channels = new Dictionary<Guid, Channel> ();
    }
}

