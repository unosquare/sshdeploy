using System;
using System.Threading;
using Unosquare.Labs.SshDeploy.Options;

namespace Unosquare.Labs.SshDeploy
{
    static public class Program
    {
        private static readonly string MutexName = string.Format("Global\\{0}", typeof(Program).Namespace);
        static private Mutex AppMutex = null;

        static public string Title
        {
            get { return Console.Title; }
            set { Console.Title = value + TitleSuffix; }
        }

        static public string TitleSuffix { get; set; } = " - SSH Deploy";


        static void Main(string[] args)
        {
            Title = "Unosquare";

            #region Handle Single Instance Application

            bool isNewMutex;
            AppMutex = new Mutex(true, MutexName, out isNewMutex);
            if (isNewMutex == false)
            {
                AppMutex = null;
                Environment.ExitCode = CommandLine.Parser.DefaultExitCodeFail;
                return;
            }

            #endregion

            ConsoleManager.WriteLine("SSH Deployment Tool [Version " + typeof(Unosquare.Labs.SshDeploy.Program).Assembly.GetName().Version.ToString() + "]");
            ConsoleManager.WriteLine("(c) 2015-2016 Unosquare SA de CV. All Rights Reserved.");
            ConsoleManager.WriteLine("For additional help, please visit https://github.com/unosquare/sshdeploy");

            var invokedVerbName = string.Empty;
            CliVerbOptionsBase invokedVerbOptions = null;
            var options = new CliOptions();

            var parseResult = CommandLine.Parser.Default.ParseArguments(args, options, (verb, verbOptions) =>
            {
                invokedVerbName = verb;
                invokedVerbOptions = verbOptions as CliVerbOptionsBase;

                if (invokedVerbOptions != null)
                    ConsoleManager.Verbose = invokedVerbOptions.Verbose != 0;
            });

            if (parseResult == false)
            {
                Environment.ExitCode = CommandLine.Parser.DefaultExitCodeFail;
                return;
            }

            try
            {

                switch (invokedVerbName)
                {
                    case CliOptions.RunVerb:
                        {
                            TitleSuffix = " - Run Mode" + TitleSuffix;
                            Title = "Command";
                            var verbOptions = invokedVerbOptions as RunVerbOptions;
                            DeploymentManager.ExecuteRunVerb(verbOptions);
                            break;
                        }
                    case CliOptions.ShellVerb:
                        {
                            TitleSuffix = " - Shell Mode" + TitleSuffix;
                            Title = "Interactive";
                            var verbOptions = invokedVerbOptions as ShellVerbOptions;
                            DeploymentManager.ExecuteShellVerb(verbOptions);
                            break;
                        }
                    case CliOptions.MonitorVerb:
                        {
                            TitleSuffix = " - Monitor Mode" + TitleSuffix;
                            Title = "Monitor";
                            var verbOptions = invokedVerbOptions as MonitorVerbOptions;
                            DeploymentManager.ExecuteMonitorVerb(verbOptions);
                            break;
                        }
                }
            }
            catch (Exception ex)
            {
                ConsoleManager.ErrorWriteLine("Error - " + ex.GetType().Name);
                ConsoleManager.ErrorWriteLine(ex.Message);
                ConsoleManager.ErrorWriteLine(ex.StackTrace);
                Environment.ExitCode = CommandLine.Parser.DefaultExitCodeFail;
            }

            if (Environment.ExitCode != 0)
                ConsoleManager.ErrorWriteLine("Completed with errors. Exit Code " + Environment.ExitCode.ToString());
            else
                ConsoleManager.WriteLine("Completed.");
        }
    }

}
