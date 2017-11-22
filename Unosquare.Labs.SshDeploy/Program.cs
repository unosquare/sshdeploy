using System;
using Unosquare.Labs.SshDeploy.Options;
using Unosquare.Swan;
using Unosquare.Swan.Components;

namespace Unosquare.Labs.SshDeploy
{
    class Program
    {
        public static void Main(string[] args)
        {
            var app = new ArgumentParser();
            var options = new CliOptions();
            var cli = new CliVerbOptionsBase();
            try
            {
                Runtime.ArgumentParser.ParseArguments(args, options);
            }
            catch( Exception e)
            {
                Console.WriteLine(e.Message);
            }

      
            Console.ReadKey();


        }
    }
}
