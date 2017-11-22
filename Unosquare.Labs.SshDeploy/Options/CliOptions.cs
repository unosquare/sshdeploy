using System;
using System.Collections.Generic;
using System.Text;
using Unosquare.Swan.Attributes;

namespace Unosquare.Labs.SshDeploy.Options
{
    public class CliOptions
    {
        [VerbOption("push")]
        public PushVerbOptions PushVerbOptions { get; set; }

        [VerbOption("monitor")]
        public MonitorVerbOptions MonitorVerbOptions { get; set; }

        [VerbOption("run")]
        public RunVerbOptions RunVerbOptions { get; set; }

        [VerbOption("shell")]
        public ShellVerbOptions ShellVerbOptions { get; set; }

    }
}
