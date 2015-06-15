using CommandLine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Unosquare.Labs.SshDeploy.Options
{
    public class MonitorVerbOptions
        : PushVerbOptions
    {
        [Option('m', "monitor", DefaultValue = "sshdeploy.ready", HelpText = "The path to the file used as a signal that the files are ready to be deployed. Once the deployemtn is completed, the file is deleted.", Required = false)]
        public string MonitorFile { get; set; }
    }
}
