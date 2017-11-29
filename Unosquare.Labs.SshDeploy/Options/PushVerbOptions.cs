
namespace Unosquare.Labs.SshDeploy.Options
{
    using System.IO;
    using Unosquare.Swan.Attributes;

    public class PushVerbOptions : CliExecuteOptionsBase
    {
        private static string Bin { get; } = "bin";

        [ArgumentOption('c', "configuration", DefaultValue = "Debug",
            HelpText = "Target configuration. The default for most projects is 'Debug'.", Required = false)]
        public string Configuration { get; set; }

        [ArgumentOption('f', "framework", HelpText = "The target framework has to be specified in the project file.", Required = true)]
        public string Framework { get; set; }

        public string SourcePath
        {
            get
            {
                return Path.Combine(Program.CurrentDirectory, PushVerbOptions.Bin, this.Configuration, this.Framework);
            }
        }
    }
}
