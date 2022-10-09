using System;
using System.Text.Json;

namespace quiu
{
    public static class Serializer
    {
        public static byte[] FromJson (JsonDocument json) => System.Text.Encoding.UTF8.GetBytes (JsonSerializer.Serialize (json));
        public static byte[] FromText (string text) => System.Text.Encoding.UTF8.GetBytes (text);

        public static JsonDocument ToJson (byte[] json) => JsonDocument.Parse (System.Text.Encoding.UTF8.GetString (json));
        public static string ToText (byte[]? text) => text != null ? System.Text.Encoding.UTF8.GetString (text) : String.Empty;
    }
}