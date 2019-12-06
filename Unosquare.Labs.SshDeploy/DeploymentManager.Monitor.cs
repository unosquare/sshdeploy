﻿namespace Unosquare.Labs.SshDeploy
{
    using Options;
    using Renci.SshNet;
    using Renci.SshNet.Common;
    using Swan;
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using Swan.Logging;

    public partial class DeploymentManager
    {
        #region State Variables

        private static bool _forwardShellStreamOutput;
        private static bool _forwardShellStreamInput;
        private static bool _isDeploying;
        private static int _deploymentNumber;

        #endregion

        #region Main Verb Methods

        /// <summary>
        /// Executes the monitor verb. Using a legacy method.
        /// </summary>
        /// <param name="verbOptions">The verb options.</param>
        /// <exception cref="DirectoryNotFoundException">Source Path ' + sourcePath + ' was not found.</exception>
        internal static void ExecuteMonitorVerbLegacy(MonitorVerbOptions verbOptions)
        {
            // Initialize Variables
            _isDeploying = false;
            _deploymentNumber = 1;

            // Normalize and show the options to the user so he knows what he's doing
            NormalizeMonitorVerbOptions(verbOptions);
            PrintMonitorOptions(verbOptions);

            // Create the FS Monitor and connection info
            var fsmonitor = new FileSystemMonitor(1, verbOptions.SourcePath);
            var simpleConnectionInfo = new PasswordConnectionInfo(
                verbOptions.Host,
                verbOptions.Port,
                verbOptions.Username,
                verbOptions.Password);

            // Validate source path exists
            if (Directory.Exists(verbOptions.SourcePath) == false)
                throw new DirectoryNotFoundException("Source Path '" + verbOptions.SourcePath + "' was not found.");

            // Instantiate an SFTP client and an SSH client
            // SFTP will be used to transfer the files and SSH to execute pre-deployment and post-deployment commands
            using var sftpClient = new SftpClient(simpleConnectionInfo);

            // SSH will be used to execute commands and to get the output back from the program we are running
            using var sshClient = new SshClient(simpleConnectionInfo);

            // Connect SSH and SFTP clients
            EnsureMonitorConnection(sshClient, sftpClient, verbOptions);

            // Create the shell stream so we can get debugging info from the post-deployment command
            using var shellStream = CreateShellStream(sshClient);

            // Starts the FS Monitor and binds the event handler
            StartMonitorMode(fsmonitor, sshClient, sftpClient, shellStream, verbOptions);

            // Allows user interaction with the shell
            StartUserInteraction(sshClient, sftpClient, shellStream, verbOptions);

            // When we quit, we stop the monitor and disconnect the clients
            StopMonitorMode(sftpClient, sshClient, fsmonitor);
        }

        /// <summary>
        /// Executes the monitor verb. This is the main method.
        /// </summary>
        /// <param name="verbOptions">The verb options.</param>
        /// <exception cref="DirectoryNotFoundException">Source Path ' + sourcePath + ' was not found.</exception>
        internal static void ExecuteMonitorVerb(MonitorVerbOptions verbOptions)
        {
            // Initialize Variables
            _isDeploying = false;
            _deploymentNumber = 1;

            // Normalize and show the options to the user so he knows what he's doing
            NormalizeMonitorVerbOptions(verbOptions);
            PrintMonitorOptions(verbOptions);

            // Create connection info
            var simpleConnectionInfo = new PasswordConnectionInfo(
                verbOptions.Host,
                verbOptions.Port,
                verbOptions.Username,
                verbOptions.Password);

            // Create a file watcher
            var watcher = new FileSystemWatcher
            {
                Path = verbOptions.SourcePath,
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName,
                Filter = Path.GetFileName(verbOptions.MonitorFile),
            };

            // Validate source path exists
            if (Directory.Exists(verbOptions.SourcePath) == false)
                throw new DirectoryNotFoundException($"Source Path \'{verbOptions.SourcePath}\' was not found.");

            // Instantiate an SFTP client and an SSH client
            // SFTP will be used to transfer the files and SSH to execute pre-deployment and post-deployment commands
            using var sftpClient = new SftpClient(simpleConnectionInfo);

            // SSH will be used to execute commands and to get the output back from the program we are running
            using var sshClient = new SshClient(simpleConnectionInfo);

            // Connect SSH and SFTP clients
            EnsureMonitorConnection(sshClient, sftpClient, verbOptions);

            // Create the shell stream so we can get debugging info from the post-deployment command
            using var shellStream = CreateShellStream(sshClient);

            // Adds an onChange event and enables it
            watcher.Changed += (s, e) =>
                CreateNewDeployment(sshClient, sftpClient, shellStream, verbOptions);
            watcher.EnableRaisingEvents = true;

            Terminal.WriteLine("File System Monitor is now running.");
            Terminal.WriteLine("Writing a new monitor file will trigger a new deployment.");
            Terminal.WriteLine("Press H for help!");
            Terminal.WriteLine("Ground Control to Major Tom: Have a nice trip in space!.", ConsoleColor.DarkCyan);

            // Allows user interaction with the shell
            StartUserInteraction(sshClient, sftpClient, shellStream, verbOptions);

            // When we quit, we stop the monitor and disconnect the clients
            StopMonitorMode(sftpClient, sshClient, watcher);
        }

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
                    $"WARNING: Failed to delete file or folder '{file.FullName}'".Error(nameof(DeleteLinuxDirectoryRecursive));
                }
            }
        }

        /// <summary>
        /// Creates the linux directory recursively.
        /// </summary>
        /// <param name="client">The client.</param>
        /// <param name="path">The path.</param>
        /// <exception cref="ArgumentException">Argument path must start with  + LinuxDirectorySeparator.</exception>
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

            var pathParts = path.Split(new[] { LinuxDirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries);

            pathParts = pathParts.Skip(0).Take(pathParts.Length - 1).ToArray();
            var priorPath = LinuxDirectorySeparator + string.Join(LinuxDirectorySeparator, pathParts);

            if (pathParts.Length > 1)
                CreateLinuxDirectoryRecursive(client, priorPath);

            client.CreateDirectory(path);
        }

        /// <summary>
        /// Runs pre and post deployment commands over the SSH client.
        /// </summary>
        /// <param name="shellStream">The shell stream.</param>
        /// <param name="verbOptions">The verb options.</param>
        private static void RunShellStreamCommand(ShellStream shellStream, CliExecuteOptionsBase verbOptions)
        {
            var commandText = verbOptions.PostCommand;
            if (string.IsNullOrWhiteSpace(commandText)) return;

            Terminal.WriteLine("    Executing shell command.", ConsoleColor.Green);
            shellStream.Write($"{commandText}\r\n");
            shellStream.Flush();
            Terminal.WriteLine($"    TX: {commandText}", ConsoleColor.DarkYellow);
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

            Terminal.WriteLine("    Executing SSH client command.", ConsoleColor.Green);

            var result = RunCommand(sshClient, commandText);
            Terminal.WriteLine($"    SSH TX: {commandText}", ConsoleColor.DarkYellow);
            Terminal.WriteLine($"    SSH RX: [{result.ExitStatus}] {result.Result} {result.Error}", ConsoleColor.DarkYellow);
        }

        private static void RunCommand(SshClient sshClient, string type, string command)
        {
            if (string.IsNullOrWhiteSpace(command)) return;

            Terminal.WriteLine($"    Executing SSH {type} command.", ConsoleColor.Green);

            var result = RunCommand(sshClient, command);
            Terminal.WriteLine($"    SSH TX: {command}", ConsoleColor.DarkYellow);
            Terminal.WriteLine($"    SSH RX: [{result.ExitStatus}] {result.Result} {result.Error}", ConsoleColor.DarkYellow);
        }

        private static void AllowExecute(SshClient sshClient, PushVerbOptions verbOptions)
        {
            if (!bool.TryParse(verbOptions.Execute, out var value) || !value) return;

            Terminal.WriteLine("    Changing mode.", ConsoleColor.Green);
            var target = Path.Combine(verbOptions.TargetPath, "*").Replace(WindowsDirectorySeparatorChar, LinuxDirectorySeparatorChar);
            var command = $"chmod -R u+x {target}";

            var result = RunCommand(sshClient, command);
            Terminal.WriteLine($"    SSH TX: {command}", ConsoleColor.DarkYellow);
            Terminal.WriteLine($"    SSH RX: [{result.ExitStatus}] {result.Result} {result.Error}", ConsoleColor.DarkYellow);
        }

        private static SshCommand RunCommand(SshClient sshClient, string command) =>
            sshClient.RunCommand(command);

        /// <summary>
        /// Prints the currently supplied monitor mode options.
        /// </summary>
        /// <param name="verbOptions">The verb options.</param>
        private static void PrintMonitorOptions(MonitorVerbOptions verbOptions)
        {
            Terminal.WriteLine();
            Terminal.WriteLine("Monitor mode starting");
            Terminal.WriteLine("Monitor parameters follow: ");
            Terminal.WriteLine($"    Monitor File    {verbOptions.MonitorFile}", ConsoleColor.DarkYellow);
            Terminal.WriteLine($"    Source Path     {verbOptions.SourcePath}", ConsoleColor.DarkYellow);
            Terminal.WriteLine($"    Excluded Files  {string.Join("|", verbOptions.ExcludeFileSuffixes)}",  ConsoleColor.DarkYellow);
            Terminal.WriteLine($"    Target Address  {verbOptions.Host}:{verbOptions.Port}", ConsoleColor.DarkYellow);
            Terminal.WriteLine($"    Username        {verbOptions.Username}", ConsoleColor.DarkYellow);
            Terminal.WriteLine($"    Target Path     {verbOptions.TargetPath}", ConsoleColor.DarkYellow);
            Terminal.WriteLine($"    Clean Target    {(verbOptions.CleanTarget ? "YES" : "NO")}", ConsoleColor.DarkYellow);
            Terminal.WriteLine($"    Pre Deployment  {verbOptions.PreCommand}", ConsoleColor.DarkYellow);
            Terminal.WriteLine($"    Post Deployment {verbOptions.PostCommand}", ConsoleColor.DarkYellow);
        }

        /// <summary>
        /// Checks that both, SFTP and SSH clients have a working connection. If they don't it attempts to reconnect.
        /// </summary>
        /// <param name="sshClient">The SSH client.</param>
        /// <param name="sftpClient">The SFTP client.</param>
        /// <param name="verbOptions">The verb options.</param>
        private static void EnsureMonitorConnection(
            SshClient sshClient,
            SftpClient sftpClient,
            CliVerbOptionsBase verbOptions)
        {
            if (sshClient.IsConnected == false)
            {
                Terminal.WriteLine($"Connecting to host {verbOptions.Host}:{verbOptions.Port} via SSH.");
                sshClient.Connect();
            }

            if (sftpClient.IsConnected == false)
            {
                Terminal.WriteLine($"Connecting to host {verbOptions.Host}:{verbOptions.Port} via SFTP.");
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

            Terminal.WriteLine($"    Target Path '{verbOptions.TargetPath}' does not exist. -- Will attempt to create.", ConsoleColor.Green);
            CreateLinuxDirectoryRecursive(sftpClient, verbOptions.TargetPath);
            Terminal.WriteLine($"    Target Path '{verbOptions.TargetPath}' created successfully.", ConsoleColor.Green);
        }

        /// <summary>
        /// Prepares the given target path for deployment. If clean target is false, it does nothing.
        /// </summary>
        /// <param name="sftpClient">The SFTP client.</param>
        /// <param name="verbOptions">The verb options.</param>
        private static void PrepareTargetPath(SftpClient sftpClient, CliExecuteOptionsBase verbOptions)
        {
            if (!verbOptions.CleanTarget) return;
            Terminal.WriteLine($"    Cleaning Target Path '{verbOptions.TargetPath}'", ConsoleColor.Green);
            DeleteLinuxDirectoryRecursive(sftpClient, verbOptions.TargetPath);
        }

        /// <summary>
        /// Uploads the files in the source Windows path to the target Linux path.
        /// </summary>
        /// <param name="sftpClient">The SFTP client.</param>
        /// <param name="sourcePath">The source path.</param>
        /// <param name="targetPath">The target path.</param>
        /// <param name="excludeFileSuffixes">The exclude file suffixes.</param>
        private static void UploadFilesToTarget(
            SftpClient sftpClient,
            string sourcePath,
            string targetPath,
            string[] excludeFileSuffixes)
        {
            var filesInSource = Directory.GetFiles(
                sourcePath,
                FileSystemMonitor.AllFilesPattern,
                SearchOption.AllDirectories);
            var filesToDeploy = filesInSource.Where(file => !excludeFileSuffixes.Any(file.EndsWith))
                .ToList();

            Terminal.WriteLine($"    Deploying {filesToDeploy.Count} files.", ConsoleColor.Green);

            foreach (var file in filesToDeploy)
            {
                var relativePath = MakeRelativePath(file, sourcePath + Path.DirectorySeparatorChar);
                var fileTargetPath = Path.Combine(targetPath, relativePath)
                    .Replace(WindowsDirectorySeparatorChar, LinuxDirectorySeparatorChar);
                var targetDirectory = Path.GetDirectoryName(fileTargetPath)
                    .Replace(WindowsDirectorySeparatorChar, LinuxDirectorySeparatorChar);

                CreateLinuxDirectoryRecursive(sftpClient, targetDirectory);

                using var fileStream = File.OpenRead(file);
                sftpClient.UploadFile(fileStream, fileTargetPath);
            }
        }

        /// <summary>
        /// Makes the given path relative to an absolute path.
        /// </summary>
        /// <param name="filePath">The file path.</param>
        /// <param name="referencePath">The reference path.</param>
        /// <returns>Relative path.</returns>
        private static string MakeRelativePath(string filePath, string referencePath)
        {
            var fileUri = new Uri(filePath);
            var referenceUri = new Uri(referencePath);
            return referenceUri.MakeRelativeUri(fileUri).ToString();
        }

        private static void StopMonitorMode(SftpClient sftpClient, SshClient sshClient, FileSystemMonitor fsmonitor)
        {
            Terminal.WriteLine();

            fsmonitor.Stop();
            Terminal.WriteLine("File System monitor was stopped.");

            if (sftpClient.IsConnected)
                sftpClient.Disconnect();

            Terminal.WriteLine("SFTP client disconnected.");

            if (sshClient.IsConnected)
                sshClient.Disconnect();

            Terminal.WriteLine("SSH client disconnected.");
            Terminal.WriteLine("Application will exit now.");
        }

        private static void StopMonitorMode(SftpClient sftpClient, SshClient sshClient, FileSystemWatcher watcher)
        {
            Terminal.WriteLine();

            watcher.EnableRaisingEvents = false;
            Terminal.WriteLine("File System monitor was stopped.");

            if (sftpClient.IsConnected)
                sftpClient.Disconnect();

            Terminal.WriteLine("SFTP client disconnected.");

            if (sshClient.IsConnected)
                sshClient.Disconnect();

            Terminal.WriteLine("SSH client disconnected.");
            Terminal.WriteLine("Application will exit now.");
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
            Terminal.WriteLine($"    Starting deployment ID {deploymentNumber} - {DateTime.Now.ToLongDateString()} {DateTime.Now.ToLongTimeString()}", ConsoleColor.Green);
        }

        private static ShellStream CreateShellStream(SshClient sshClient)
        {
            var shell = CreateBaseShellStream(sshClient);

            shell.DataReceived += OnShellDataRx;

            shell.ErrorOccurred += (s, e) => PrintException(e.Exception);

            return shell;
        }

        private static void OnShellDataRx(object sender, ShellDataEventArgs e)
        {
            var escapeSequenceBytes = new List<byte>(128);
            var isInEscapeSequence = false;
            byte rxbyteprevious = 0;
            byte escapeSequenceType = 0;
            var rxbuffer = e.Data;

            foreach (var rxByte in rxbuffer)
            {
                // We've found the beginning of an escapr sequence
                if (isInEscapeSequence == false && rxByte == Escape)
                {
                    isInEscapeSequence = true;
                    escapeSequenceBytes.Clear();
                    rxbyteprevious = rxByte;
                    continue;
                }

                // Print out the character if we are not in an escape sequence and it is a printable character
                if (isInEscapeSequence == false)
                {
                    if (rxByte >= 32 || (rxByte >= 8 && rxByte <= 13))
                    {
                        if (_forwardShellStreamOutput)
                            Console.Write((char)rxByte);
                    }
                    else if (rxByte == 7)
                    {
                        if (_forwardShellStreamOutput)
                            Console.Beep();
                    }
                    else
                    {
                        if (_forwardShellStreamOutput)
                            Terminal.WriteLine($"[NPC {rxByte}]", ConsoleColor.DarkYellow);
                    }

                    rxbyteprevious = rxByte;
                    continue;
                }

                // If we are already inside an escape sequence . . .
                // Add the byte to the escape sequence
                escapeSequenceBytes.Add(rxByte);

                // Ignore the second escape byte 91 '[' or ']'
                if (rxbyteprevious == Escape)
                {
                    rxbyteprevious = rxByte;
                    if (ControlSequenceInitiators.Contains(rxByte))
                    {
                        escapeSequenceType = rxByte;
                        continue;
                    }

                    escapeSequenceType = 0;
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
                        rxbyteprevious = rxByte;
                    }

                    continue;
                }

                rxbyteprevious = rxByte;
            }
        }

        /// <summary>
        /// Creates a new deployment cycle.
        /// </summary>
        /// <param name="sshClient">The SSH client.</param>
        /// <param name="sftpClient">The SFTP client.</param>
        /// <param name="shellStream">The shell stream.</param>
        /// <param name="verbOptions">The verb options.</param>
        private static void CreateNewDeployment(
            SshClient sshClient,
            SftpClient sftpClient,
            ShellStream shellStream,
            MonitorVerbOptions verbOptions)
        {
            // At this point the change has been detected; Make sure we are not deploying
            Terminal.WriteLine();

            if (_isDeploying)
            {
                Terminal.WriteLine("WARNING: Deployment already in progress. Deployment will not occur.", ConsoleColor.DarkYellow);
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
                UploadFilesToTarget(
                    sftpClient,
                    verbOptions.SourcePath,
                    verbOptions.TargetPath,
                    verbOptions.ExcludeFileSuffixes);
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
                Terminal.WriteLine($"    Finished deployment in {Math.Round(stopwatch.Elapsed.TotalSeconds, 2)} seconds.", ConsoleColor.Green);

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
        /// <param name="fsmonitor">The fs monitor.</param>
        /// <param name="sshClient">The SSH client.</param>
        /// <param name="sftpClient">The SFTP client.</param>
        /// <param name="shellStream">The shell stream.</param>
        /// <param name="verbOptions">The verb options.</param>
        private static void StartMonitorMode(
            FileSystemMonitor fsmonitor,
            SshClient sshClient,
            SftpClient sftpClient,
            ShellStream shellStream,
            MonitorVerbOptions verbOptions)
        {
            fsmonitor.FileSystemEntryChanged += (s, e) =>
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

            Terminal.WriteLine("File System Monitor is now running.");
            Terminal.WriteLine("Writing a new monitor file will trigger a new deployment.");
            Terminal.WriteLine("Press H for help!");
            Terminal.WriteLine("Ground Control to Major Tom: Have a nice trip in space!.", ConsoleColor.DarkCyan);
        }

        /// <summary>
        /// Starts the user interaction.
        /// </summary>
        /// <param name="sshClient">The SSH client.</param>
        /// <param name="sftpClient">The SFTP client.</param>
        /// <param name="shellStream">The shell stream.</param>
        /// <param name="verbOptions">The verb options.</param>
        private static void StartUserInteraction(
            SshClient sshClient,
            SftpClient sftpClient,
            ShellStream shellStream,
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
                        Terminal.WriteLine("    >> Entered console input forwarding.", ConsoleColor.Green);
                        _forwardShellStreamOutput = true;
                    }
                    else
                    {
                        Program.Title = "Monitor (Press H for Help)";
                        Terminal.WriteLine("    >> Left console input forwarding.", ConsoleColor.Red);
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
                        shellStream.WriteByte((byte)readKey.KeyChar);
                    }

                    shellStream.Flush();
                    continue;
                }

                switch (readKey.Key)
                {
                    case ConsoleKey.Q:
                        return;
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
                        Terminal.WriteLine("Console help", helpColor);
                        Terminal.WriteLine("    H    Prints this screen", helpColor);
                        Terminal.WriteLine("    Q    Quits this application", helpColor);
                        Terminal.WriteLine("    C    Clears the screen", helpColor);
                        Terminal.WriteLine("    N    Force a deployment cycle", helpColor);
                        Terminal.WriteLine("    E    Run the Pre-deployment command", helpColor);
                        Terminal.WriteLine("    S    Run the Post-deployment command", helpColor);
                        Terminal.WriteLine("    F1   Toggle shell-interactive mode", helpColor);

                        Terminal.WriteLine();
                        break;
                    default:
                        Terminal.WriteLine($"Unrecognized command '{readKey.KeyChar}' -- Press 'H' to get a list of available commands.", ConsoleColor.Red);
                        break;
                }
            }
        }

        #endregion
    }
}