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
        [Option('v', "verbose", DefaultValue = 1, HelpText = "Add this option to print messages to standard error and standard output streams. 0 to disable, any other number to enable.", Required = false)]
        public int Verbose { get; set; }

        [Option('h', "host", HelpText = "Hostname or IP Address of the target. -- Must be running an SSH server.", Required = true)]
        public string Host { get; set; }

        [Option('p', "port", DefaultValue = 22, HelpText = "Port on which SSH is running.")]
        public int Port { get; set; }

        [Option('u', "username", DefaultValue = "pi", HelpText = "The username under which the connection will be established.")]
        public string Username { get; set; }

        [Option('w', "password", DefaultValue = "raspberry", HelpText = "The password for the given username.", Required = false)]
        public string Password { get; set; }
    }
}
