using System;
using System.Diagnostics;
using Tenray.ZoneTree;
using Tenray.ZoneTree.Comparers;
using Tenray.ZoneTree.Serializers;

namespace quiu.core
{
    public class Channel
        : IDisposable
    {
        public Guid Guid { get; private set; }
        public string Name { get; set; } = string.Empty;
        public string StoragePath => _app.GetFullPath ($"channels/{this.Guid}");
        public Int64 LastOffset => _offset;

        public Channel (Context app, Guid guid)
        {
            this.Guid = guid;

            _app = app;

            LogInfo ("Initalizing storage...");

            var path = _app.EnsurePathExists (this.StoragePath);
            LogInfo ($" - {path}");

            _dataStorage = new ZoneTreeFactory<Int64, byte[]> ()
                            .SetComparer (new Int64ComparerAscending ())
                            .SetDataDirectory (path)
                            .SetKeySerializer (new Int64Serializer ())
                            .SetValueSerializer (new ByteArraySerializer ())
                            .OpenOrCreate ();

            LogInfo ("Done.");

            this.LoadMetadata ();
        }

        void LoadMetadata ()
        {
            LogInfo ("Getting last offset...");
            //var keys = _dataStorage.Keys;
            //_offset = (keys.Count () == 0) ? 0 : (keys.Max () + 1);
            LogInfo ($" - Last offset: {_offset}");
        }

        public void Append (byte[] data) => _dataStorage.Upsert (Interlocked.Increment (ref _offset), data);
        
        public byte[]? Fetch (Int64 offset) => _dataStorage.TryGet (offset, out var result) ? result : null;

        public IEnumerable<byte[]> Fetch (Int64 offset, int count)
        {
            for (var i = 0; i < count; i++)
            {
                if (!_dataStorage.TryGet (offset++, out var item))
                    break;

                yield return item;
            }
        }

        public void PruneData ()
        {
            try
            {
                Directory.Delete (this.StoragePath, true);
            }
            catch (Exception ex)
            {
                LogWarning ($"Cant prune data for channel {this.Guid}: {ex.Message}");
            }
        }

        public void Dispose ()
        {
            _dataStorage.Dispose ();
        }

        [Conditional ("DEBUG")]
        void LogDebug (string message, params object[] args) => Logger.Debug ($"[#{this.Guid}] {message}", args);
        void LogInfo (string message, params object[] args) => Logger.Info ($"[#{this.Guid}] {message}", args);
        void LogWarning (string message, params object[] args) => Logger.Warning ($"[#{this.Guid}] {message}", args);
        void LogError (string message, params object[] args) => Logger.Error ($"[#{this.Guid}] {message}", args);

        public override string ToString () => this.Guid.ToString ();

        Int64 _offset = 0;

        readonly IZoneTree<Int64, byte[]> _dataStorage;
        readonly Context _app;
    }
}
