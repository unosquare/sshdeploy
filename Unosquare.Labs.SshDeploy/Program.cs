using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CommandLine;
using CommandLine.Text;
using Renci.SshNet;
using Renci.SshNet.Common;
using System.Threading;
using Unosquare.Labs.SshDeploy.Options;

namespace Unosquare.Labs.SshDeploy
{
    class Program
    {
        private static readonly string MutexName = string.Format("Global\\{0}", typeof(Program).Namespace);
        static private Mutex AppMutex = null;

        static void Main(string[] args)
        {
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

            Console.WriteLine("SSH Deployment Tool [Version " + typeof(Unosquare.Labs.SshDeploy.Program).Assembly.GetName().Version.ToString() + "]");
            Console.WriteLine("(c) 2015 Unosquare SA de CV. All Rights Reserved.");

            var invokedVerbName = string.Empty;
            CliVerbOptionsBase invokedVerbOptions = null;
            var options = new CliOptions();

            var parseResult = CommandLine.Parser.Default.ParseArguments(args, options, (verb, verbOptions) =>
            {
                invokedVerbName = verb;
                invokedVerbOptions = verbOptions as CliVerbOptionsBase;

                if (invokedVerbOptions != null)
                    ConsoleManager.Verbose = invokedVerbOptions.Verbose;
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
                            var verbOptions = invokedVerbOptions as RunVerbOptions;
                            DeploymentManager.ExecuteRunVerb(verbOptions);
                            break;
                        }
                    case CliOptions.ShellVerb:
                        {
                            var verbOptions = invokedVerbOptions as ShellVerbOptions;
                            DeploymentManager.ExecuteShellVerb(verbOptions);
                            break;
                        }
                    case CliOptions.MonitorVerb:
                        {
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
