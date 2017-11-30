namespace Unosquare.Labs.SshDeploy
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using Options;
    using Swan;
    using Unosquare.Labs.SshDeploy.Attributes;
    using Unosquare.Labs.SshDeploy.util;

    public static class Program
    {
        private static readonly string MutexName = string.Format("Global\\{0}", typeof(Program).Namespace);
        private static Mutex _appMutex;

        public static string Title
        {
            get => Console.Title;
            set => Console.Title = value + TitleSuffix;
        }

        public static string CurrentDirectory { get; } = Directory.GetCurrentDirectory();

        public static string TitleSuffix { get; set; } = " - SSH Deploy";

        private static void Main(string[] args)
        {
            Title = "Unosquare";

            #region Handle Single Instance Application

            _appMutex = new Mutex(true, MutexName, out var isNewMutex);

            if (isNewMutex == false)
            {
                _appMutex = null;
                Environment.ExitCode = 1;
                return;
            }

            #endregion

            $"SSH Deployment Tool [Version {typeof(Program).Assembly.GetName().Version}]".WriteLine();
            "(c)2015 - 2017 Unosquare SA de CV. All Rights Reserved.".WriteLine();
            "For additional help, please visit https://github.com/unosquare/sshdeploy".WriteLine();

            var options = new CliOptions();
            if (args.Contains("push") | args.Contains("monitor"))
            {
                var projectFile = Directory.EnumerateFiles(Program.CurrentDirectory, "*.csproj", SearchOption.TopDirectoryOnly).FirstOrDefault();
                if (projectFile != null)
                {
                    using (var stream = File.Open(projectFile, FileMode.OpenOrCreate, FileAccess.ReadWrite))
                    using (var csproj = new CsProjFile(stream, leaveOpen: true))
                    {
                        if (args.Contains("push"))
                        {
                            csproj.NuGetMetadata.ParseCsProjTags(ref args, typeof(PushVerbOptions));
                        }
                        else
                        {
                            csproj.NuGetMetadata.ParseCsProjTags(ref args, typeof(MonitorAttribute));
                        }
                    }
                }
            }

            var parseResult = Runtime.ArgumentParser.ParseArguments(args, options);

            if (parseResult == false)
            {
                Environment.ExitCode = 1;
                Console.ReadKey();
                return;
            }

            try
            {
                if (options.RunVerbOptions != null)
                {
                    TitleSuffix = $" - Run Mode{TitleSuffix}";
                    Title = "Command";
                    DeploymentManager.ExecuteRunVerb(options.RunVerbOptions);
                }
                else if (options.ShellVerbOptions != null)
                {
                    TitleSuffix = $" - Shell Mode{TitleSuffix}";
                    Title = "Interactive";
                    DeploymentManager.ExecuteShellVerb(options.ShellVerbOptions);
                }
                else if (options.MonitorVerbOptions != null)
                {
                    TitleSuffix = $" - Monitor Mode{TitleSuffix}";
                    Title = "Monitor";

                    if (options.MonitorVerbOptions.Legacy)
                    {
                        DeploymentManager.ExecuteMonitorVerbLegacy(options.MonitorVerbOptions);
                    }
                    else
                    {
                        DeploymentManager.ExecuteMonitorVerb(options.MonitorVerbOptions);
                    }
                }
                else if (options.PushVerbOptions != null)
                {
                    TitleSuffix = $" - Push Mode{TitleSuffix}";
                    Title = "Push";
                    DeploymentManager.ExecutePushVerb(options.PushVerbOptions);
                    Console.ReadKey();
                }
            }
            catch (Exception ex)
            {
                $"Error - {ex.GetType().Name}".Error();
                ex.Message.Error();
                ex.StackTrace.Error();
                Environment.ExitCode = 1;
            }

            if (Environment.ExitCode != 0)
            {
                $"Completed with errors. Exit Code {Environment.ExitCode}".Error();
            }
            else
            {
                "Completed.".WriteLine();
            }
        }
    }
}