using Renci.SshNet;
using Renci.SshNet.Common;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unosquare.Labs.SshDeploy.Options;

namespace Unosquare.Labs.SshDeploy
{
    public static partial class DeploymentManager
    {

        private const string LinuxCurrentDirectory = ".";
        private const string LinuxParentDirectory = "..";
        private const char LinuxDirectorySeparatorChar = '/';
        private const char WindowsDirectorySeparatorChar = '\\';
        private const string LinuxDirectorySeparator = "/";
        private const string WindowsDirectorySeparator = "\\";
        private const byte Escape = 27; // Escape sequence character
        private readonly static byte[] ControlSequenceInitiators = new byte[] { (byte)'[', (byte)']' };
        private const string TerminalName = "xterm"; // "vanilla" works well; "xterm" is also a good option

        public static void ExecuteRunVerb(RunVerbOptions invokedVerbOptions)
        {
            using (var client = DeploymentManager.CreateClient(invokedVerbOptions))
            {
                client.Connect();
                var command = DeploymentManager.ExecuteCommand(client, invokedVerbOptions.Command);
                Environment.ExitCode = command.ExitStatus;
                client.Disconnect();
            }
        }

        private static SshClient CreateClient(CliVerbOptionsBase options)
        {
            var simpleConnectionInfo = new PasswordConnectionInfo(options.Host, options.Port, options.Username, options.Password);
            return new Renci.SshNet.SshClient(simpleConnectionInfo);
        }

        private static SshCommand ExecuteCommand(SshClient client, string commandText)
        {
            ConsoleManager.WriteLine("SSH TX:");
            ConsoleManager.WriteLine(commandText, ConsoleColor.Green);


            using (var command = client.CreateCommand(commandText))
            {
                var result = command.Execute();
                ConsoleManager.WriteLine("SSH RX:");

                if (command.ExitStatus != 0)
                {
                    ConsoleManager.ErrorWriteLine("Error " + command.ExitStatus);
                    ConsoleManager.ErrorWriteLine(command.Error);
                }

                if (string.IsNullOrWhiteSpace(result) == false)
                {
                    ConsoleManager.WriteLine(result, ConsoleColor.Yellow);
                }

                return command;
            }
        }

        private static void HandleShellEscapeSequence(byte[] escapeSequence)
        {
            var controlSequenceChars = ControlSequenceInitiators.Select(s => (char)s).ToArray();
            var escapeString = System.Text.Encoding.ASCII.GetString(escapeSequence);
            var command = escapeString.Last();
            var arguments = escapeString
                .TrimStart(controlSequenceChars)
                .TrimEnd(command)
                .Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries);

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
                    case "30": Console.ForegroundColor = ConsoleColor.Black; break;
                    case "31": Console.ForegroundColor = ConsoleColor.Red; break;
                    case "32": Console.ForegroundColor = ConsoleColor.Green; break;
                    case "33": Console.ForegroundColor = ConsoleColor.Yellow; break;
                    case "34": Console.ForegroundColor = ConsoleColor.Cyan; break;
                    case "35": Console.ForegroundColor = ConsoleColor.Magenta; break;
                    case "36": Console.ForegroundColor = ConsoleColor.Cyan; break;
                    case "37": Console.ForegroundColor = ConsoleColor.Gray; break;
                }

                switch (background)
                {
                    case "40": Console.BackgroundColor = ConsoleColor.Black; break;
                    case "41": Console.BackgroundColor = ConsoleColor.Red; break;
                    case "42": Console.BackgroundColor = ConsoleColor.Green; break;
                    case "43": Console.BackgroundColor = ConsoleColor.Yellow; break;
                    case "44": Console.BackgroundColor = ConsoleColor.DarkBlue; break;
                    case "45": Console.BackgroundColor = ConsoleColor.Magenta; break;
                    case "46": Console.BackgroundColor = ConsoleColor.Cyan; break;
                    case "47": Console.BackgroundColor = ConsoleColor.Gray; break;
                }
            }
            else
            {
                if (System.Diagnostics.Debugger.IsAttached)
                {
                    System.Diagnostics.Debug.WriteLine("Unhandled escape sequence.\r\n    Text:  {0}\r\n    Bytes: {1}",
                        escapeString, string.Join(" ", escapeSequence.Select(s => s.ToString()).ToArray()));
                }

                /*
                var originalColor = Console.ForegroundColor;
                Console.ForegroundColor = ConsoleColor.DarkYellow;
                Console.Write(string.Format("{0}", escapeString));
                Console.ForegroundColor = originalColor;
                */
            }
        }



    }
}
