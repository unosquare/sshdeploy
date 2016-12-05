namespace Unosquare.Labs.SshDeploy.Options
{
    using CommandLine;

    public class RunVerbOptions
        : CliVerbOptionsBase
    {
        [Option('c', "command", HelpText = "The command to run on the target machine", Required = true)]
        public string Command { get; set; }
    }
}
