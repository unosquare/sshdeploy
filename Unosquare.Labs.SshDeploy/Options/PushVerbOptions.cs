namespace Unosquare.Labs.SshDeploy.Options
{
    using System.IO;
    using Swan.Attributes;

    public class PushVerbOptions : CliExecuteOptionsBase
    {
        private const string BinFolder = "bin";
        private const string PublishFolder = "bin";

        [ArgumentOption('c', "configuration", DefaultValue = "Debug",
            HelpText = "Target configuration. The default for most projects is 'Debug'.", Required = false)]
        public string Configuration { get; set; }

        [ArgumentOption('f', "framework", HelpText = "The target framework has to be specified in the project file.",
            Required = true)]
        public string Framework { get; set; }

        [ArgumentOption('r', "runtime", HelpText = "The given runtime used for creating a self-contained deployment",
            Required = true)]
        public string Runtime { get; set; }

        public string SourcePath => Path.Combine(Program.CurrentDirectory, BinFolder, Configuration, Framework, Runtime, PublishFolder);
    }
}