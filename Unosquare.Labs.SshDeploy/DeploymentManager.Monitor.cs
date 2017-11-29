namespace Unosquare.Labs.SshDeploy
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using Renci.SshNet;
    using Renci.SshNet.Common;
    using Options;
    using Swan;

    partial class DeploymentManager
    {
        #region State Variables

        private static bool _forwardShellStreamOutput;
        private static bool _forwardShellStreamInput;
        private static bool _isDeploying;
        private static int _deploymentNumber;

        #endregion

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
                    $"WARNING: Failed to delete file or folder '{file.FullName}'".Error();
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
            var pathParts = path.Split(new[] {LinuxDirectorySeparatorChar}, StringSplitOptions.RemoveEmptyEntries);

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
        /// <param name="shellStream">The shell stream.</param>
        /// <param name="verbOptions">The verb options.</param>
        private static void RunShellStreamCommand(ShellStream shellStream, CliExecuteOptionsBase verbOptions)
        {
            var commandText = verbOptions.PostCommand;
            if (string.IsNullOrWhiteSpace(commandText)) return;

            $"    Executing shell command.".WriteLine(ConsoleColor.Green);
            shellStream.Write($"{commandText}\r\n");
            shellStream.Flush();
            $"    TX: {commandText}".WriteLine(ConsoleColor.DarkYellow);
        }

        /// <summary>
        /// Runs the deployment command.
        /// </summary>
        /// <param name="sshClient">The SSH client.</param>
        /// <param name="verbOptions">The verb options.</param>
        private static void RunSshClientCommand(SshClient sshClient, CliExecuteOptionsBase verbOptions)
        {
            var commandText = verbOptions.PreCommand;
            if (string.IsNullOrWhiteSpace(commandText)) return;

            "    Executing SSH client command.".WriteLine(ConsoleColor.Green);
            var result = sshClient.RunCommand(commandText);
            $"    SSH TX: {commandText}".WriteLine(ConsoleColor.DarkYellow);
            $"    SSH RX: [{result.ExitStatus}] {result.Result}".WriteLine(ConsoleColor.DarkYellow);
        }

        /// <summary>
        /// Prints the currently supplied monitor mode options.
        /// </summary>
        /// <param name="verbOptions">The verb options.</param>
        private static void PrintMonitorOptions(MonitorVerbOptions verbOptions)
        {
            string.Empty.WriteLine();
            "Monitor mode starting".WriteLine();
            "Monitor parameters follow: ".WriteLine();
            $"    Monitor File    {verbOptions.MonitorFile}".WriteLine(ConsoleColor.DarkYellow);
            $"    Source Path     {verbOptions.SourcePath}".WriteLine(ConsoleColor.DarkYellow);
            $"    Excluded Files  {string.Join("|", verbOptions.ExcludeFileSuffixes)}".WriteLine(ConsoleColor.DarkYellow);
            $"    Target Address  {verbOptions.Host}:{verbOptions.Port}".WriteLine(ConsoleColor.DarkYellow);
            $"    Username        {verbOptions.Username}".WriteLine(ConsoleColor.DarkYellow);
            $"    Target Path     {verbOptions.TargetPath}".WriteLine(ConsoleColor.DarkYellow);
            $"    Clean Target    {(verbOptions.CleanTarget ? "YES" : "NO")}".WriteLine(ConsoleColor.DarkYellow);
            $"    Pre Deployment  {verbOptions.PreCommand}".WriteLine(ConsoleColor.DarkYellow);
            $"    Post Deployment {verbOptions.PostCommand}".WriteLine(ConsoleColor.DarkYellow);
        }

        /// <summary>
        /// Checks that both, SFTP and SSH clients have a working connection. If they don't it attempts to reconnect.
        /// </summary>
        /// <param name="sshClient">The SSH client.</param>
        /// <param name="sftpClient">The SFTP client.</param>
        /// <param name="verbOptions">The verb options.</param>
        private static void EnsureMonitorConnection(SshClient sshClient, SftpClient sftpClient,
            CliVerbOptionsBase verbOptions)
        {
            if (sshClient.IsConnected == false)
            {
                $"Connecting to host {verbOptions.Host}:{verbOptions.Port} via SSH.".WriteLine();
                sshClient.Connect();
            }

            if (sftpClient.IsConnected == false)
            {
                $"Connecting to host {verbOptions.Host}:{verbOptions.Port} via SFTP.".WriteLine();
                sftpClient.Connect();
            }
        }

        /// <summary>
        /// Creates the given directory structure on the target machine.
        /// </summary>
        /// <param name="sftpClient">The SFTP client.</param>
        /// <param name="verbOptions">The verb options.</param>
        private static void CreateTargetPath(SftpClient sftpClient, CliExecuteOptionsBase verbOptions)
        {
            if (sftpClient.Exists(verbOptions.TargetPath)) return;

            $"    Target Path '{verbOptions.TargetPath}' does not exist. -- Will attempt to create.".WriteLine(
                ConsoleColor.Green);
            CreateLinuxDirectoryRecursive(sftpClient, verbOptions.TargetPath);
            $"    Target Path '{verbOptions.TargetPath}' created successfully.".WriteLine(ConsoleColor.Green);

        }

        /// <summary>
        /// Prepares the given target path for deployment. If clean target is false, it does nothing.
        /// </summary>
        /// <param name="sftpClient">The SFTP client.</param>
        /// <param name="verbOptions">The verb options.</param>
        private static void PrepareTargetPath(SftpClient sftpClient, CliExecuteOptionsBase verbOptions)
        {
            if (!verbOptions.CleanTarget) return;
            $"    Cleaning Target Path '{verbOptions.TargetPath}'".WriteLine(ConsoleColor.Green);
            DeleteLinuxDirectoryRecursive(sftpClient, verbOptions.TargetPath);
        }

        /// <summary>
        /// Uploads the files in the source Windows path to the target Linux path.
        /// </summary>
        /// <param name="sftpClient">The SFTP client.</param>
        /// <param name="verbOptions">The verb options.</param>
        private static void UploadFilesToTarget(SftpClient sftpClient, string SourcePath, string TargetPath, string[] ExcludeFileSuffixes)
        {
            var filesInSource = Directory.GetFiles(SourcePath, FileSystemMonitor.AllFilesPattern,
                SearchOption.AllDirectories);
            var filesToDeploy = filesInSource.Where(file => !ExcludeFileSuffixes.Any(file.EndsWith))
                .ToList();

            $"    Deploying {filesToDeploy.Count} files.".WriteLine(ConsoleColor.Green);

            foreach (var file in filesToDeploy)
            {
                var relativePath = Path.GetFileName(file);
                var fileTargetPath = Path.Combine(TargetPath, relativePath)
                    .Replace(WindowsDirectorySeparatorChar, LinuxDirectorySeparatorChar);
                var targetDirectory = Path.GetDirectoryName(fileTargetPath)
                    .Replace(WindowsDirectorySeparatorChar, LinuxDirectorySeparatorChar);

                CreateLinuxDirectoryRecursive(sftpClient, targetDirectory);

                using (var fileStream = File.OpenRead(file))
                {
                    sftpClient.UploadFile(fileStream, fileTargetPath);
                }
            }
        }

        private static void StopMonitorMode(SftpClient sftpClient, SshClient sshClient, FileSystemMonitor fsMonitor)
        {
            string.Empty.WriteLine();

            fsMonitor.Stop();
            "File System monitor was stopped.".WriteLine();

            if (sftpClient.IsConnected)
                sftpClient.Disconnect();

            "SFTP client disconnected.".WriteLine();

            if (sshClient.IsConnected)
                sshClient.Disconnect();

            "SSH client disconnected.".WriteLine();
            "Application will exit now.".WriteLine();
        }

        private static void StopMonitorMode(SftpClient sftpClient, SshClient sshClient, FileSystemWatcher watcher)
        {
            string.Empty.WriteLine();

            watcher.EnableRaisingEvents = false;
            "File System monitor was stopped.".WriteLine();

            if (sftpClient.IsConnected)
                sftpClient.Disconnect();

            "SFTP client disconnected.".WriteLine();

            if (sshClient.IsConnected)
                sshClient.Disconnect();

            "SSH client disconnected.".WriteLine();
            "Application will exit now.".WriteLine();
        }

        /// <summary>
        /// Prints the given exception using the Console Manager.
        /// </summary>
        /// <param name="ex">The ex.</param>
        private static void PrintException(Exception ex)
        {
            "Deployment failed.".Error();
            $"    Error - {ex.GetType().Name}".Error();
            $"    {ex.Message}".Error();
            $"    {ex.StackTrace}".Error();
        }

        /// <summary>
        /// Prints the deployment number the Monitor is currently in.
        /// </summary>
        /// <param name="deploymentNumber">The deployment number.</param>
        private static void PrintDeploymentNumber(int deploymentNumber)
        {
            $"    Starting deployment ID {deploymentNumber} - {DateTime.Now.ToLongDateString()} {DateTime.Now.ToLongTimeString()}"
                .WriteLine(ConsoleColor.Green);
        }

        /// <summary>
        /// Creates the shell stream for interactive mode.
        /// </summary>
        /// <param name="sshClient">The SSH client.</param>
        /// <returns></returns>
        private static ShellStream CreateShellStream(SshClient sshClient)
        {
            var terminalModes = new Dictionary<TerminalModes, uint> {{TerminalModes.ECHO, 1}, {TerminalModes.IGNCR, 1}};

            var bufferWidth = (uint) Console.BufferWidth;
            var bufferHeight = (uint) Console.BufferHeight;
            var windowWidth = (uint) Console.WindowWidth;
            var windowHeight = (uint) Console.WindowHeight;
            var bufferSize = Console.BufferWidth * Console.BufferHeight;

            var shell = sshClient.CreateShellStream(TerminalName, bufferWidth, bufferHeight, windowWidth, windowHeight,
                bufferSize, terminalModes);

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
                            if (_forwardShellStreamOutput)
                                Console.Write((char) rxByte);
                        }
                        else if (rxByte == 7)
                        {
                            if (_forwardShellStreamOutput)
                                Console.Beep();
                        }
                        else
                        {
                            if (_forwardShellStreamOutput)
                                $"[NPC {rxByte}]".WriteLine(ConsoleColor.DarkYellow);
                        }

                        rxBytePrevious = rxByte;
                        continue;
                    }

                    // If we are already inside an escape sequence . . .
                    if (isInEscapeSequence)
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
                            escapeSequenceType = 0;
                        }

                        // Detect if it's the last byte of the escape sequence (64 to 126)
                        // This last character determines the command to execute
                        var endOfSequenceType91 = escapeSequenceType == (byte) '[' && (rxByte >= 64 && rxByte <= 126);
                        var endOfSequenceType93 = escapeSequenceType == (byte) ']' && (rxByte == 7);
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

            shell.ErrorOccurred += (s, e) => PrintException(e.Exception);

            return shell;
        }

        /// <summary>
        /// Creates a new deployment cycle.
        /// </summary>
        /// <param name="sshClient">The SSH client.</param>
        /// <param name="sftpClient">The SFTP client.</param>
        /// <param name="shellStream">The shell stream.</param>
        /// <param name="verbOptions">The verb options.</param>
        private static void CreateNewDeployment(SshClient sshClient, SftpClient sftpClient, ShellStream shellStream,
            MonitorVerbOptions verbOptions)
        {
            // At this point the change has been detected; Make sure we are not deploying
            string.Empty.WriteLine();

            if (_isDeploying)
            {
                "WARNING: Deployment already in progress. Deployment will not occur."
                    .WriteLine(ConsoleColor.DarkYellow);
                return;
            }

            // Lock Deployment
            _isDeploying = true;
            var stopwatch = new Stopwatch();
            stopwatch.Start();

            try
            {
                _forwardShellStreamOutput = false;
                PrintDeploymentNumber(_deploymentNumber);
                RunSshClientCommand(sshClient, verbOptions);
                CreateTargetPath(sftpClient, verbOptions);
                PrepareTargetPath(sftpClient, verbOptions);
                UploadFilesToTarget(sftpClient, verbOptions.SourcePath, verbOptions.TargetPath, verbOptions.ExcludeFileSuffixes);
            }
            catch (Exception ex)
            {
                PrintException(ex);
            }
            finally
            {
                // Unlock deployment
                _isDeploying = false;
                _deploymentNumber++;
                stopwatch.Stop();
                $"    Finished deployment in {Math.Round(stopwatch.Elapsed.TotalSeconds, 2)} seconds."
                    .WriteLine(ConsoleColor.Green);

                _forwardShellStreamOutput = true;
                RunShellStreamCommand(shellStream, verbOptions);
            }
        }

        /// <summary>
        /// Normalizes the monitor verb options.
        /// </summary>
        /// <param name="verbOptions">The verb options.</param>
        private static void NormalizeMonitorVerbOptions(MonitorVerbOptions verbOptions)
        {
            var sourcePath = verbOptions.SourcePath.Trim();
            var targetPath = verbOptions.TargetPath.Trim();
            var monitorFile = Path.IsPathRooted(verbOptions.MonitorFile)
                ? Path.GetFullPath(verbOptions.MonitorFile)
                : Path.Combine(sourcePath, verbOptions.MonitorFile);

            verbOptions.TargetPath = targetPath;
            verbOptions.MonitorFile = monitorFile;
            verbOptions.SourcePath = sourcePath;
        }

        /// <summary>
        /// Starts the monitor mode.
        /// </summary>
        /// <param name="fsMonitor">The fs monitor.</param>
        /// <param name="sshClient">The SSH client.</param>
        /// <param name="sftpClient">The SFTP client.</param>
        /// <param name="shellStream">The shell stream.</param>
        /// <param name="verbOptions">The verb options.</param>
        private static void StartMonitorMode(FileSystemMonitor fsMonitor, SshClient sshClient, SftpClient sftpClient,
            ShellStream shellStream, MonitorVerbOptions verbOptions)
        {
            fsMonitor.FileSystemEntryChanged += (s, e) =>
            {
                // Detect changes to the monitor file by ignoring deletions and checking file paths.
                if (e.ChangeType != FileSystemEntryChangeType.FileAdded &&
                    e.ChangeType != FileSystemEntryChangeType.FileModified)
                    return;

                // If the change was not in the monitor file, then ignore it
                if (e.Path.ToLowerInvariant().Equals(verbOptions.MonitorFile.ToLowerInvariant()) == false)
                    return;

                // Create a new deployment once
                CreateNewDeployment(sshClient, sftpClient, shellStream, verbOptions);
            };

            "File System Monitor is now running.".WriteLine();
            "Writing a new monitor file will trigger a new deployment.".WriteLine();
            "Press H for help!".WriteLine();
            "Ground Control to Major Tom: Have a nice trip in space!.".WriteLine(ConsoleColor.DarkCyan);
        }

        /// <summary>
        /// Starts the user interaction.
        /// </summary>
        /// <param name="sshClient">The SSH client.</param>
        /// <param name="sftpClient">The SFTP client.</param>
        /// <param name="shellStream">The shell stream.</param>
        /// <param name="verbOptions">The verb options.</param>
        private static void StartUserInteraction(SshClient sshClient, SftpClient sftpClient, ShellStream shellStream,
            MonitorVerbOptions verbOptions)
        {
            _forwardShellStreamInput = false;

            while (true)
            {
                var readKey = Console.ReadKey(true);

                if (readKey.Key == ConsoleKey.F1)
                {
                    _forwardShellStreamInput = !_forwardShellStreamInput;
                    if (_forwardShellStreamInput)
                    {
                        Program.Title = "Monitor (Interactive)";
                        "    >> Entered console input forwarding.".WriteLine(ConsoleColor.Green);
                        _forwardShellStreamOutput = true;
                    }
                    else
                    {
                        Program.Title = "Monitor (Press H for Help)";
                        "    >> Left console input forwarding.".WriteLine(ConsoleColor.Red);
                    }

                    continue;
                }

                if (_forwardShellStreamInput)
                {
                    if (readKey.Key == ConsoleKey.Enter)
                    {
                        shellStream.Write("\r\n");
                    }
                    else
                    {
                        shellStream.WriteByte((byte) readKey.KeyChar);
                    }

                    shellStream.Flush();
                    continue;
                }

                switch (readKey.Key)
                {
                    case ConsoleKey.Q:
                        break;
                    case ConsoleKey.C:
                        Console.Clear();
                        break;
                    case ConsoleKey.N:
                        CreateNewDeployment(sshClient, sftpClient, shellStream, verbOptions);
                        break;
                    case ConsoleKey.E:
                        RunSshClientCommand(sshClient, verbOptions);
                        break;
                    case ConsoleKey.S:
                        RunShellStreamCommand(shellStream, verbOptions);
                        break;
                    case ConsoleKey.H:

                        const ConsoleColor helpColor = ConsoleColor.Cyan;
                        "Console help".WriteLine(helpColor);
                        "    H    Prints this screen".WriteLine(helpColor);
                        "    Q    Quits this application".WriteLine(helpColor);
                        "    C    Clears the screen".WriteLine(helpColor);
                        "    N    Force a deployment cycle".WriteLine(helpColor);
                        "    E    Run the Pre-deployment command".WriteLine(helpColor);
                        "    S    Run the Post-deployment command".WriteLine(helpColor);
                        "    F1   Toggle shell-interactive mode".WriteLine(helpColor);

                        string.Empty.WriteLine();
                        break;
                    default:
                        $"Unrecognized command '{readKey.KeyChar}' -- Press 'H' to get a list of available commands."
                            .WriteLine(ConsoleColor.Red);
                        break;
                }
            }
        }

        #endregion

        #region Main Verb Methods

        /// <summary>
        /// Executes the monitor verb. Using a legacy method.
        /// </summary>
        /// <param name="verbOptions">The verb options.</param>
        /// <exception cref="DirectoryNotFoundException">Source Path ' + sourcePath + ' was not found.</exception>
        public static void ExecuteMonitorVerbLegacy(MonitorVerbOptions verbOptions)
        {
            // Initialize Variables
            _isDeploying = false;
            _deploymentNumber = 1;

            // Normalize and show the options to the user so he knows what he's doing
            NormalizeMonitorVerbOptions(verbOptions);
            PrintMonitorOptions(verbOptions);

            // Create the FS Monitor and connection info
            var fsMonitor = new FileSystemMonitor(1, verbOptions.SourcePath);
            var simpleConnectionInfo = new PasswordConnectionInfo(verbOptions.Host, verbOptions.Port,
                verbOptions.Username, verbOptions.Password);

            // Validate source path exists
            if (Directory.Exists(verbOptions.SourcePath) == false)
                throw new DirectoryNotFoundException("Source Path '" + verbOptions.SourcePath + "' was not found.");

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
                        // Starts the FS Monitor and binds the event handler
                        StartMonitorMode(fsMonitor, sshClient, sftpClient, shellStream, verbOptions);

                        // Allows user interaction with the shell
                        StartUserInteraction(sshClient, sftpClient, shellStream, verbOptions);

                        // When we quit, we stop the monitor and disconnect the clients
                        StopMonitorMode(sftpClient, sshClient, fsMonitor);
                    }
                }
            }
        }

        /// <summary>
        /// Executes the monitor verb. This is the main method.
        /// </summary>
        /// <param name="verbOptions">The verb options.</param>
        /// <exception cref="DirectoryNotFoundException">Source Path ' + sourcePath + ' was not found.</exception>
        public static void ExecuteMonitorVerb(MonitorVerbOptions verbOptions)
        {
            // Initialize Variables
            _isDeploying = false;
            _deploymentNumber = 1;

            // Normalize and show the options to the user so he knows what he's doing
            NormalizeMonitorVerbOptions(verbOptions);
            PrintMonitorOptions(verbOptions);

            // Create connection info
            var simpleConnectionInfo = new PasswordConnectionInfo(verbOptions.Host, verbOptions.Port,
                verbOptions.Username, verbOptions.Password);

            // Create a file watcher
            var watcher = new FileSystemWatcher
            {
                Path = verbOptions.SourcePath,
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName,
                Filter = Path.GetFileName(verbOptions.MonitorFile)
            };

            // Validate source path exists
            if (Directory.Exists(verbOptions.SourcePath) == false)
                throw new DirectoryNotFoundException($"Source Path \'{verbOptions.SourcePath}\' was not found.");

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
                        // Adds an onChange event and enables it
                        watcher.Changed += (s, e) =>
                            CreateNewDeployment(sshClient, sftpClient, shellStream, verbOptions);
                        watcher.EnableRaisingEvents = true;

                        "File System Monitor is now running.".WriteLine();
                        "Writing a new monitor file will trigger a new deployment.".WriteLine();
                        "Press H for help!".WriteLine();
                        "Ground Control to Major Tom: Have a nice trip in space!.".WriteLine(ConsoleColor.DarkCyan);

                        // Allows user interaction with the shell
                        StartUserInteraction(sshClient, sftpClient, shellStream, verbOptions);

                        // When we quit, we stop the monitor and disconnect the clients
                        StopMonitorMode(sftpClient, sshClient, watcher);
                    }
                }
            }
        }
        #endregion
    }
}