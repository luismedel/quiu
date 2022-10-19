using System;
using System.Diagnostics;

namespace quiu.core
{
    public static class Logger
    {
        static void ConsoleOutput (string category, string message, params object[] args)
        {
            var date = DateTime.Now.ToString ("dd MMM yyyy HH:mm:ss");

            if (args?.Length > 0)
                Console.WriteLine ($"[{date}] [{category}] {string.Format (message, args)}");
            else
                Console.WriteLine ($"[{date}] [{category}] {message}");
        }

        [Conditional ("DEBUG")]
        static void DebugOutput (string category, string message, params object[] args)
        {
            var date = DateTime.Now.ToString ("dd MMM yyyy HH:mm:ss");

            if (args?.Length > 0)
                System.Diagnostics.Debug.WriteLine ($"[{date}] [{category}] {string.Format (message, args)}");
            else
                System.Diagnostics.Debug.WriteLine ($"[{date}] [{category}] {message}");
        }

        [Conditional ("TRACE")]
        static void TraceOutput (string category, string message, params object[] args)
        {
            var date = DateTime.Now.ToString ("dd MMM yyyy HH:mm:ss");

            if (args?.Length > 0)
                System.Diagnostics.Trace.WriteLine ($"[{date}] [{category}] {string.Format (message, args)}");
            else
                System.Diagnostics.Trace.WriteLine ($"[{date}] [{category}] {message}");
        }

        [Conditional ("DEBUG")]
        public static void Debug (string message, params object[] args) => DebugOutput ("DEBUG   ", message, args);

        [Conditional ("TRACE")]
        public static void Trace (string message, params object[] args) => TraceOutput ("TRACE   ", message, args);

        public static void Info (string message, params object[] args) => ConsoleOutput ("INFO    ", message, args);
        public static void Warning (string message, params object[] args) => ConsoleOutput ("WARN    ", message, args);
        public static void Error (string message, params object[] args) => ConsoleOutput ("ERROR   ", message, args);
    }
}
