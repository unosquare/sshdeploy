using CommandLine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Unosquare.Labs.SshDeploy
{
    public class RunVerbOptions
        : CliVerbOptionsBase
    {
        [Option('c', "command", HelpText = "The command to run on the target machine", Required = true)]
        public string Command { get; set; }
    }
}
