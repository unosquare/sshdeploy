namespace Unosquare.Labs.SshDeploy.Options
{
    using Swan.Parsers;

    public class RunVerbOptions : CliVerbOptionsBase
    {
        [ArgumentOption('c', "command", HelpText = "The command to run on the target machine", Required = true)]
        public string Command { get; set; }
    }
}