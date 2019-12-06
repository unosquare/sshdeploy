namespace Unosquare.Labs.SshDeploy.Attributes
{
    using System;

    [AttributeUsage(AttributeTargets.Property)]
    internal class VerbAttributeBase : Attribute
    {
        public string? ShortName { get; set; }

        public string? LongName { get; set; }
    }
}
