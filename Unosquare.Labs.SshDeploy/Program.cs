namespace Unosquare.Labs.SshDeploy
{
    using Options;
    using Swan;
    using System;
    using System.IO;
    using System.Linq;
    using Utils;
    using Swan.Components;
    using System.Threading.Tasks;

    public static class Program
    {
        public static string Title
        {
            get => Console.Title;
            set => Console.Title = value + TitleSuffix;
        }

        public static string CurrentDirectory { get; } = Directory.GetCurrentDirectory();

        public static string TitleSuffix { get; set; } = " - SSH Deploy";

        private static void Main(string[] args)
        {
            Terminal.Settings.OverrideIsConsolePresent = true;

            Title = "Unosquare";
            
            $"SSH Deployment Tool [Version {typeof(Program).Assembly.GetName().Version}]".WriteLine();
            "(c)2015 - 2017 Unosquare SA de CV. All Rights Reserved.".WriteLine();
            "For additional help, please visit https://github.com/unosquare/sshdeploy".WriteLine();

            var options = new CliOptions();

            using (var csproj = new CsProjFile<CsProjNuGetMetadata>())
            {
                csproj.Metadata.ParseCsProjTags(ref args);
            }

            var parseResult = Runtime.ArgumentParser.ParseArguments(args, options);

            if (parseResult == false)
            {
                Environment.ExitCode = 1;
                Task.Delay(400).Wait();
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

            Task.Delay(200).Wait();
        }
    }
}