namespace Unosquare.Labs.SshDeploy
{
    using Renci.SshNet;
    using System;
    using System.Diagnostics;
    using System.IO;
    using Options;
    using Swan;
    using System.Linq;
    using System.Collections.Generic;
    using Unosquare.Swan.Formatters;
    using Unosquare.Labs.SshDeploy.Utils;
    using System.Text.RegularExpressions;

    public partial class DeploymentManager
    {
        private static string DependencyRegex => @"runtimes*[\/]+.*\.(dll|a|so|txt)";
        internal static void ExecutePushVerb(PushVerbOptions verbOptions)
        {
            NormalizePushVerbOptions(verbOptions);
            PrintPushOptions(verbOptions);

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

        private static Dictionary<string, object> LoopJsonObj(object dic, params string[] search)
        {
            var obj = (Dictionary<string, object>)dic;
            foreach (var item in search)
            {
                if (obj.ContainsKey(item))
                {
                    obj = (Dictionary<string, object>)obj.First(x => x.Key.Equals(item)).Value;
                }
                else
                {
                    return null;
                }
            }

            return obj;
        }

        private static List<string> GetDependencies(string path, string runtime)
        {
            var dependencylist = new List<string>();
            var filename = Directory
                  .EnumerateFiles(path, "*.deps.json")
                  .FirstOrDefault();

            if (String.IsNullOrEmpty(filename))
                return dependencylist;

            string data = File.ReadAllText(filename);
           
            var json = Json.Deserialize(data);
            var projectVersion = LoopJsonObj(json, "targets").FirstOrDefault(x => x.Key.Contains(runtime));

            if (projectVersion.Value == null)
            {
                $"No dependencies found ".Warn();
                return dependencylist;
            }

            var res = ((Dictionary<string, object>)projectVersion.Value).First();
            var dependencies = ((Dictionary<string, object>)res.Value).First(x => x.Key.Equals("dependencies"));
            var osrun = ((Dictionary<string, object>)projectVersion.Value).FirstOrDefault(x => x.Key.Contains("runtime." + runtime));
            var runtimelibs = ((Dictionary<string, object>)osrun.Value).FirstOrDefault(x => x.Key.Equals("runtime"));

            dependencylist = Regex.Matches(data,DependencyRegex).Cast<Match>().Select(x => Path.Combine(osrun.Key, x.Value)).ToList();

            foreach (var item in (Dictionary<string, object>)dependencies.Value)
            {
                var deps = LoopJsonObj(projectVersion.Value, item.Key + "/" + item.Value, "runtime");
                if (deps != null)
                {
                    dependencylist.Add(Path.Combine(item.Key, item.Value.ToString(), deps.First().Key));
                    var childDeps = LoopJsonObj(projectVersion.Value, item.Key + "/" + item.Value, "dependencies");
                    foreach (var child in childDeps)
                    {
                        var childSearch = LoopJsonObj(projectVersion.Value, child.Key + "/" + child.Value, "runtime");
                        if (childSearch != null)
                        {
                            dependencylist.Add(Path.Combine(child.Key, child.Value.ToString(), childSearch.First().Key));
                        }
                    }
                }
            }

            return dependencylist;
        }

        private static void NormalizePushVerbOptions(PushVerbOptions verbOptions)
        {
            var targetPath = verbOptions.TargetPath.Trim();

            verbOptions.TargetPath = targetPath;
        }

        private static void UploadDependencies(SftpClient sftpClient, string targetPath, List<string> dependencies)
        {
            $"    Deploying {dependencies.Count} dependencies.".WriteLine(ConsoleColor.Green);
            var nugetPath = NuGetHelper.GetGlobalPackagesFolder();
            foreach (var file in dependencies)
            {
                var relativePath = Path.GetFileName(file);

                var fileTargetPath = Path.Combine(targetPath, relativePath)
                    .Replace(WindowsDirectorySeparatorChar, LinuxDirectorySeparatorChar);

                var targetDirectory = Path.GetDirectoryName(fileTargetPath)
                    .Replace(WindowsDirectorySeparatorChar, LinuxDirectorySeparatorChar);

                try
                {
                    using (var fileStream = File.OpenRead(Path.Combine(nugetPath, file)))
                    {
                        sftpClient.UploadFile(fileStream, fileTargetPath);
                        $"    {file}".WriteLine(ConsoleColor.Green);
                    }
                }
                catch (Exception e)
                {
                    e.Message.Error();
                }
            }
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
                UploadDependencies(sftpClient, verbOptions.TargetPath, GetDependencies(verbOptions.SourcePath, verbOptions.Runtime));
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