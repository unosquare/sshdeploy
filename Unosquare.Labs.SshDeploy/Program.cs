namespace Unosquare.Labs.SshDeploy
{
    using Options;
    using Swan;
    using Swan.Logging;
    using Swan.Parsers;
    using System;
    using System.IO;
    using System.Linq;
    using Utils;

    public static class Program
    {
        public static string Title
        {
            get => Console.Title;
            set => Console.Title = value + TitleSuffix;
        }

        public static string CurrentDirectory { get; } = Directory.GetCurrentDirectory();

        public static string TitleSuffix { get; set; } = " - SSH Deploy";

        public static string ResolveProjectFile()
        {
            var csproj = Directory
                .EnumerateFiles(Directory.GetCurrentDirectory(), "*.csproj", SearchOption.TopDirectoryOnly)
                .FirstOrDefault();
            return !string.IsNullOrWhiteSpace(csproj)
                ? csproj
                : Directory.EnumerateFiles(Directory.GetCurrentDirectory(), "*.fsproj", SearchOption.TopDirectoryOnly)
                    .FirstOrDefault();
        }

        private static void Main(string[] args)
        {
            Title = "Unosquare";

            Terminal.WriteLine($"SSH Deployment Tool [Version {typeof(Program).Assembly.GetName().Version}]");
            Terminal.WriteLine("(c)2015 - 2019 Unosquare SA de CV. All Rights Reserved.");
            Terminal.WriteLine("For additional help, please visit https://github.com/unosquare/sshdeploy");

            try
            {
                using var csproj = new CsProjFile<CsProjNuGetMetadata>(ResolveProjectFile());
                csproj.Metadata.ParseCsProjTags(ref args);
            }
            catch (UnauthorizedAccessException)
            {
                Terminal.WriteLine("Access to csproj file denied", ConsoleColor.Red);
            }
            catch (ArgumentNullException)
            {
                Terminal.WriteLine("No csproj file was found", ConsoleColor.DarkRed);
            }

            if (!ArgumentParser.Current.ParseArguments<CliOptions>(args, out var options))
            {
                Environment.ExitCode = 1;
                Terminal.Flush();
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
                Terminal.WriteLine("Completed.");
            }

            Terminal.Flush();
        }
    }
}