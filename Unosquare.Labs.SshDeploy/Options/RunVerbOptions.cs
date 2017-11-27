using Unosquare.Swan.Attributes;

namespace Unosquare.Labs.SshDeploy.Options
{
    public class RunVerbOptions : CliVerbOptionsBase
    {
        [ArgumentOption('c', "command", HelpText = "The command to run on the target machine", Required = true)]
        public string Command { get; set; }
    }
}