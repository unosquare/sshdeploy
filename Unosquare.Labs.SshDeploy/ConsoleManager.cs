using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Unosquare.Labs.SshDeploy
{
    public static class ConsoleManager
    {
        public static readonly ConsoleColor DefaultForeground = Console.ForegroundColor;
        

        public static bool Verbose { get; set; }
        public static ConsoleColor ErrorColor { get; set; }

        static ConsoleManager()
        {
            Verbose = true;
            ErrorColor = ConsoleColor.Red;
        }

        public static void WriteLine(string text, ConsoleColor color)
        {
            Write(text + Environment.NewLine, color);
        }

        public static void Write(string text, ConsoleColor color)
        {
            if (Verbose == false) return;
            Console.ForegroundColor = color;
            Console.Write(text);
            Console.ForegroundColor = DefaultForeground;
        }

        public static void Write(string text)
        {
            Write(text, DefaultForeground);
        }

        public static void WriteLine(string text)
        {
            Write(text + Environment.NewLine, DefaultForeground);
        }

        public static void ErrorWrite(string text)
        {
            if (Verbose == false) return;
            Console.ForegroundColor = ErrorColor;
            Console.Error.Write(text);
            Console.ForegroundColor = DefaultForeground;
        }

        public static void ErrorWriteLine(string text)
        {
            ErrorWrite(text + Environment.NewLine);
        }
    }
}
