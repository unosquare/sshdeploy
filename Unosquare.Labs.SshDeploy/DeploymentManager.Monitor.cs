namespace Unosquare.Labs.SshDeploy
{
    using Renci.SshNet;
    using Renci.SshNet.Common;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using Unosquare.Labs.SshDeploy.Options;

    partial class DeploymentManager
    {
        #region Supporting Methods

        /// <summary>
        /// Deletes the linux directory recursively.
        /// </summary>
        /// <param name="client">The client.</param>
        /// <param name="path">The path.</param>
        private static void DeleteLinuxDirectoryRecursive(SftpClient client, string path)
        {
            var files = client.ListDirectory(path);
            foreach (var file in files)
            {
                if (file.Name.Equals(LinuxCurrentDirectory) || file.Name.Equals(LinuxParentDirectory))
                    continue;

                if (file.IsDirectory)
                {
                    DeleteLinuxDirectoryRecursive(client, file.FullName);
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

        /// <summary>
        /// Creates the linux directory recursively.
        /// </summary>
        /// <param name="client">The client.</param>
        /// <param name="path">The path.</param>
        /// <exception cref="ArgumentException">Argument path must start with  + LinuxDirectorySeparator</exception>
        private static void CreateLinuxDirectoryRecursive(SftpClient client, string path)
        {

            if (path.StartsWith(LinuxDirectorySeparator) == false)
                throw new ArgumentException("Argument path must start with " + LinuxDirectorySeparator);

            if (client.Exists(path))
            {
                var info = client.GetAttributes(path);
                if (info.IsDirectory)
                    return;
            }
            var pathParts = path.Split(new char[] { LinuxDirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries);

            pathParts = pathParts.Skip(0).Take(pathParts.Length - 1).ToArray();
            var priorPath = LinuxDirectorySeparator + string.Join(LinuxDirectorySeparator, pathParts);

            if (pathParts.Length > 1)
                CreateLinuxDirectoryRecursive(client, priorPath);

            client.CreateDirectory(path);

        }

        /// <summary>
        /// Makes the given path relative to an absolute path.
        /// </summary>
        /// <param name="filePath">The file path.</param>
        /// <param name="referencePath">The reference path.</param>
        /// <returns></returns>
        private static string MakeRelativePath(string filePath, string referencePath)
        {
            var fileUri = new Uri(filePath);
            var referenceUri = new Uri(referencePath);
            return referenceUri.MakeRelativeUri(fileUri).ToString();
        }

        /// <summary>
        /// Runs pre and post deployment commands over the SSH client
        /// </summary>
        /// <param name="sshClient">The SSH client.</param>
        /// <param name="commandText">The command text.</param>
        private static void RunDeploymentCommand(ShellStream shellStream, string commandText)
        {
            if (string.IsNullOrWhiteSpace(commandText) == true) return;

            ConsoleManager.WriteLine("    Executing shell command.", ConsoleColor.Green);
            shellStream.WriteLine(commandText);
            shellStream.Flush();
            ConsoleManager.WriteLine("    TX: " + commandText, ConsoleColor.DarkYellow);
        }

        /// <summary>
        /// Runs the deployment command.
        /// </summary>
        /// <param name="sshClient">The SSH client.</param>
        /// <param name="commandText">The command text.</param>
        private static void RunDeploymentCommand(SshClient sshClient, string commandText)
        {
            if (string.IsNullOrWhiteSpace(commandText) == true) return;

            ConsoleManager.WriteLine("    Executing SSH client command.", ConsoleColor.Green);
            var result = sshClient.RunCommand(commandText);
            ConsoleManager.WriteLine(string.Format("    SSH TX: {0}", commandText), ConsoleColor.DarkYellow);
            ConsoleManager.WriteLine(string.Format("    SSH RX: [{0}] ", result.ExitStatus, result.Result), ConsoleColor.DarkYellow);
        }

        /// <summary>
        /// Prints the currently supplied monitor mode options.
        /// </summary>
        /// <param name="verbOptions">The verb options.</param>
        /// <param name="monitorFile">The monitor file.</param>
        /// <param name="sourcePath">The source path.</param>
        /// <param name="targetPath">The target path.</param>
        private static void PrintMonitorOptions(MonitorVerbOptions verbOptions, string monitorFile, string sourcePath, string targetPath)
        {
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
        }

        /// <summary>
        /// Checks that both, SFTP and SSH clients have a working connection. If they don't it attempts to reconnect.
        /// </summary>
        /// <param name="sshClient">The SSH client.</param>
        /// <param name="sftpClient">The SFTP client.</param>
        /// <param name="verbOptions">The verb options.</param>
        private static void EnsureMonitorConnection(SshClient sshClient, SftpClient sftpClient, MonitorVerbOptions verbOptions)
        {
            if (sshClient.IsConnected == false)
            {
                ConsoleManager.WriteLine("Connecting to host " + verbOptions.Host + ":" + verbOptions.Port + " via SSH.");
                sshClient.Connect();
            }

            if (sftpClient.IsConnected == false)
            {
                ConsoleManager.WriteLine("Connecting to host " + verbOptions.Host + ":" + verbOptions.Port + " via SFTP.");
                sftpClient.Connect();
            }
        }

        /// <summary>
        /// Creates the given directory structure on the target machine.
        /// </summary>
        /// <param name="sftpClient">The SFTP client.</param>
        /// <param name="targetPath">The target path.</param>
        private static void CreateTargetPath(SftpClient sftpClient, string targetPath)
        {
            if (sftpClient.Exists(targetPath) == true) return;

            ConsoleManager.WriteLine("    Target Path '" + targetPath + "' does not exist. -- Will attempt to create.", ConsoleColor.Green);
            CreateLinuxDirectoryRecursive(sftpClient, targetPath);
            ConsoleManager.WriteLine("    Target Path '" + targetPath + "' created successfully.", ConsoleColor.Green);

        }

        /// <summary>
        /// Prepares the given target path for deployment. If clean target is false, it does nothing.
        /// </summary>
        /// <param name="sftpClient">The SFTP client.</param>
        /// <param name="targetPath">The target path.</param>
        /// <param name="cleanTarget">if set to <c>true</c> [clean target].</param>
        private static void PrepareTargetPath(SftpClient sftpClient, string targetPath, bool cleanTarget)
        {
            if (cleanTarget == false) return;
            ConsoleManager.WriteLine("    Cleaning Target Path '" + targetPath + "'", ConsoleColor.Green);
            DeleteLinuxDirectoryRecursive(sftpClient, targetPath);

        }

        /// <summary>
        /// Uploads the files in the source Windows path to the target Linux path.
        /// </summary>
        /// <param name="sftpClient">The SFTP client.</param>
        /// <param name="targetPath">The target path.</param>
        /// <param name="sourcePath">The source path.</param>
        /// <param name="ignoreFileSuffixes">The ignore file suffixes.</param>
        private static void UploadFilesToTarget(SftpClient sftpClient, string targetPath, string sourcePath, string[] ignoreFileSuffixes)
        {
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
                var fileTargetPath = Path.Combine(targetPath, relativePath).Replace(WindowsDirectorySeparatorChar, LinuxDirectorySeparatorChar);
                var targetDirectory = Path.GetDirectoryName(fileTargetPath).Replace(WindowsDirectorySeparatorChar, LinuxDirectorySeparatorChar);

                CreateLinuxDirectoryRecursive(sftpClient, targetDirectory);

                using (var fileStream = System.IO.File.OpenRead(file))
                {
                    sftpClient.UploadFile(fileStream, fileTargetPath);
                }

            }
        }

        /// <summary>
        /// Stops the monitor mode by closing SFTP and SSH connections, and stopping the File System Monitor.
        /// </summary>
        /// <param name="sftpClient">The SFTP client.</param>
        /// <param name="sshClient">The SSH client.</param>
        /// <param name="fsMonitor">The fs monitor.</param>
        private static void StopMonitorMode(SftpClient sftpClient, SshClient sshClient, FileSystemMonitor fsMonitor)
        {
            ConsoleManager.WriteLine(string.Empty);

            fsMonitor.Stop();
            ConsoleManager.WriteLine("File System monitor was stopped.");

            if (sftpClient.IsConnected == true)
                sftpClient.Disconnect();

            ConsoleManager.WriteLine("SFTP client disconnected.");

            if (sshClient.IsConnected == true)
                sshClient.Disconnect();

            ConsoleManager.WriteLine("SSH client disconnected.");
            ConsoleManager.WriteLine("Application will exit now.");
        }

        /// <summary>
        /// Prints the given exception using the Console Manager.
        /// </summary>
        /// <param name="ex">The ex.</param>
        private static void PrintException(Exception ex)
        {
            ConsoleManager.ErrorWriteLine("Deployment failed.");
            ConsoleManager.ErrorWriteLine("    Error - " + ex.GetType().Name);
            ConsoleManager.ErrorWriteLine("    " + ex.Message);
            ConsoleManager.ErrorWriteLine("    " + ex.StackTrace);
        }

        /// <summary>
        /// Prints the deployment number the Monitor is currently in.
        /// </summary>
        /// <param name="deploymentNumber">The deployment number.</param>
        private static void PrintDeploymentNumber(int deploymentNumber)
        {
            ConsoleManager.WriteLine("    Starting deployment ID " + deploymentNumber + " - "
                + DateTime.Now.ToLongDateString() + " " + DateTime.Now.ToLongTimeString(), ConsoleColor.Green);
        }

        private static ShellStream CreateShellStream(SshClient sshClient)
        {
            var terminalModes = new Dictionary<TerminalModes, uint>();
            terminalModes.Add(TerminalModes.ECHO, 0);
            terminalModes.Add(TerminalModes.IGNCR, 1);

            var bufferWidth = (uint)Console.BufferWidth;
            var bufferHeight = (uint)Console.BufferHeight;
            var windowWidth = (uint)Console.WindowWidth;
            var windowHeight = (uint)Console.WindowHeight;
            var bufferSize = Console.BufferWidth * Console.BufferHeight;

            var encoding = System.Text.Encoding.ASCII;

            var shell = sshClient.CreateShellStream(TerminalName, bufferWidth, bufferHeight, windowWidth, windowHeight, bufferSize, terminalModes);

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
                            if (AllowShellStreamPrint)
                                Console.Write((char)rxByte);
                        }
                        else
                        {
                            var originalColor = Console.ForegroundColor;
                            Console.ForegroundColor = ConsoleColor.DarkYellow;
                            if (AllowShellStreamPrint)
                                Console.Write("[NPC " + rxByte.ToString() + "]");
                            Console.ForegroundColor = originalColor;
                        }

                        rxBytePrevious = rxByte;
                        continue;
                    }

                    // If we are already inside an escape sequence . . .
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
                PrintException(e.Exception);
            };

            return shell;

        }

        private static bool AllowShellStreamPrint = false;

        #endregion

        #region Main Verb Methods

        /// <summary>
        /// Executes the monitor verb. This is the main method.
        /// </summary>
        /// <param name="verbOptions">The verb options.</param>
        /// <exception cref="DirectoryNotFoundException">Source Path ' + sourcePath + ' was not found.</exception>
        public static void ExecuteMonitorVerb(MonitorVerbOptions verbOptions)
        {

            // Working variables
            var sourcePath = System.IO.Path.GetFullPath(verbOptions.SourcePath.Trim());
            var targetPath = verbOptions.TargetPath.Trim();
            var monitorFile = System.IO.Path.IsPathRooted(verbOptions.MonitorFile) ?
                System.IO.Path.GetFullPath(verbOptions.MonitorFile) :
                System.IO.Path.Combine(sourcePath, verbOptions.MonitorFile);
            var fsMonitor = new FileSystemMonitor(1, sourcePath);
            var isDeploying = false;
            var deploymentNumber = 1;
            var simpleConnectionInfo = new PasswordConnectionInfo(verbOptions.Host, verbOptions.Port, verbOptions.Username, verbOptions.Password);
            var ignoreFileSuffixes = string.IsNullOrWhiteSpace(verbOptions.ExcludeFileSuffixes) ?
                new string[] { } :
                verbOptions.ExcludeFileSuffixes.Split(new char[] { '|' }, StringSplitOptions.RemoveEmptyEntries);

            // Show the options to the user so he knows what he's doing
            PrintMonitorOptions(verbOptions, monitorFile, sourcePath, targetPath);

            // Validate source path exists
            if (System.IO.Directory.Exists(sourcePath) == false)
                throw new DirectoryNotFoundException("Source Path '" + sourcePath + "' was not found.");

            // Instantiate an SFTP client and an SSH client
            // SFTP will be used to transfer the files and SSH to execute pre-deployment and post-deployment commands
            using (var sftpClient = new SftpClient(simpleConnectionInfo))
            {
                // SSH will be used to execute commands and to get the output back from the program we are running
                using (var sshClient = new SshClient(simpleConnectionInfo))
                {
                    // Connect SSH and SFTP clients
                    EnsureMonitorConnection(sshClient, sftpClient, verbOptions);

                    // Create the shell stream so we can get debugging info from the post-deployment command
                    using (var shellStream = CreateShellStream(sshClient))
                    {
                        fsMonitor.FileSystemEntryChanged += (s, e) =>
                        {
                            // Detect changes to the monitor file by ignoring deletions and checking file paths.
                            if (e.ChangeType != FileSystemEntryChangeType.FileAdded && e.ChangeType != FileSystemEntryChangeType.FileModified)
                                return;

                            if (e.Path.ToLowerInvariant().Equals(monitorFile.ToLowerInvariant()) == false)
                                return;

                            // At this point the change has been detected; Make sure we are not deploying
                            ConsoleManager.WriteLine(string.Empty);

                            if (isDeploying)
                            {
                                ConsoleManager.WriteLine("WARNING: Deployment already in progress. Deployment will not occur.", ConsoleColor.DarkYellow);
                                return;
                            }

                            // Lock Deployment
                            isDeploying = true;
                            var stopwatch = new System.Diagnostics.Stopwatch();
                            stopwatch.Start();

                            try
                            {
                                AllowShellStreamPrint = false;
                                PrintDeploymentNumber(deploymentNumber);
                                RunDeploymentCommand(sshClient, verbOptions.PreCommand);
                                CreateTargetPath(sftpClient, targetPath);
                                PrepareTargetPath(sftpClient, targetPath, verbOptions.CleanTarget);
                                UploadFilesToTarget(sftpClient, targetPath, sourcePath, ignoreFileSuffixes);
                            }
                            catch (Exception ex)
                            {
                                PrintException(ex);
                            }
                            finally
                            {
                                // Unlock deployment
                                isDeploying = false;
                                deploymentNumber++;
                                stopwatch.Stop();
                                ConsoleManager.WriteLine("    Finished deployment in "
                                    + Math.Round(stopwatch.Elapsed.TotalSeconds, 2).ToString()
                                    + " seconds.", ConsoleColor.Green);

                                AllowShellStreamPrint = true;
                                RunDeploymentCommand(shellStream, verbOptions.PostCommand);
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

                        StopMonitorMode(sftpClient, sshClient, fsMonitor);
                    }
                }
            }
        }

        #endregion
    }
}
