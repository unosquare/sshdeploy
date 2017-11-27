using Unosquare.Swan.Attributes;

namespace Unosquare.Labs.SshDeploy.Options
{
    public class CliOptions
    {
        [VerbOption("push", HelpText = "Transfers the files and folders from a source path in the local machine to a target path in the remote machine")]
        public PushVerbOptions PushVerbOptions { get; set; }

        [VerbOption("monitor", HelpText = "Monitors a folder for a deployment and automatically transfers the files over the target.")]
        public MonitorVerbOptions MonitorVerbOptions { get; set; }

        [VerbOption("run", HelpText = "Runs the specified command on the target machine")]
        public RunVerbOptions RunVerbOptions { get; set; }

        [VerbOption("shell", HelpText = "Opens an interactive mode shell")]
        public ShellVerbOptions ShellVerbOptions { get; set; }

    }
}
