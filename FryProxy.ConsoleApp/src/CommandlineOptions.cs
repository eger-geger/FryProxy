using System;
using CommandLine;
using CommandLine.Text;

namespace FryProxy.ConsoleApp
{
    public class CommandlineOptions {

        [Option('h', "host", DefaultValue ="localhost", HelpText = "Hostname on to listen")]
        public String Host { get; set; }

        [HelpOption]
        public String GetUsage()
        {
            return HelpText.AutoBuild(this);
        }
    }
}
