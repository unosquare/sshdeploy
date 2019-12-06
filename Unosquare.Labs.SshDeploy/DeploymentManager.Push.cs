namespace Unosquare.Labs.SshDeploy
{
    using Options;
    using Renci.SshNet;
    using Swan;
    using System;
    using System.Diagnostics;
    using System.IO;

    public partial class DeploymentManager
    {
        internal static void ExecutePushVerb(PushVerbOptions verbOptions)
        {
            NormalizePushVerbOptions(verbOptions);
            PrintPushOptions(verbOptions);

            var psi = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = " msbuild -restore /t:Publish " +
                $" /p:Configuration={verbOptions.Configuration};BuildingInsideSshDeploy=true;" +
                $"TargetFramework={verbOptions.Framework};RuntimeIdentifier={verbOptions.Runtime};" +
                "PreBuildEvent=\"\";PostBuildEvent=\"\"",
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
            var simpleConnectionInfo = new PasswordConnectionInfo(verbOptions.Host, verbOptions.Port, verbOptions.Username, verbOptions.Password);

            // Instantiate an SFTP client and an SSH client
            // SFTP will be used to transfer the files and SSH to execute pre-deployment and post-deployment commands
            using var sftpClient = new SftpClient(simpleConnectionInfo);

            // SSH will be used to execute commands and to get the output back from the program we are running
            using var sshClient = new SshClient(simpleConnectionInfo);
            
            // Connect SSH and SFTP clients
            EnsureMonitorConnection(sshClient, sftpClient, verbOptions);
            CreateNewDeployment(sshClient, sftpClient, verbOptions);
        }
        
        private static void NormalizePushVerbOptions(PushVerbOptions verbOptions)
        {
            verbOptions.TargetPath = verbOptions.TargetPath.Trim();
        }

        private static void PrintPushOptions(PushVerbOptions verbOptions)
        {
            Terminal.WriteLine();
            Terminal.WriteLine("Deploying....");
            Terminal.WriteLine($"    Configuration   {verbOptions.Configuration}", ConsoleColor.DarkYellow);
            Terminal.WriteLine($"    Framework       {verbOptions.Framework}", ConsoleColor.DarkYellow);
            Terminal.WriteLine($"    Source Path     {verbOptions.SourcePath}", ConsoleColor.DarkYellow);
            Terminal.WriteLine($"    Excluded Files  {string.Join("|", verbOptions.ExcludeFileSuffixes)}", ConsoleColor.DarkYellow);
            Terminal.WriteLine($"    Target Address  {verbOptions.Host}:{verbOptions.Port}", ConsoleColor.DarkYellow);
            Terminal.WriteLine($"    Username        {verbOptions.Username}", ConsoleColor.DarkYellow);
            Terminal.WriteLine($"    Target Path     {verbOptions.TargetPath}", ConsoleColor.DarkYellow);
            Terminal.WriteLine($"    Clean Target    {(verbOptions.CleanTarget ? "YES" : "NO")}", ConsoleColor.DarkYellow);
            Terminal.WriteLine($"    Pre Deployment  {verbOptions.PreCommand}", ConsoleColor.DarkYellow);
            Terminal.WriteLine($"    Post Deployment {verbOptions.PostCommand}", ConsoleColor.DarkYellow);
        }

        private static void CreateNewDeployment(
            SshClient sshClient, 
            SftpClient sftpClient, 
            PushVerbOptions verbOptions)
        {
            // At this point the change has been detected; Make sure we are not deploying
            Terminal.WriteLine();

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
                UploadFilesToTarget(sftpClient, verbOptions.SourcePath, verbOptions.TargetPath,verbOptions.ExcludeFileSuffixes);
                AllowExecute(sshClient, verbOptions);
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
                RunCommand(sshClient, "shell", verbOptions.PostCommand);
            }
        }
    }
}