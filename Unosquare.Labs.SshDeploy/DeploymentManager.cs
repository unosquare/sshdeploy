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

        private static void DeleteDirectoryRecursive(SftpClient client, string path)
        {
            var files = client.ListDirectory(path);
            foreach (var file in files)
            {
                if (file.Name.Equals(".") || file.Name.Equals(".."))
                    continue;

                if (file.IsDirectory)
                {
                    DeleteDirectoryRecursive(client, file.FullName);
                }

                try
                {
                    client.Delete(file.FullName);
                }
                catch
                {
                    ConsoleManager.ErrorWriteLine("WARNING: Failed to delete file or folder '" + file.FullName + "'");
                }
            }
        }

        private static void CreateDirectoryRecursive(SftpClient client, string path)
        {

            if (path.StartsWith("/") == false)
                throw new ArgumentException("Argument path must start with '/'");

            if (client.Exists(path))
            {
                var info = client.GetAttributes(path);
                if (info.IsDirectory)
                    return;
            }
            var pathParts = path.Split(new char[] { '/' }, StringSplitOptions.RemoveEmptyEntries);

            pathParts = pathParts.Skip(0).Take(pathParts.Length - 1).ToArray();
            var priorPath = "/" + string.Join("/", pathParts);

            if (pathParts.Length > 1)
                CreateDirectoryRecursive(client, priorPath);

            client.CreateDirectory(path);

        }

        private static string MakeRelativePath(string filePath, string referencePath)
        {
            var fileUri = new Uri(filePath);
            var referenceUri = new Uri(referencePath);
            return referenceUri.MakeRelativeUri(fileUri).ToString();
        }

        private static string MakeAbsolutePath(string filePath, string referencePath)
        {
            var referenceUri = new Uri(referencePath, UriKind.Relative);
            var fileUri = new Uri(referenceUri, filePath);
            return fileUri.ToString();
        }

        internal static void ExecuteMonitorVerb(MonitorVerbOptions verbOptions)
        {
            var sourcePath = System.IO.Path.GetFullPath(verbOptions.SourcePath.Trim());
            var targetPath = verbOptions.TargetPath.Trim();
            var monitorFile = System.IO.Path.IsPathRooted(verbOptions.MonitorFile) ? System.IO.Path.GetFullPath(verbOptions.MonitorFile) : System.IO.Path.Combine(sourcePath, verbOptions.MonitorFile);
            ConsoleManager.WriteLine(string.Empty);
            ConsoleManager.WriteLine("Monitor mode starting");
            ConsoleManager.WriteLine("Monitor parameters follow: ");
            ConsoleManager.WriteLine("    Monitor File    " + monitorFile, ConsoleColor.DarkYellow);
            ConsoleManager.WriteLine("    Source Path     " + sourcePath, ConsoleColor.DarkYellow);
            ConsoleManager.WriteLine("    Excluded Files  " + verbOptions.ExcludeFileSuffixes, ConsoleColor.DarkYellow);
            ConsoleManager.WriteLine("    Target Address  " + verbOptions.Host + ":" + verbOptions.Port.ToString(), ConsoleColor.DarkYellow);
            ConsoleManager.WriteLine("    Username        " + verbOptions.Username, ConsoleColor.DarkYellow);
            ConsoleManager.WriteLine("    Target Path     " + targetPath, ConsoleColor.DarkYellow);
            ConsoleManager.WriteLine("    Clean Target    " + (verbOptions.CleanTarget ? "YES" : "NO"), ConsoleColor.DarkYellow);
            ConsoleManager.WriteLine("    Pre Deployment  " + verbOptions.PreCommand, ConsoleColor.DarkYellow);
            ConsoleManager.WriteLine("    Post Deployment " + verbOptions.PostCommand, ConsoleColor.DarkYellow);

            

           

            if (System.IO.Directory.Exists(sourcePath) == false)
                throw new DirectoryNotFoundException("Source Path '" + sourcePath + "' was not found.");

            var fsMonitor = new FileSystemMonitor(1, sourcePath);
            var isDeploying = false;
            var deploymentNumber = 1;
            var simpleConnectionInfo = new PasswordConnectionInfo(verbOptions.Host, verbOptions.Port, verbOptions.Username, verbOptions.Password);
            var ignoreFileSuffixes = string.IsNullOrWhiteSpace(verbOptions.ExcludeFileSuffixes) ?
                new string[] { } : verbOptions.ExcludeFileSuffixes.Split(new char[] { '|' }, StringSplitOptions.RemoveEmptyEntries);

            using (var sftpClient = new Renci.SshNet.SftpClient(simpleConnectionInfo))
            {
                ConsoleManager.WriteLine("Connecting to host " + verbOptions.Host + ":" + verbOptions.Port + " via SFTP.");
                sftpClient.Connect();

                fsMonitor.FileSystemEntryChanged += (s, e) =>
                {
                    if (e.ChangeType != FileSystemEntryChangeType.FileAdded && e.ChangeType != FileSystemEntryChangeType.FileModified)
                        return;

                    if (e.Path.ToLowerInvariant().Equals(monitorFile.ToLowerInvariant()) == false)
                        return;

                    ConsoleManager.WriteLine(string.Empty);

                    if (isDeploying)
                    {
                        ConsoleManager.WriteLine("WARNING: Deployment already in progress. Deployment will not occur.", ConsoleColor.DarkYellow);
                        return;
                    }

                    isDeploying = true;
                    var stopwatch = new System.Diagnostics.Stopwatch();
                    stopwatch.Start();

                    try
                    {
                        ConsoleManager.WriteLine("    Starting deployment ID " + deploymentNumber + " - "
                            + DateTime.Now.ToLongDateString() + " " + DateTime.Now.ToLongTimeString(), ConsoleColor.Green);
                        deploymentNumber++;

                        if (sftpClient.IsConnected == false)
                        {
                            ConsoleManager.WriteLine("Reconnecting to host " + verbOptions.Host + ":" + verbOptions.Port + " via SFTP.");
                            sftpClient.Connect();
                        }

                        if (sftpClient.Exists(targetPath) == false)
                        {
                            ConsoleManager.WriteLine("    Target Path '" + targetPath + "' does not exist. -- Will attempt to create.", ConsoleColor.Green);
                            CreateDirectoryRecursive(sftpClient, targetPath);
                            ConsoleManager.WriteLine("    Target Path '" + targetPath + "' created successfully.", ConsoleColor.Green);
                        }

                        if (verbOptions.CleanTarget)
                        {
                            ConsoleManager.WriteLine("    Cleaning Target Path '" + targetPath + "'", ConsoleColor.Green);
                            DeleteDirectoryRecursive(sftpClient, targetPath);
                        }

                        var filesInSource = System.IO.Directory.GetFiles(sourcePath, FileSystemMonitor.AllFilesPattern, System.IO.SearchOption.AllDirectories);
                        var filesToDeploy = new List<string>();

                        foreach (var file in filesInSource)
                        {
                            var ignore = false;

                            foreach (var ignoreSuffix in ignoreFileSuffixes)
                            {
                                if (file.EndsWith(ignoreSuffix))
                                {
                                    ignore = true;
                                    break;
                                }
                            }

                            if (ignore) continue;
                            filesToDeploy.Add(file);
                        }

                        ConsoleManager.WriteLine("    Deploying " + filesToDeploy.Count + " files.", ConsoleColor.Green);
                        foreach (var file in filesToDeploy)
                        {
                            var relativePath = MakeRelativePath(file, sourcePath + Path.DirectorySeparatorChar);
                            var fileTargetPath = Path.Combine(targetPath, relativePath).Replace(Path.DirectorySeparatorChar, '/');
                            var targetDirectory = Path.GetDirectoryName(fileTargetPath).Replace(Path.DirectorySeparatorChar, '/');

                            CreateDirectoryRecursive(sftpClient, targetDirectory);

                            using (var fileStream = System.IO.File.OpenRead(file))
                            {
                                sftpClient.UploadFile(fileStream, fileTargetPath);
                            }

                        }

                    }
                    catch (Exception ex)
                    {
                        ConsoleManager.ErrorWriteLine("Deployment failed.");
                        ConsoleManager.ErrorWriteLine("    Error - " + ex.GetType().Name);
                        ConsoleManager.ErrorWriteLine("    " + ex.Message);
                        ConsoleManager.ErrorWriteLine("    " + ex.StackTrace);
                    }
                    finally
                    {

                        isDeploying = false;
                        stopwatch.Stop();
                        ConsoleManager.WriteLine("    Finished deployment in " + Math.Round(stopwatch.Elapsed.TotalSeconds, 2).ToString() + " seconds.", ConsoleColor.Green);
                    }

                };

                fsMonitor.Start();
                ConsoleManager.WriteLine("File System Monitor is now running.");
                ConsoleManager.WriteLine("Writing a new monitor file will trigger a new deployment.");
                ConsoleManager.WriteLine("Remember: Press Q to quit.");
                ConsoleManager.WriteLine("Ground Control to Major Tom: Have a nice trip in space!", ConsoleColor.DarkCyan);

                while (true)
                {
                    if (Console.ReadKey(true).Key == ConsoleKey.Q)
                        break;
                }

                ConsoleManager.WriteLine(string.Empty);

                fsMonitor.Stop();
                ConsoleManager.WriteLine("File System monitor was stopped.");

                if (sftpClient.IsConnected == true)
                    sftpClient.Disconnect();

                ConsoleManager.WriteLine("SFTP client disconnected.");
                ConsoleManager.WriteLine("Application will exit now.");
            }
        }
    }
}
