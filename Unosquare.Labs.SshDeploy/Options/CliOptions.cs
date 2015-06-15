using CommandLine;
using CommandLine.Text;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Unosquare.Labs.SshDeploy.Options
{
    public class CliOptions
    {
        public const string PushVerb = "push";
        public const string RunVerb = "run";
        public const string ShellVerb = "shell";
        public const string MonitorVerb = "monitor";

        [HelpVerbOption]
        public string GetUsage(string verb)
        {
            return HelpText.AutoBuild(this, verb);
        }

        [VerbOption(PushVerb, HelpText = "Transfers the files and folders from a source path in the local machine to a target path in the remote machine.")]
        public PushVerbOptions PushVerbOptions { get; set; }

        [VerbOption(MonitorVerb, HelpText = "Monitors a folder for a deployment and automatically transfers the files over to the target.")]
        public MonitorVerbOptions MonitorVerbOptions { get; set; }

        [VerbOption(RunVerb, HelpText = "Runs the specified command on the target machine.")]
        public RunVerbOptions RunVerbOptions { get; set; }

        [VerbOption(ShellVerb, HelpText = "Opens an interactive mode shell.")]
        public ShellVerbOptions ShellVerbOptions { get; set; }

    }
}
