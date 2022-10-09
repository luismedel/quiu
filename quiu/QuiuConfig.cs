using System;
using System.Text;
using System.Text.Json;

namespace quiu
{
    public class QuiuConfig
    {
        public object? this[string key]
        {
            get => _values.TryGetValue (key, out var result) ? result!.ToString() : null;
            set { if (value == null) _values.Remove (key); else _values[key] = value; }
        }

        public QuiuConfig () => _values = new Dictionary<string, object> (StringComparer.InvariantCultureIgnoreCase);
        public QuiuConfig (string path) : this () => Load (path);

        public T? Get<T> (string key, T? @default = default(T))
        {
            var value = this[key];

            return value != null ? (T) Convert.ChangeType (value, typeof (T))
                                 : @default;
        }

        public void Set<T> (string key, T? value) => this[key] = value == null ? null : value.ToString ();

        void Load (string path)
        {
            var json = System.IO.File.ReadAllText (path);

            var opts = new JsonSerializerOptions { ReadCommentHandling = JsonCommentHandling.Skip };
            var dict = JsonSerializer.Deserialize<Dictionary<string, string>> (json, opts);

            if (dict == null)
                return;

            foreach (var kv in dict)
                _values.Add (kv.Key, kv.Value);
        }

        readonly Dictionary<string, object> _values;
    }
}

