namespace Unosquare.Labs.SshDeploy
{
    using Options;
    using Renci.SshNet;
    using Swan;
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Text;

    public partial class DeploymentManager
    {
        internal static void ExecutePushVerb(PushVerbOptions verbOptions)
        {
            NormalizePushVerbOptions(verbOptions);
            PrintPushOptions(verbOptions);
            var builder = new StringBuilder("BuildingInsideSshDeploy=true");
            if (!string.IsNullOrWhiteSpace(verbOptions.Configuration))
                builder.Append($";Configuration={verbOptions.Configuration}");
            if (!string.IsNullOrWhiteSpace(verbOptions.Framework) && !verbOptions.SkipBuildTargetFramework)
                builder.Append($";TargetFramework={verbOptions.Framework}");
            if (!string.IsNullOrWhiteSpace(verbOptions.Runtime))
                builder.Append($";RuntimeIdentifier={verbOptions.Runtime}");
            //builder.Append("PreBuildEvent=\"\";PostBuildEvent=\"\"");

            var arguments = " msbuild -restore /t:Publish " +
                $" /p:{builder}";
            var psi = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = arguments,
            };
            Console.WriteLine($"building with command: dotnet {arguments}");
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
            var simpleConnectionInfo = DeploymentManager.GetConnectionInfo(verbOptions);

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
            if (!string.IsNullOrWhiteSpace(verbOptions.KeyPath))
                Terminal.WriteLine($"    KeyPath        {verbOptions.KeyPath}", ConsoleColor.DarkYellow);
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
                
                if (verbOptions.UseSync)
                {
                    CreateTargetPath(sftpClient, verbOptions);
                    SyncDirectories(sftpClient, verbOptions.SourcePath, verbOptions.TargetPath);
                }
                else if (verbOptions.UseZip)
                {
                    var zipPath = GetRemoteZipFilename(sftpClient, verbOptions.SourcePath, verbOptions.TargetPath);
                    PrepareTargetPath(sftpClient, verbOptions, sshClient);
                    CreateTargetPath(sftpClient, verbOptions);
                    Unzip(sshClient, verbOptions, zipPath);
                }
                else
                {
                    PrepareTargetPath(sftpClient, verbOptions, sshClient);
                    CreateTargetPath(sftpClient, verbOptions);
                    UploadFilesToTarget(sftpClient, verbOptions.SourcePath, verbOptions.TargetPath, verbOptions.ExcludeFileSuffixes);
                }                
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