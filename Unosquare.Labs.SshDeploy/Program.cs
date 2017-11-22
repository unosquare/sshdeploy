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
            var options = new CliOptions();
          
                Runtime.ArgumentParser.ParseArguments(args, options);
      
            Console.ReadKey();


        }
    }
}
