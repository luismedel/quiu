using System;
using System.Runtime.InteropServices;
using CommandLine;
using quiu.core;

Parser.Default.ParseArguments<Options> (args)
              .WithParsed (Run);


void Run (Options options)
{
    var cfg = new Config (options.ConfigPath);
    var app = new Context (cfg);
}


#pragma warning disable CS8618
class Options
{
    [Option ('c', "config", Required = true, HelpText = "Config file path")]
    public string ConfigPath { get; set; }
}
#pragma warning restore CS8618
