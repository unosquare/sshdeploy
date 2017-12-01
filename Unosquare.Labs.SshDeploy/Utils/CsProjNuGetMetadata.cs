namespace Unosquare.Labs.SshDeploy.Utils
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using System.Text;
    using System.Xml.Linq;
    using Unosquare.Labs.SshDeploy.Attributes;

    public class CsProjNuGetMetadata
    {
        private readonly XDocument _xmlDocument;

        public CsProjNuGetMetadata(XDocument xmlDocument)
        {
            _xmlDocument = xmlDocument;
        }

        public string PackageId
        {
            get
            {
                var element = FindElement(nameof(PackageId));
                return element?.Value;
            }
        }

        [Push(ShortName = "-f", LongName = "--framework")]
        public string TargetFramework
        {
            get
            {
                var element = FindElement(nameof(TargetFramework));
                return element?.Value;
            }
        }

        [Monitor(ShortName = "-l", LongName = "--legacy")]
        public bool Legacy
        {
            get
            {
                var element = FindElement(nameof(Legacy));
                return (element != null) ? true : false;
            }
        }

        [Push(ShortName = "-c", LongName = "--configuration")]
        public string Configuration
        {
            get
            {
                var element = FindElement(nameof(Configuration));
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
                return (element != null) ? true : false;
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
        public bool Verbose
        {
            get
            {
                var element = FindElement(nameof(Verbose));
                return (element != null) ? true : false;
            }
        }

        [Push(ShortName = "-h", LongName = "--host")]
        [Monitor(ShortName = "-h", LongName = "--host")]
        public string Host
        {
            get
            {
                var element = FindElement(nameof(Host));
                return element?.Value;
            }
        }

        [Push(ShortName ="-p", LongName = "--port")]
        [Monitor(ShortName = "-p", LongName = "--port")]
        public string Port
        {
            get
            {
                var element = FindElement(nameof(Port));
                return element?.Value;
            }
        }

        [Push( ShortName ="-u", LongName ="--username")]
        [Monitor(ShortName = "-u", LongName = "--username")]
        public string Username
        {
            get
            {
                var element = FindElement(nameof(Username));
                return element?.Value;
            }
        }

        [Push(ShortName ="-w", LongName = "--password")]
        [Monitor(ShortName = "-w", LongName = "--password")]
        public string Password
        {
            get
            {
                var element = FindElement(nameof(Password));
                return element?.Value;
            }
        }

        [Monitor(ShortName ="-s", LongName ="--source")]
        public string Source
        {
            get
            {
                var element = FindElement(nameof(Source));
                return element?.Value;
            }
        }

        [Monitor(ShortName = "-t", LongName = "--target")]
        public string SSHTargetPath
        {
            get
            {
                var element = FindElement(nameof(SSHTargetPath));
                return element?.Value;
            }
        }

        public void ParseCsProjTags(ref string[] args)
        {
            var argsList = args.ToList();
            var type = args.Contains("push") ? typeof(PushAttribute) : typeof(MonitorAttribute);
            var props = this.GetType().GetProperties().Where(prop => Attribute.IsDefined(prop, type));
            foreach (PropertyInfo propertyInfo in props)
            {
                if (propertyInfo.GetValue(this) != null)
                {
                    var attribute = (VerbAttributeBase) propertyInfo.GetCustomAttribute(type);
                    if (!args.Contains(attribute.LongName) & !args.Contains(attribute.ShortName))
                    {
                        if (!(propertyInfo.GetValue(this).GetType() == typeof(bool)))
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

        private XElement FindElement(string elementName)
        {
            return _xmlDocument.Descendants(elementName).FirstOrDefault();
        }
    }
}
