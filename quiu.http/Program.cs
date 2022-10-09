using CommandLine;
using quiu;

Parser.Default.ParseArguments<Options> (args)
              .WithParsed (Run);


void Run (Options options)
{
    var cfg = new QuiuConfig (options.ConfigPath);
    var app = new quiu.QuiuContext (cfg);
}


#pragma warning disable CS8618
class Options
{
    [Option('c', "config", Required =true, HelpText ="Config file path")]
    public string ConfigPath { get; set; }
}
#pragma warning restore CS8618
