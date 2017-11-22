using System;
using System.Threading;
using Unosquare.Labs.SshDeploy.Options;
using Unosquare.Swan;
using Unosquare.Swan.Components;

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
                Environment.ExitCode = -1;
                return;
            }

            #endregion

            $"SSH Deployment Tool [Version {typeof(Unosquare.Labs.SshDeploy.Program).Assembly.GetName().Version.ToString()}]".WriteLine();
            $"(c)2015 - 2016 Unosquare SA de CV. All Rights Reserved.".WriteLine();
            $"For additional help, please visit https://github.com/unosquare/sshdeploy".WriteLine();
            var options = new CliOptions();
            
                Runtime.ArgumentParser.ParseArguments(args, options);
      
            Console.ReadKey();


        }
    }
}
