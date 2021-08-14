namespace Unosquare.Labs.SshDeploy.Options
{
    using Swan.Parsers;

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
        public string? Password { get; set; }

        [ArgumentOption(
            'k', 
            "keypath", 
            DefaultValue = "", 
            HelpText = "The private key path used for key authentication on the target machine.",
            Required = false)]
        public string? KeyPath { get; set; }

        [ArgumentOption(
            "keypassword", 
            DefaultValue = "", 
            HelpText = "The password used to open the private key file.",
            Required = false)]
        public string? KeyPassword { get; set; }
    }
}