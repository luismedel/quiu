using System;
using System.IO;
using System.Data.SQLite;
using System.Data.Common;
using System.Linq;

namespace quiu
{
    public class Storage : IDisposable
    {
        public string Path => _path;

        public Storage(QuiuContext app, string path)
        {
            _app = app;
            _path = path;

            _conn = new SQLiteConnection ($@"Data Source={_path};");
            _conn.Open ();
        }

        public void Dispose ()
        {
            _conn.Dispose ();
        }

        public void Execute (string command, params object[] args)
        {
            using (var cmd = CreateCommand (command, args))
                cmd.ExecuteNonQuery ();
        }

        public async Task ExecuteAsync (string command, params object[] args)
        {
            using (var cmd = CreateCommand (command, args))
                await cmd.ExecuteNonQueryAsync (_app.CancellationToken);
        }

        public T? ExecuteScalar<T> (string command, params object[] args)
        {
            using (var cmd = CreateCommand (command, args))
            {
                var data = cmd.ExecuteScalar ();
                if (data is DBNull)
                    return default (T?);

                return (T?) Convert.ChangeType (data, typeof (T));
            }
        }

        public async Task<T?> ExecuteScalarAsync<T> (string command, params object[] args)
        {
            using (var cmd = CreateCommand (command, args))
            {
                var data = await cmd.ExecuteScalarAsync (_app.CancellationToken);
                if (data is DBNull)
                    return default (T?);

                return (T?) Convert.ChangeType (data, typeof (T));
            }
        }

        public DbDataReader ExecuteReader (string command, params object[] args)
        {
            using (var cmd = CreateCommand (command, args))
                return cmd.ExecuteReader ();
        }

        public async Task<DbDataReader> ExecuteReaderAsync (string command, params object[] args)
        {
            using (var cmd = CreateCommand (command, args))
                return await cmd.ExecuteReaderAsync (_app.CancellationToken);
        }

        public int Execute (int commandId, params object[] args) => GetCachedCommand (commandId, args).ExecuteNonQuery ();
        public async Task<int> ExecuteAsync (int commandId, params object[] args) => await GetCachedCommand (commandId, args).ExecuteNonQueryAsync (_app.CancellationToken);

        public DbDataReader ExecuteReader (int commandId, params object[] args) => GetCachedCommand (commandId, args).ExecuteReader ();
        public async Task<DbDataReader> ExecuteReaderAsync (int commandId, params object[] args) => await GetCachedCommand (commandId, args).ExecuteReaderAsync (_app.CancellationToken);

        public T? ExecuteScalar<T> (int commandId, params object[] args) => (T?) Convert.ChangeType(GetCachedCommand (commandId, args).ExecuteScalar (), typeof(T));
        public async Task<T?> ExecuteScalarAsync<T> (int commandId, params object[] args) => (T?) Convert.ChangeType (await GetCachedCommand (commandId, args).ExecuteScalarAsync (_app.CancellationToken), typeof (T));

        public int PrepareCommand (string commandText)
        {
            _cachedCommands.Add (CreateCommand (commandText));
            return _cachedCommands.Count;
        }

        SQLiteCommand GetCachedCommand (int commandId, params object[] args)
        {
            var result = _cachedCommands[commandId - 1];
            result.Parameters.Clear ();
            result.Parameters.AddRange (args.Select((arg, i) => new SQLiteParameter(i.ToString (), arg)).ToArray ());
            return result;
        }

        SQLiteCommand CreateCommand (string commandText, params object[] args)
        {
            var result = _conn.CreateCommand ();
            result.CommandText = commandText;
            if (args?.Length > 0)
                result.Parameters.AddRange (args.Select ((arg, i) => new SQLiteParameter (i.ToString (), arg)).ToArray ());
            return result;
        }

        readonly List<SQLiteCommand> _cachedCommands = new List<SQLiteCommand> ();
        readonly SQLiteConnection _conn;

        readonly QuiuContext _app;
        readonly string _path;
    }
}

