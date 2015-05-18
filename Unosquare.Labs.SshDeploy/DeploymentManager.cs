using Renci.SshNet;
using Renci.SshNet.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Unosquare.Labs.SshDeploy
{
    public static class DeploymentManager
    {

        static public void ExecuteRunVerb(RunVerbOptions invokedVerbOptions)
        {
            using (var client = DeploymentManager.CreateClient(invokedVerbOptions))
            {
                client.Connect();
                var command = DeploymentManager.ExecuteCommand(client, invokedVerbOptions.Command);
                Environment.ExitCode = command.ExitStatus;
                client.Disconnect();
            }
        }

        static private SshClient CreateClient(CliVerbOptionsBase options)
        {
            var simpleConnectionInfo = new PasswordConnectionInfo(options.Host, options.Port, options.Username, options.Password);
            return new Renci.SshNet.SshClient(simpleConnectionInfo);
        }

        static private SshCommand ExecuteCommand(SshClient client, string commandText)
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

        private const byte Escape = 27; // Escape sequence character
        private readonly static byte[] ControlSequenceInitiators = new byte[] { (byte)'[', (byte)']' };
        private const string TerminalName = "vanilla"; // "vanilla" works well

        static private void HandleShellEscapeSequence(byte[] escapeSequence)
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
                    case "34": Console.ForegroundColor = ConsoleColor.Blue; break;
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
                    case "44": Console.BackgroundColor = ConsoleColor.Blue; break;
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

        static public void ExecuteShellVerb(ShellVerbOptions invokedVerbOptions)
        {
            using (var sshClient = DeploymentManager.CreateClient(invokedVerbOptions))
            {

                var exitEvent = new System.Threading.ManualResetEventSlim(false);
                sshClient.Connect();

                var terminalModes = new Dictionary<TerminalModes, uint>();
                terminalModes.Add(TerminalModes.ECHO, 0);
                terminalModes.Add(TerminalModes.IGNCR, 1);

                var bufferWidth = (uint)Console.BufferWidth;
                var bufferHeight = (uint)Console.BufferHeight;
                var windowWidth = (uint)Console.WindowWidth;
                var windowHeight = (uint)Console.WindowHeight;
                var bufferSize = Console.BufferWidth * Console.BufferHeight;

                var encoding = System.Text.Encoding.ASCII;

                using (var shell = sshClient.CreateShellStream(TerminalName, bufferWidth, bufferHeight, windowWidth, windowHeight, bufferSize, terminalModes))
                {
                    var escapeSequenceBytes = new List<byte>(128);
                    var isInEscapeSequence = false;
                    byte rxBytePrevious = 0;
                    byte rxByte = 0;
                    byte escapeSequenceType = 0;

                    shell.DataReceived += (s, e) =>
                    {
                        var rxBuffer = e.Data;
                        for (var i = 0; i < rxBuffer.Length; i++)
                        {
                            rxByte = rxBuffer[i];

                            // We've found the beginning of an escapr sequence
                            if (isInEscapeSequence == false && rxByte == Escape)
                            {
                                isInEscapeSequence = true;
                                escapeSequenceBytes.Clear();
                                rxBytePrevious = rxByte;
                                continue;
                            }

                            // Print out the character if we are not in an escape sequence and it is a printable character
                            if (isInEscapeSequence == false)
                            {
                                if (rxByte >= 32 || (rxByte >= 8 && rxByte <= 13))
                                {
                                    Console.Write((char)rxByte);
                                }
                                else
                                {
                                    var originalColor = Console.ForegroundColor;
                                    Console.ForegroundColor = ConsoleColor.DarkYellow;
                                    Console.Write("[NPC " + rxByte.ToString() + "]");
                                    Console.ForegroundColor = originalColor;
                                }

                                rxBytePrevious = rxByte;
                                continue;
                            }

                            if (isInEscapeSequence == true)
                            {
                                // Add the byte to the escape sequence
                                escapeSequenceBytes.Add(rxByte);

                                // Ignore the second escape byte 91 '[' or ']'
                                if (rxBytePrevious == Escape)
                                {
                                    rxBytePrevious = rxByte;
                                    if (ControlSequenceInitiators.Contains(rxByte))
                                    {
                                        escapeSequenceType = rxByte;
                                        continue;
                                    }
                                    else
                                    {
                                        escapeSequenceType = 0;
                                    }
                                }

                                // Detect if it's the last byte of the escape sequence (64 to 126)
                                // This last character determines the command to execute
                                var endOfSequenceType91 = escapeSequenceType == (byte)'[' && (rxByte >= 64 && rxByte <= 126);
                                var endOfSequenceType93 = escapeSequenceType == (byte)']' && (rxByte == 7);
                                if (endOfSequenceType91 || endOfSequenceType93)
                                {
                                    try
                                    {
                                        // Execute the command of the given escape sequence
                                        HandleShellEscapeSequence(escapeSequenceBytes.ToArray());
                                    }
                                    finally
                                    {
                                        isInEscapeSequence = false;
                                        escapeSequenceBytes.Clear();
                                        rxBytePrevious = rxByte;
                                    }

                                    continue;
                                }
                            }

                            rxBytePrevious = rxByte;

                        }
                    };

                    shell.ErrorOccurred += (s, e) =>
                    {
                        System.Diagnostics.Debug.WriteLine(e.Exception.Message);
                    };

                    while (true)
                    {
                        var line = Console.ReadLine();
                        var lineData = encoding.GetBytes(line + "\r\n");
                        shell.Write(lineData, 0, lineData.Length);
                        shell.Flush();

                        if (line.Equals("exit"))
                        {
                            var expectResult = shell.Expect("logout", TimeSpan.FromSeconds(2));
                            if (string.IsNullOrWhiteSpace(expectResult) == false && expectResult.Trim().EndsWith("logout"))
                            {
                                break;
                            }
                        }
                    }
                }

                sshClient.Disconnect();
            }
        }
    }
}
