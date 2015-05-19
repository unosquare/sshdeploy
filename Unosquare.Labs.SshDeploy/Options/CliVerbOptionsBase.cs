using CommandLine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Unosquare.Labs.SshDeploy.Options
{
    public abstract class CliVerbOptionsBase
    {
        [Option('v', "verbose", DefaultValue = false, HelpText = "Add this option to print messages to standard error and standard output streams.", Required = false)]
        public bool Verbose { get; set; }

        [Option('h', "host", DefaultValue = "localhost", HelpText = "Hostname or IP Address of the target")]
        public string Host { get; set; }

        [Option('p', "port", DefaultValue = 22, HelpText = "Port on which SSH is running.")]
        public int Port { get; set; }

        [Option('u', "username", DefaultValue = "root", HelpText = "The username under which the connection will be established.")]
        public string Username { get; set; }

        [Option('w', "password", HelpText = "The password for the given username.", Required = false)]
        public string Password { get; set; }
    }
}
