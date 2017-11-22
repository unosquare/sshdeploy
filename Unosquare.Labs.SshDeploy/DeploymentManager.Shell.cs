using Renci.SshNet.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Unosquare.Labs.SshDeploy.Options;

namespace Unosquare.Labs.SshDeploy
{
    partial class DeploymentManager
    {
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
