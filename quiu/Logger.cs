using System;
using System.Diagnostics;

namespace quiu
{
    public static class Logger
    {
        static void Write (string category, string message, params object[] args)
        {
            var date = DateTime.Now.ToString ("dd MMM yyyy HH:mm:ss");

            if (args?.Length > 0)
                System.Diagnostics.Debug.WriteLine ($"[{date}] [{category}] {string.Format (message, args)}");
            else
                System.Diagnostics.Debug.WriteLine ($"[{date}] [{category}] {message}");
        }

        [Conditional ("DEBUG")]
        public static void Debug (string message, params object[] args) => Write ("DEBUG   ", message, args);

        public static void Info (string message, params object[] args) => Write ("INFO    ", message, args);
        public static void Warning (string message, params object[] args) => Write ("WARN    ", message, args);
        public static void Error (string message, params object[] args) => Write ("ERROR   ", message, args);
    }
}
