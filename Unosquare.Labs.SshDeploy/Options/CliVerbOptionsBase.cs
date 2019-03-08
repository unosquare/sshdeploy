namespace Unosquare.Labs.SshDeploy.Options
{
    using Swan.Attributes;

    public abstract class CliVerbOptionsBase
    {
        [ArgumentOption(
            'v', 
            "verbose", 
            DefaultValue = true,
            HelpText = "Add this option to print messages to standard error and output streams.",
            Required = false)]
        public bool Verbose { get; set; }

        [ArgumentOption(
            'h', 
            "host",
            HelpText = "Hostname or IP Address of the target. -- Must be running an SSH server.", 
            Required = true)]
        public string Host { get; set; }

        [ArgumentOption('p', "port", DefaultValue = 22, HelpText = "Port on which SSH is running.")]
        public int Port { get; set; }

        [ArgumentOption(
            'u', 
            "username", 
            DefaultValue = "pi",
            HelpText = "The username under which the connection will be established.")]
        public string Username { get; set; }

        [ArgumentOption(
            'w', 
            "password", 
            DefaultValue = "raspberry", 
            HelpText = "The password for the given username.",
            Required = false)]
        public string Password { get; set; }
    }
}