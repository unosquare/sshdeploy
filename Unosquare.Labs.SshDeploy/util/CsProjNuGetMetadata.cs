using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Xml.Linq;
using Unosquare.Labs.SshDeploy.Attributes;

namespace Unosquare.Labs.SshDeploy.util
{
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

        [Push(ShortName = "-h", LongName = "--host")]
        public string Host
        {
            get
            {
                var element = FindElement(nameof(Host));
                return element?.Value;
            }
        }

        [Push(ShortName ="-p", LongName = "--port")]
        public string Port
        {
            get
            {
                var element = FindElement(nameof(Port));
                return element?.Value;
            }
        }

        [Push( ShortName ="-u", LongName ="--username")]
        public string Username
        {
            get
            {
                var element = FindElement(nameof(Username));
                return element?.Value;
            }
        }

        [Push(ShortName ="-w", LongName = "--password")]
        public string Password
        {
            get
            {
                var element = FindElement(nameof(Password));
                return element?.Value;
            }
        }

        public string Monitor
        {
            get
            {
                var element = FindElement(nameof(Monitor));
                return element?.Value;
            }
        }

        [Monitor(ShortName ="-m", LongName ="--monitor")]
        public string Source
        {
            get
            {
                var element = FindElement(nameof(Source));
                return element?.Value;
            }
        }

        public string Target
        {
            get
            {
                var element = FindElement(nameof(Target));
                return element?.Value;
            }
        }

        public void ParseCsProjTags(ref string[] args, Type t)
        {
            var argsList = args.ToList();

            var type = (t.GetType() == typeof(PushAttribute)) ? typeof(PushAttribute) : typeof(MonitorAttribute);
            var props = this.GetType().GetProperties().Where(prop => Attribute.IsDefined(prop, type));

            foreach (PropertyInfo propertyInfo in props)
            {
                if (propertyInfo.GetValue(this) != null)
                {
                    var attribute = propertyInfo.GetCustomAttribute<VerbAttribute>();
                    if ( !args.Contains(attribute.LongName) & !args.Contains(attribute.ShortName))
                    {
                        argsList.Add(string.IsNullOrWhiteSpace(attribute.ShortName) ? attribute.ShortName : attribute.LongName);
                        argsList.Add(propertyInfo.GetValue(this).ToString());
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
