using System;
using System.Collections.Generic;
using System.Text;

namespace Unosquare.Labs.SshDeploy.Attributes
{
    [AttributeUsage(AttributeTargets.Property)]
    class PushAttribute : Attribute
    {
        private string _shortName;

        public string ShortName
        {
            get { return _shortName; }
            set { _shortName = value; }
        }

        private string _longName;

        public string LongName
        {
            get { return _longName; }
            set { _longName = value; }
        }
    }
}
