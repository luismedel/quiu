using System;
using System.Diagnostics;

namespace quiu
{
    public class Channel
        : IDisposable
    {
        public Guid Guid { get; private set; }

        public string Name { get; set; } = string.Empty;

        public Storage Storage => _dataStorage;

        public Channel (QuiuContext app, Guid guid)
        {
            this.Guid = guid;

            _app = app;
            _dataStorage = this.InitDataStorage ();

            this.LoadMetadata ();
        }

        void LoadMetadata ()
        {
        }

        Storage InitDataStorage ()
        {
            this.LogInfo ("Initializing data storage...");

            _app.EnsurePathExists ("channels");
            var path = _app.GetFullPath ($"channels/{this.Guid.ToString ("N")}_data.db");

            this.LogInfo($"- {path}");

            var result = new Storage (_app, path);

            this.LogInfo (" - Creating database tables...");

            result.Execute (@"
            CREATE TABLE IF NOT EXISTS meta_t (
                guid TEXT,
                name TEXT,
                ttl INTEGER
            )");

            result.Execute (@"
            CREATE TABLE IF NOT EXISTS data_t (
                timestamp INTEGER,
                data BLOB
            )");

            this.LogInfo (" - Creating indices...");

            result.Execute (@"
            CREATE INDEX IF NOT EXISTS idx_channel_timestamp
            ON data_t(timestamp)");

            this.LogInfo (" - Preparing queries...");

            _insertCommand = result.PrepareCommand ("insert into data_t values (?, ?)");
            _selectCommand = result.PrepareCommand ("select timestamp, data from data_t where rowid = ? limit 1");
            _selectManyCommand = result.PrepareCommand ("select timestamp, data from data_t where rowid >= ? order by rowid asc limit ?");
            _upsertCommand = result.PrepareCommand ("insert or replace into data_t (timestamp, data) values (?, ?);");

            this.LogInfo ("Done.");

            return result;
        }

        public int Append (byte[] data) => _dataStorage.Execute(_insertCommand, DateTime.Now.Ticks, data);
        public async Task<int> AppendAsync (byte[] data) => await _dataStorage.ExecuteAsync (_insertCommand, DateTime.Now.Ticks, data);

        public Data Fetch (Int64 offset)
        {
            using (var rs = _dataStorage.ExecuteReader (_selectCommand, offset))
            {
                if (!rs.Read ())
                    return Data.Empty;

                return new Data((Int64) rs[0], (byte[]) rs[1]);
            }
        }

        public IEnumerable<Data> Fetch (Int64 offset, int count)
        {
            using (var rs = _dataStorage.ExecuteReader (_selectManyCommand, offset, count))
            {
                while (rs.Read ())
                    yield return new Data ((Int64) rs[0], (byte[]) rs[1]);
            }
        }

        public async Task<Data> FetchAsync (Int64 offset)
        {
            using (var rs = await _dataStorage.ExecuteReaderAsync (_selectCommand, offset))
            {
                if (!await rs.ReadAsync ())
                    return Data.Empty;

                return new Data ((Int64) rs[0], (byte[]) rs[1]);
            }
        }

        public async IAsyncEnumerable<Data> FetchAsync (Int64 offset, int count)
        {
            using (var rs = await _dataStorage.ExecuteReaderAsync (_selectManyCommand, offset, count))
            {
                while (await rs.ReadAsync ())
                    yield return new Data ((Int64) rs[0], (byte[]) rs[1]);
            }
        }

        [Conditional ("DEBUG")]
        void LogDebug (string message, params object[] args) => Logger.Debug ($"[#{this.Guid}] {message}", args);
        void LogInfo (string message, params object[] args) => Logger.Info ($"[#{this.Guid}] {message}", args);
        void LogWarning (string message, params object[] args) => Logger.Warning ($"[#{this.Guid}] {message}", args);
        void LogError (string message, params object[] args) => Logger.Error ($"[#{this.Guid}] {message}", args);

        public void Dispose ()
        {
            _dataStorage.Dispose ();
        }

        int _insertCommand;
        int _selectCommand;
        int _selectManyCommand;
        int _upsertCommand;

        readonly Storage _dataStorage;
        readonly QuiuContext _app;
    }
}
