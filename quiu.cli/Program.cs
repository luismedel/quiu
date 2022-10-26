using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Loader;
using CommandLine;
using quiu.core;
using quiu.http;

namespace quiu.cli
{
    public class Program
    {
        enum OperationMode
        {
            Server,
            AdminServer,
            Client
        }

#pragma warning disable CS8618
        abstract class OptionsBase
        {
            [Option ('c', "config", Required = false, HelpText = "Config file path")]
            public string? ConfigPath { get; set; }

            public Config? Config
            {
                get {
                    if (_config == null)
                        this.PrepareConfig ();

                    return _config;
                }
            }

            protected abstract void SetOptionsFromConfig ();
            protected abstract void ValidateConfig ();

            protected T? GetOptionValue<T>(T? currentValue, string configKey)
            {
                if (currentValue != null)
                {
                    this.Config!.Set (configKey, currentValue);
                    return currentValue;
                }
                else
                    return this.Config!.Get<T?> (configKey);
            }

            void PrepareConfig ()
            {
                if (string.IsNullOrEmpty (ConfigPath))
                    _config = new Config ();
                else
                {
                    Logger.Info ($"Loading config from {Path.GetFullPath (ConfigPath)}...");
                    try { _config = new Config (ConfigPath); }
                    catch (Exception ex) { throw new Exception ($"Error loading config: {ex.Message}"); }
                }

                try
                {
                    SetOptionsFromConfig ();
                    ValidateConfig ();
                }
                catch (Exception ex) { throw new Exception ($"Config error: {ex.Message}"); }
            }

            Config? _config;
        }

        [Verb ("server", isDefault: true)]
        class ServerOptions : OptionsBase
        {
            [Option ('h', "host", Required = false, Default = "*", HelpText = "Server host address")]
            public string? ServerHost { get; set; }

            [Option ('p', "port", Required = false, Default = HttpServer.DEFAULT_PORT, HelpText = "Server port")]
            public int? ServerPort { get; set; }

            [Option ("builtins", Separator = ',', Required = false, HelpText = "Add default channel with the specified Guids")]
            public IEnumerable<Guid> BuiltinChannels { get; set; }

            protected override void SetOptionsFromConfig ()
            {
                ServerHost = this.GetOptionValue (ServerHost, "server_host");
                ServerPort = this.GetOptionValue (ServerPort, "server_port");
            }

            protected override void ValidateConfig ()
            {
                if (ServerHost == null)
                    throw new ArgumentException ("Missing server host");

                if (ServerPort == null)
                    throw new ArgumentException ("Missing server port");
            }
        }

        [Verb ("client", isDefault: false)]
        class ClientOptions /*: OptionsBase*/
        {
            // TODO: Add local client options
        }
#pragma warning restore CS8618


        public static async Task Main (string[] args)
        {
            try { await new Program ().Start (args); }
            catch (OperationCanceledException) { }
        }

        async Task Start (string[] args)
        {
            Trace.Listeners.Add (new ConsoleTraceListener (false));

            Console.CancelKeyPress += Console_CancelKeyPress;

            try
            {
                Parser.Default.ParseArguments<ServerOptions> (args)
                              .WithParsed<ServerOptions> (ExecServer);

                if (_app != null)
                {
                    while (_app!.RunningTasks.Length > 0 && !_app!.CancellationToken.IsCancellationRequested)
                        await Task.Delay (100);
                }
            }
            catch (Exception ex)
            {
                Logger.Error (ex.Message);
#if DEBUG
                throw;
#endif
            }

            Environment.Exit (0);
        }

        void Console_CancelKeyPress (object? sender, ConsoleCancelEventArgs e)
        {
            Console.CancelKeyPress -= Console_CancelKeyPress;
            _app?.Dispose ();
        }

        void CreateContext (OptionsBase options)
        {
            try
            {
                Logger.Info ("Initializing context.");
                _app = new Context (options.Config!);
            }
            catch (Exception ex)
            {
                throw new Exception ($"Error initializing app: {ex.Message}");
            }
        }

        void ExecServer (ServerOptions options)
        {
            CreateContext (options);

            var builtins = options.BuiltinChannels.ToArray ();
            if (builtins.Length > 0)
            {
                Logger.Info ($"Adding {builtins.Length} builtin(s)...");
                foreach (var g in options.BuiltinChannels)
                {
                    Logger.Info ($" - {g}");
                    _app!.AddChannel (g);
                }
            }

            _app!.EnqueueTask (StartServer (new HttpServer (_app!)), "Data server loop");
        }

        Task? StartServer (HttpServerBase server) => server.RunLoop ();

        Context? _app;
    }
}
