﻿using System;
using NDesk.Options;
using System.IO;

namespace broccoli
{
    class Program
    {
        static int Main(string[] args)
        {
            if (args.Length == 0) {
                // todo start repl
            }

            string file = null;
            OptionSet options = new OptionSet(){
                {"h|help", "Show help", n=>{if(n!=null)GetHelp();}},
                {"<>", "File containing code to read, or - for StdIn.", f=>{
                        if (file == null)file=f; else GetHelp();
                    }
                }
            };

            options.Parse(args);

            if(file == null) {
                GetHelp();
            }

            var code = (file == "-") ? Console.In.ReadToEnd() : File.ReadAllText(file);

            Console.WriteLine("Hello World! -\U0001F966");

            return 0;
        }

        private static void GetHelp() {
            Console.Error.WriteLine("\tBroccoli .NET: C# based Broccoli interpreter.");
            Console.Error.WriteLine("Usage: broccoli <filename>");
            Environment.Exit(1);
        }

    }
}
