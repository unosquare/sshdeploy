using CommandLine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Unosquare.Labs.SshDeploy
{
    public class PushVerbOptions
        : CliVerbOptionsBase
    {
        [Option('s', "source", HelpText = "The source path for the files to transfer", Required = true)]
        public string SourcePath { get; set; }

        [Option('t', "target", HelpText = "The target path of the files to transfer", Required = true)]
        public string TargetPath { get; set; }
    }
}
