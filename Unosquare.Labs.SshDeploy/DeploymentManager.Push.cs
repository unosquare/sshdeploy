namespace Unosquare.Labs.SshDeploy
{
    using Renci.SshNet;
    using System;
    using System.Diagnostics;
    using System.IO;
    using Options;
    using Swan;

    public partial class DeploymentManager
    {
        internal static void ExecutePushVerb(PushVerbOptions verbOptions)
        {
            NormalizePushVerbOptions(verbOptions);
            PrintPushOptions(verbOptions);

            var psi = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = " msbuild /t:Publish " +
                $" /p:Configuration={verbOptions.Configuration};" +
                $"TargetFramework={verbOptions.Framework};RuntimeIdentifier={verbOptions.Runtime}"
            };
            var process = Process.Start(psi);
            process.WaitForExit();

            if (process.ExitCode != 0)
            {
                Console.Error.WriteLine("Invoking MSBuild target failed");
                Environment.ExitCode = 0;
                return;
            }

            if (Directory.Exists(verbOptions.SourcePath) == false)
                throw new DirectoryNotFoundException($"Source Path \'{verbOptions.SourcePath}\' was not found.");
            // Create connection info
            var simpleConnectionInfo = new PasswordConnectionInfo(verbOptions.Host, verbOptions.Port,
                verbOptions.Username, verbOptions.Password);

            // Instantiate an SFTP client and an SSH client
            // SFTP will be used to transfer the files and SSH to execute pre-deployment and post-deployment commands
            using (var sftpClient = new SftpClient(simpleConnectionInfo))
            {
                // SSH will be used to execute commands and to get the output back from the program we are running
                using (var sshClient = new SshClient(simpleConnectionInfo))
                {
                    // Connect SSH and SFTP clients
                    EnsureMonitorConnection(sshClient, sftpClient, verbOptions);
                    CreateNewDeployment(sshClient, sftpClient, verbOptions);
                }
            }
        }
        
        private static void NormalizePushVerbOptions(PushVerbOptions verbOptions)
        {
            var targetPath = verbOptions.TargetPath.Trim();

            verbOptions.TargetPath = targetPath;
        }

        private static void PrintPushOptions(PushVerbOptions verbOptions)
        {
            string.Empty.WriteLine();
            "Deploying....".WriteLine();
            $"    Configuration   {verbOptions.Configuration}".WriteLine(ConsoleColor.DarkYellow);
            $"    Framework       {verbOptions.Framework}".WriteLine(ConsoleColor.DarkYellow);
            $"    Source Path     {verbOptions.SourcePath}".WriteLine(ConsoleColor.DarkYellow);
            $"    Excluded Files  {string.Join("|", verbOptions.ExcludeFileSuffixes)}".WriteLine(
                ConsoleColor.DarkYellow);
            $"    Target Address  {verbOptions.Host}:{verbOptions.Port}".WriteLine(ConsoleColor.DarkYellow);
            $"    Username        {verbOptions.Username}".WriteLine(ConsoleColor.DarkYellow);
            $"    Target Path     {verbOptions.TargetPath}".WriteLine(ConsoleColor.DarkYellow);
            $"    Clean Target    {(verbOptions.CleanTarget ? "YES" : "NO")}".WriteLine(ConsoleColor.DarkYellow);
            $"    Pre Deployment  {verbOptions.PreCommand}".WriteLine(ConsoleColor.DarkYellow);
            $"    Post Deployment {verbOptions.PostCommand}".WriteLine(ConsoleColor.DarkYellow);
        }

        private static void CreateNewDeployment(
            SshClient sshClient, 
            SftpClient sftpClient, 
            PushVerbOptions verbOptions)
        {
            // At this point the change has been detected; Make sure we are not deploying
            string.Empty.WriteLine();

            // Lock Deployment
            _isDeploying = true;
            var stopwatch = new Stopwatch();
            stopwatch.Start();

            try
            {
                _forwardShellStreamOutput = false;
                RunCommand(sshClient, "client", verbOptions.PreCommand);
                CreateTargetPath(sftpClient, verbOptions);
                PrepareTargetPath(sftpClient, verbOptions);                
                UploadFilesToTarget(sftpClient, verbOptions.SourcePath, verbOptions.TargetPath,
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
                $"    Finished deployment in {Math.Round(stopwatch.Elapsed.TotalSeconds, 2)} seconds."
                    .WriteLine(ConsoleColor.Green);
                RunCommand(sshClient, "shell", verbOptions.PostCommand);
            }
        }
    }
}