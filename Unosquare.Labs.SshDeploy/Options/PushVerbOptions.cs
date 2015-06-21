using CommandLine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Unosquare.Labs.SshDeploy.Options
{
    public class PushVerbOptions
        : CliVerbOptionsBase
    {
        [Option('s', "source", HelpText = "The source path for the files to transfer", Required = true)]
        public string SourcePath { get; set; }

        [Option('t', "target", HelpText = "The target path of the files to transfer", Required = true)]
        public string TargetPath { get; set; }

        [Option("pre", HelpText = "Command to execute prior file transfer to target", Required = false)]
        public string PreCommand { get; set; }

        [Option("post", HelpText = "Command to execute after file transfer to target", Required = false)]
        public string PostCommand { get; set; }

        [Option("clean", DefaultValue = true, HelpText = "Deletes all files and folders on the target before pushing the new files", Required = false)]
        public bool CleanTarget { get; set; }

        [Option("exclude", DefaultValue = ".ready|.vshost.exe|.vshost.exe.config", HelpText = "a pipe (|) separated list of file suffixes to ignore while deploying.", Required = false)]
        public string ExcludeFileSuffixes { get; set; }

        public string[] ExcludeFileSuffixList
        {
            get
            {
                var ignoreFileSuffixes = string.IsNullOrWhiteSpace(ExcludeFileSuffixes) ?
                    new string[] { } :
                    ExcludeFileSuffixes.Split(new char[] { '|' }, StringSplitOptions.RemoveEmptyEntries);

                return ignoreFileSuffixes;
            }
        }
    }
}
