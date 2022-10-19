using System;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace quiu.core
{
    public class Config
    {
        public object? this[string key]
        {
            get => _values.TryGetValue (key, out var result) ? result!.ToString() : null;
            set { if (value == null) _values.Remove (key); else _values[key] = value; }
        }

        public Config () => _values = new Dictionary<string, object> (StringComparer.InvariantCultureIgnoreCase);
        public Config (string path) : this () => Load (path);

        public T? Get<T> (string key, T? @default = default(T))
        {
            if (typeof (T) == typeof (string))
                return (T?) Convert.ChangeType (this.Get (key, @default as string), typeof (T));

            var value = this[key];

            return value != null ? (T) Convert.ChangeType (value, typeof (T))
                                 : @default;
        }

        public string? Get (string key, string? @default = null) => ExpandEnvironmentVariables (this[key] as string ?? @default);

        public void Set<T> (string key, T? value) => this[key] = value == null ? null : value.ToString ();

        void Load (string path)
        {
            var json = System.IO.File.ReadAllText (path);

            var opts = new JsonSerializerOptions { ReadCommentHandling = JsonCommentHandling.Skip };
            var dict = JsonSerializer.Deserialize<Dictionary<string, object>> (json, opts);

            if (dict == null)
                return;

            foreach (var kv in dict)
                _values.Add (kv.Key, kv.Value);
        }

        public void UpdateWith(Config other)
        {
            foreach (var kv in other._values)
                _values.Add (kv.Key, kv.Value);
        }

        public static string? ExpandEnvironmentVariables (string? sval)
        {
            if (string.IsNullOrEmpty (sval))
                return sval;

            return _renv.Replace (sval, m => Environment.GetEnvironmentVariable (m.Groups[1].Value) ?? String.Empty);
        }

        readonly Dictionary<string, object> _values;

        static readonly Regex _renv = new Regex (@"\$([a-zA-Z-_][a-zA-Z0-9-_]*)", RegexOptions.Compiled);
    }
}

