namespace Unosquare.Labs.SshDeploy
{
    using Options;
    using Renci.SshNet;
    using Renci.SshNet.Common;
    using Swan;
    using Swan.Logging;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;

    public static partial class DeploymentManager
    {
        private const string TerminalName = "xterm"; // "vanilla" works well; "xterm" is also a good option
        private const string LinuxCurrentDirectory = ".";
        private const string LinuxParentDirectory = "..";
        private const char LinuxDirectorySeparatorChar = '/';
        private const char WindowsDirectorySeparatorChar = '\\';
        private const string LinuxDirectorySeparator = "/";
        private const byte Escape = 27; // Escape sequence character
        private static readonly byte[] ControlSequenceInitiators = { (byte) '[', (byte) ']' };

        public static void ExecuteRunVerb(RunVerbOptions invokedVerbOptions)
        {
            using var client = CreateClient(invokedVerbOptions);
            client.Connect();
            var command = ExecuteCommand(client, invokedVerbOptions.Command);
            Environment.ExitCode = command.ExitStatus;
            client.Disconnect();
        }

        private static ShellStream CreateBaseShellStream(SshClient sshClient)
        {
            var bufferSize = Console.BufferWidth * Console.BufferHeight;

            return sshClient.CreateShellStream(
                TerminalName,
                (uint) Console.BufferWidth,
                (uint) Console.BufferHeight,
                (uint) Console.WindowWidth,
                (uint) Console.WindowHeight,
                bufferSize,
                new Dictionary<TerminalModes, uint> {{TerminalModes.ECHO, 0}, {TerminalModes.IGNCR, 1}});
        }

        private static SshClient CreateClient(CliVerbOptionsBase options)
        {
            var simpleConnectionInfo =
                new PasswordConnectionInfo(options.Host, options.Port, options.Username, options.Password);
            return new SshClient(simpleConnectionInfo);
        }

        private static SshCommand ExecuteCommand(SshClient client, string commandText)
        {
            Terminal.WriteLine("SSH TX:");
            Terminal.WriteLine(commandText, ConsoleColor.Green);

            using var command = client.CreateCommand(commandText);
            var result = command.Execute();
            Terminal.WriteLine("SSH RX:");

            if (command.ExitStatus != 0)
            {
                Terminal.WriteLine($"Error {command.ExitStatus}");
                Terminal.WriteLine(command.Error);
            }

            if (!string.IsNullOrWhiteSpace(result))
                Terminal.WriteLine(result, ConsoleColor.Yellow);

            return command;
        }

        private static void HandleShellEscapeSequence(byte[] escapeSequence)
        {
            var controlSequenceChars = ControlSequenceInitiators.Select(s => (char) s).ToArray();
            var escapeString = Encoding.ASCII.GetString(escapeSequence);
            var command = escapeString.Last();
            var arguments = escapeString
                .TrimStart(controlSequenceChars)
                .TrimEnd(command)
                .Split(new[] {';'}, StringSplitOptions.RemoveEmptyEntries);

            if (command == 'm' | command == '\a')
            {
                var background = "40";
                var foreground = "37";

                if (arguments.Length == 2)
                {
                    foreground = arguments[1];
                }

                if (arguments.Length == 3)
                {
                    foreground = arguments[1];
                    background = arguments[0];
                }

                Console.ForegroundColor = foreground switch
                {
                    "30" => ConsoleColor.Black,
                    "31" => ConsoleColor.Red,
                    "32" => ConsoleColor.Green,
                    "33" => ConsoleColor.Yellow,
                    "34" => ConsoleColor.Cyan,
                    "35" => ConsoleColor.Magenta,
                    "36" => ConsoleColor.Cyan,
                    "37" => ConsoleColor.Gray,
                    _ => Console.ForegroundColor
                };

                Console.BackgroundColor = background switch
                {
                    "40" => ConsoleColor.Black,
                    "41" => ConsoleColor.Red,
                    "42" => ConsoleColor.Green,
                    "43" => ConsoleColor.Yellow,
                    "44" => ConsoleColor.DarkBlue,
                    "45" => ConsoleColor.Magenta,
                    "46" => ConsoleColor.Cyan,
                    "47" => ConsoleColor.Gray,
                    _ => Console.BackgroundColor
                };
            }
            else
            {
                $"Unhandled escape sequence.\r\n    Text:  {escapeString}\r\n    Bytes: {string.Join(" ", escapeSequence.Select(s => s.ToString()).ToArray())}"
                    .Debug();
            }
        }
    }
}