namespace Unosquare.Labs.SshDeploy.Utils
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using System.Text;
    using System.Xml.Linq;
    using Unosquare.Labs.SshDeploy.Attributes;
    using Unosquare.Swan.Components;

    public class CsProjNuGetMetadata : CsProjMetadataBase
    {
        [Push(ShortName = "-f", LongName = "--framework")]
        public new string TargetFramework => FindElement(nameof(TargetFramework))?.Value;

        [Monitor(ShortName = "-l", LongName = "--legacy")]
        public bool SshDeployLegacy => FindElement(nameof(SshDeployLegacy)) != null;

        [Monitor(ShortName = "-m", LongName = "--monitor")]
        public string SshDeployMonitorFile => FindElement(nameof(SshDeployMonitorFile))?.Value;

        [Push(ShortName = "-c", LongName = "--configuration")]
        public string Configuration
        {
            get
            {
                var element = FindElement(nameof(Configuration));
                return element?.Value;
            }
        }

        [Run(ShortName ="-c",LongName = "--command")]
        public string Command
        {
            get
            {
                var element = FindElement(nameof(Command));
                return element?.Value;
            }
        }

        [Push(LongName = "--pre")]
        [Monitor(LongName = "--pre")]
        public string PreCommand
        {
            get
            {
                var element = FindElement(nameof(PreCommand));
                return element?.Value;
            }
        }

        [Push(LongName = "--post")]
        [Monitor(LongName = "--post")]
        public string PostCommand
        {
            get
            {
                var element = FindElement(nameof(PostCommand));
                return element?.Value;
            }
        }

        [Push(LongName = "--clean")]
        [Monitor(LongName = "--clean")]
        public bool Clean
        {
            get
            {
                var element = FindElement(nameof(Clean));
                return element != null;
            }
        }

        [Push(LongName = "--exclude")]
        [Monitor(LongName = "--exclude")]
        public string Exclude
        {
            get
            {
                var element = FindElement(nameof(Exclude));
                return element?.Value;
            }
        }

        [Push(ShortName = "-v", LongName = "--verbose")]
        [Monitor(ShortName = "-v", LongName = "--verbose")]
        [Shell(ShortName = "-v", LongName = "--verbose")]
        [Run(ShortName = "-v", LongName = "--verbose")]
        public bool Verbose
        {
            get
            {
                var element = FindElement(nameof(Verbose));
                return element != null;
            }
        }

        [Push(ShortName = "-h", LongName = "--host")]
        [Monitor(ShortName = "-h", LongName = "--host")]
        [Shell(ShortName = "-h", LongName = "--host")]
        [Run(ShortName = "-h", LongName = "--host")]
        public string SshHost
        {
            get
            {
                var element = FindElement(nameof(SshHost));
                return element?.Value;
            }
        }

        [Push(ShortName ="-p", LongName = "--port")]
        [Monitor(ShortName = "-p", LongName = "--port")]
        [Shell(ShortName = "-p", LongName = "--port")]
        [Run(ShortName = "-p", LongName = "--port")]
        public string Port
        {
            get
            {
                var element = FindElement(nameof(Port));
                return element?.Value;
            }
        }

        [Push(ShortName ="-u", LongName ="--username")]
        [Monitor(ShortName = "-u", LongName = "--username")]
        [Shell(ShortName = "-u", LongName = "--username")]
        [Run(ShortName = "-u", LongName = "--username")]
        public string SshUsername
        {
            get
            {
                var element = FindElement(nameof(SshUsername));
                return element?.Value;
            }
        }

        [Push(ShortName ="-w", LongName = "--password")]
        [Monitor(ShortName = "-w", LongName = "--password")]
        [Shell(ShortName = "-w", LongName = "--password")]
        [Run(ShortName = "-w", LongName = "--password")]
        public string SshPassword
        {
            get
            {
                var element = FindElement(nameof(SshPassword));
                return element?.Value;
            }
        }

        [Monitor(ShortName ="-s", LongName ="--source")]
        public string SourcePath
        {
            get
            {
                var element = FindElement(nameof(SourcePath));
                return element?.Value;
            }
        }

        [Monitor(ShortName = "-t", LongName = "--target")]
        [Push(ShortName = "-t", LongName = "--target")]
        public string SshTargetPath
        {
            get
            {
                var element = FindElement(nameof(SshTargetPath));
                return element?.Value;
            }
        }

        private static Type GetAttributeType(string[] args)
        {
            if (args.Contains("push"))
                return typeof(PushAttribute);
            else if (args.Contains("monitor"))
                return typeof(PushAttribute);
            else if (args.Contains("run"))
                return typeof(RunAttribute);
            else return typeof(ShellAttribute);
        } 

        public override void ParseCsProjTags(ref string[] args)
        {
            var argsList = args.ToList();
            var type = GetAttributeType(args);
            var props = this.GetType().GetProperties().Where(prop => Attribute.IsDefined(prop, type));
            foreach (PropertyInfo propertyInfo in props)
            {
                if (propertyInfo.GetValue(this) != null)
                {
                    var attribute = (VerbAttributeBase)propertyInfo.GetCustomAttribute(type);
                    if (!args.Contains(attribute.LongName) & !args.Contains(attribute.ShortName))
                    {
                        if (!(propertyInfo.GetValue(this) is bool))
                        {
                            argsList.Add(!string.IsNullOrWhiteSpace(attribute.ShortName) ? attribute.ShortName : attribute.LongName);
                            argsList.Add(propertyInfo.GetValue(this).ToString());
                        }
                        else if (bool.Parse(propertyInfo.GetValue(this).ToString()))
                        {
                            argsList.Add(!string.IsNullOrWhiteSpace(attribute.ShortName) ? attribute.ShortName : attribute.LongName);
                        }
                    }
                }
            }

            args = argsList.ToArray();
        }
    }
}
