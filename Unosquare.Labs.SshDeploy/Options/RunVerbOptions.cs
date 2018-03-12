namespace Unosquare.Labs.SshDeploy.Options
{
    using Swan.Attributes;

    public class RunVerbOptions : CliVerbOptionsBase
    {
        [ArgumentOption('c', "command", HelpText = "The command to run on the target machine", Required = true)]
        public string Command { get; set; }
    }
}