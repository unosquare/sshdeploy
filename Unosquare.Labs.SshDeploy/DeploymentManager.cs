namespace Unosquare.Labs.SshDeploy
{
    using System.Collections.Generic;
    using Renci.SshNet.Common;
    using System;
    using System.Linq;
    using System.Text;
    using Renci.SshNet;
    using Options;
    using Swan;

    public static partial class DeploymentManager
    {
        private const string TerminalName = "xterm"; // "vanilla" works well; "xterm" is also a good option
        private const string LinuxCurrentDirectory = ".";
        private const string LinuxParentDirectory = "..";
        private const char LinuxDirectorySeparatorChar = '/';
        private const char WindowsDirectorySeparatorChar = '\\';
        private const string LinuxDirectorySeparator = "/";
        private const byte Escape = 27; // Escape sequence character
        private static readonly byte[] ControlSequenceInitiators = {(byte) '[', (byte) ']'};

        public static void ExecuteRunVerb(RunVerbOptions invokedVerbOptions)
        {
            using (var client = CreateClient(invokedVerbOptions))
            {
                client.Connect();
                var command = ExecuteCommand(client, invokedVerbOptions.Command);
                Environment.ExitCode = command.ExitStatus;
                client.Disconnect();
            }
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
            "SSH TX:".WriteLine();
            commandText.WriteLine(ConsoleColor.Green);

            using (var command = client.CreateCommand(commandText))
            {
                var result = command.Execute();
                "SSH RX:".WriteLine();

                if (command.ExitStatus != 0)
                {
                    $"Error {command.ExitStatus}".WriteLine();
                    command.Error.Error();
                }

                if (string.IsNullOrWhiteSpace(result) == false)
                {
                    result.WriteLine(ConsoleColor.Yellow);
                }

                return command;
            }
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

            if (command == 'm')
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

                switch (foreground)
                {
                    case "30":
                        Console.ForegroundColor = ConsoleColor.Black;
                        break;
                    case "31":
                        Console.ForegroundColor = ConsoleColor.Red;
                        break;
                    case "32":
                        Console.ForegroundColor = ConsoleColor.Green;
                        break;
                    case "33":
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        break;
                    case "34":
                        Console.ForegroundColor = ConsoleColor.Cyan;
                        break;
                    case "35":
                        Console.ForegroundColor = ConsoleColor.Magenta;
                        break;
                    case "36":
                        Console.ForegroundColor = ConsoleColor.Cyan;
                        break;
                    case "37":
                        Console.ForegroundColor = ConsoleColor.Gray;
                        break;
                }

                switch (background)
                {
                    case "40":
                        Console.BackgroundColor = ConsoleColor.Black;
                        break;
                    case "41":
                        Console.BackgroundColor = ConsoleColor.Red;
                        break;
                    case "42":
                        Console.BackgroundColor = ConsoleColor.Green;
                        break;
                    case "43":
                        Console.BackgroundColor = ConsoleColor.Yellow;
                        break;
                    case "44":
                        Console.BackgroundColor = ConsoleColor.DarkBlue;
                        break;
                    case "45":
                        Console.BackgroundColor = ConsoleColor.Magenta;
                        break;
                    case "46":
                        Console.BackgroundColor = ConsoleColor.Cyan;
                        break;
                    case "47":
                        Console.BackgroundColor = ConsoleColor.Gray;
                        break;
                }
            }
            else
            {
                $"Unhandled escape sequence.\r\n    Text:  {escapeString}\r\n    Bytes: {string.Join(" ", escapeSequence.Select(s => s.ToString()).ToArray())}"
                    .Debug();
            }
        }
    }
}