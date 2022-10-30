using System;
using System.Net;

namespace quiu.http
{
    static class HttpListenerResponseExtensions
    {
        public static async Task SendResponseAsync (this HttpListenerResponse response, int statusCode, string? content = null)
        {
            response.StatusCode = statusCode;
            using (var sw = new StreamWriter (response.OutputStream))
            {
                sw.AutoFlush = true;
                await sw.WriteAsync ((content ?? String.Empty).ToCharArray ());
            }
        }

        public static async Task SendResponseAsync (this HttpListenerResponse response, int statusCode, IEnumerable<string> data)
        {
            response.StatusCode = statusCode;
            using (var sw = new StreamWriter (response.OutputStream))
            {
                sw.AutoFlush = false;
                foreach (var line in data)
                    await sw.WriteAsync (line.ToCharArray ());
                await sw.FlushAsync ();
            }
        }

        public static async Task SendJsonResponseAsync (this HttpListenerResponse response, int statusCode, object? data)
        {
            response.Headers.Add (HttpResponseHeader.ContentType, "application/json");
            await SendResponseAsync (response, statusCode, data != null ? System.Text.Json.JsonSerializer.Serialize (data) : string.Empty);
        }

        public static async Task SendJsonResponseAsync<T> (this HttpListenerResponse response, int statusCode, IEnumerable<T> data, Func<T, object> selector)
        {
            response.Headers.Add (HttpResponseHeader.ContentType, "application/json");

            response.StatusCode = statusCode;
            using (var sw = new StreamWriter (response.OutputStream))
            {
                sw.AutoFlush = true;
                foreach (var d in data)
                {
                    var line = System.Text.Json.JsonSerializer.Serialize (selector (d));
                    await sw.WriteLineAsync (line.ToCharArray ());
                }
            }
        }
    }
}

