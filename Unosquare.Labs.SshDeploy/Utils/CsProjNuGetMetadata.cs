namespace Unosquare.Labs.SshDeploy.Utils
{
    using Attributes;
    using Swan.Components;
    using System;
    using System.Linq;
    using System.Reflection;

    public class CsProjNuGetMetadata : CsProjMetadataBase
    {
        [Push(ShortName = "-f", LongName = "--framework")]
        public new string TargetFramework => FindElement(nameof(TargetFramework))?.Value;

        [Monitor(ShortName = "-l", LongName = "--legacy")]
        public bool SshDeployLegacy => FindElement(nameof(SshDeployLegacy)) != null;

        [Monitor(ShortName = "-m", LongName = "--monitor")]
        public string SshDeployMonitorFile => FindElement(nameof(SshDeployMonitorFile))?.Value;

        [Push(ShortName = "-c", LongName = "--configuration")]
        public string SshDeployConfiguration => FindElement(nameof(SshDeployConfiguration))?.Value;

        [Run(ShortName ="-c",LongName = "--command")]
        public string SshDeployCommand => FindElement(nameof(SshDeployCommand))?.Value;

        [Push(LongName = "--pre")]
        [Monitor(LongName = "--pre")]
        public string SshDeployPreCommand => FindElement(nameof(SshDeployPreCommand))?.Value;

        [Push(LongName = "--post")]
        [Monitor(LongName = "--post")]
        public string SshDeployPostCommand => FindElement(nameof(SshDeployPostCommand))?.Value;

        [Push(LongName = "--clean")]
        [Monitor(LongName = "--clean")]
        public bool SshDeployClean => FindElement(nameof(SshDeployClean)) != null;

        [Push(LongName = "--exclude")]
        [Monitor(LongName = "--exclude")]
        public string SshDeployExclude => FindElement(nameof(SshDeployExclude))?.Value;        
        
        [Push(ShortName = "-h", LongName = "--host")]
        [Monitor(ShortName = "-h", LongName = "--host")]
        [Shell(ShortName = "-h", LongName = "--host")]
        [Run(ShortName = "-h", LongName = "--host")]
        public string SshDeployHost => FindElement(nameof(SshDeployHost))?.Value;

        [Push(ShortName ="-p", LongName = "--port")]
        [Monitor(ShortName = "-p", LongName = "--port")]
        [Shell(ShortName = "-p", LongName = "--port")]
        [Run(ShortName = "-p", LongName = "--port")]
        public string SshDeployPort => FindElement(nameof(SshDeployPort))?.Value;

        [Push(ShortName = "-u", LongName = "--username")]
        [Monitor(ShortName = "-u", LongName = "--username")]
        [Shell(ShortName = "-u", LongName = "--username")]
        [Run(ShortName = "-u", LongName = "--username")]
        public string SshDeployUsername => FindElement(nameof(SshDeployUsername))?.Value;

        [Push(ShortName = "-w", LongName = "--password")]
        [Monitor(ShortName = "-w", LongName = "--password")]
        [Shell(ShortName = "-w", LongName = "--password")]
        [Run(ShortName = "-w", LongName = "--password")]
        public string SshDeployPassword => FindElement(nameof(SshDeployPassword))?.Value;

        [Monitor(ShortName ="-s", LongName ="--source")]
        public string SshDeploySourcePath => FindElement(nameof(SshDeploySourcePath))?.Value;

        [Monitor(ShortName = "-t", LongName = "--target")]
        [Push(ShortName = "-t", LongName = "--target")]
        public string SshDeployTargetPath => FindElement(nameof(SshDeployTargetPath))?.Value;

        private static Type GetAttributeType(string[] args)
        {
            if (args.Contains("push"))
                return typeof(PushAttribute);
            if (args.Contains("monitor"))
                return typeof(MonitorAttribute);
            if (args.Contains("run"))
                return typeof(RunAttribute);

            return typeof(ShellAttribute);
        } 

        public override void ParseCsProjTags(ref string[] args)
        {
            var argsList = args.ToList();
            var type = GetAttributeType(args);
            var props = GetType().GetProperties().Where(prop => Attribute.IsDefined(prop, type));

            foreach (var propertyInfo in props)
            {
                if (propertyInfo.GetValue(this) == null) continue;

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

            args = argsList.ToArray();
        }
    }
}
