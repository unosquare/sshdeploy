using System;
using System.Collections.Generic;
using System.Text;
using Unosquare.Swan.Attributes;

namespace Unosquare.Labs.SshDeploy.Options
{
    public class MonitorVerbOptions : PushVerbOptions
    {
        [ArgumentOption('m', "monitor", DefaultValue = "sshdeploy.ready", HelpText = "The command to run on the target machine", Required = false)]       
        public string MonitorFile { get; set; } 
    }
}
