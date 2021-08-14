namespace Unosquare.Labs.SshDeploy.Options
{
    using Swan.Parsers;

    public class CliExecuteOptionsBase : CliVerbOptionsBase
    {
        [ArgumentOption("sync", HelpText = "Instructs the engine to sync the source and target directories without performing a full replacement", Required = false)]
        public bool UseSync { get; set; }

        [ArgumentOption('t', "target", HelpText = "The target path of the files to transfer", Required = true)]
        public string TargetPath { get; set; }

        [ArgumentOption("pre", HelpText = "Command to execute prior file transfer to target", Required = false)]
        public string? PreCommand { get; set; }

        [ArgumentOption("post", HelpText = "Command to execute after file transfer to target", Required = false)]
        public string? PostCommand { get; set; }

        [ArgumentOption("clean", DefaultValue = false, HelpText = "Deletes all files and folders on the target before pushing the new files.", Required = false)]
        public bool CleanTarget { get; set; }

        [ArgumentOption("exclude", Separator = '|', DefaultValue = ".ready|.vshost.exe|.vshost.exe.config", HelpText = "a pipe (|) separated list of file suffixes to ignore while deploying.", Required = false)]
        public string[]? ExcludeFileSuffixes { get; set; }
    }
}
