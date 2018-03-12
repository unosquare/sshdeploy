namespace Unosquare.Labs.SshDeploy.Options
{
    using Swan.Attributes;

    public class MonitorVerbOptions : CliExecuteOptionsBase
    {
        [ArgumentOption('s', "source", HelpText = "The source path for the files to transfer", Required = true)]
        public string SourcePath { get; set; }

        [ArgumentOption('m', "monitor", DefaultValue = "sshdeploy.ready", HelpText = "The command to run on the target machine", Required = false)]       
        public string MonitorFile { get; set; }

        [ArgumentOption('l', "legacy", DefaultValue = false, HelpText = "Monitor files using legacy method", Required = false)]
        public bool Legacy { get; set; }
    }
}
