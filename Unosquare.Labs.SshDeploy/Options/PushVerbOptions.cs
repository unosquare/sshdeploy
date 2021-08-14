namespace Unosquare.Labs.SshDeploy.Options
{
    using System.IO;
    using Swan.Parsers;

    public class PushVerbOptions : CliExecuteOptionsBase
    {
        private const string BinFolder = "bin";
        private const string PublishFolder = "publish";

        public static bool IgnoreTargetFrameworkToOutputPath { get; set; }

        [ArgumentOption('c', "configuration", DefaultValue = "Debug", HelpText = "Target configuration. The default for most projects is 'Debug'.", Required = false)]
        public string? Configuration { get; set; }

        [ArgumentOption('f', "framework", HelpText = "The target framework has to be specified in the project file.", Required = true)]
        public string Framework { get; set; }

        [ArgumentOption('r', "runtime", HelpText = "The given runtime used for creating a self-contained deployment.", DefaultValue = "",Required = false)]
        public string? Runtime { get; set; }

        [ArgumentOption('x', "execute", HelpText = "Adds user execute mode permission to files transferred.", DefaultValue = "", Required = false)]
        public string? Execute { get; set; }

        [ArgumentOption("skipbuildtargetframework", DefaultValue = false, HelpText = "Skips adding TargetFramework to the build command", Required = false)]
        public bool SkipBuildTargetFramework { get; set; }
        public string SourcePath => IgnoreTargetFrameworkToOutputPath ?
            Path.Combine(Program.CurrentDirectory, BinFolder, Configuration, Runtime, PublishFolder) :
            Path.Combine(Program.CurrentDirectory, BinFolder, Configuration, Framework, Runtime, PublishFolder);
    }
}