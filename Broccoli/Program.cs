using System;
using static System.Console;
using NDesk.Options;
using System.IO;
using Broccoli.Parsing;
using System.Collections.Generic;
using System.Linq; 

namespace Broccoli {
    /// <summary>
    /// The main class of the Broccoli .NET interpreter.
    /// </summary>
    public static class Program {
        /// <summary>
        /// Represents whether the current Broccoli program uses the Cauliflower environment.
        /// </summary>
        public static bool IsCauliflower { get; set; }
        public static void Main(string[] args) {
            // TODO: remove as many try/catches as possible, this isn't Python, nor is it Java
            // TODO: do we even need row/col in ParseNode
            var broccoli = new Interpreter();
            string file;
            bool getHelp = false, useREPL = args.Length == 0;

            var options = new OptionSet {
                {
                    "h|help", "Show help", n => getHelp = n != null
                }, {
                    "r|repl", "Use REPL", n => useREPL = n != null
                }, {
                    "c|cauliflower", "Use Cauliflower", n => IsCauliflower = n != null
                }
            };
            
            IEnumerable<string> argv = options.Parse(args);
            file = argv.FirstOrDefault();
            if (file is null)
                useREPL = true;
            argv = argv.Skip(1);

            if (IsCauliflower) {
                broccoli.Builtins = Interpreter.AlternativeEnvironments["cauliflower"];
                broccoli.Scope.Lists["ARGV"] = new ValueList(argv.Select(i => new BString(i)));
            }

            if (useREPL)
                while (true) {
                    if (CursorLeft != 0)
                        WriteLine();
                    ForegroundColor = ConsoleColor.Green;
                    Write("broccoli> ");
                    ForegroundColor = ConsoleColor.White;
                    
                    string ReadOrDie() {
                        var line = ReadLine();
                        if (line is null)
                            Environment.Exit(0);
                        return line;
                    }

                    var parsed = Parser.Parse(ReadOrDie());
                    while (!parsed.Finished) {
                        ForegroundColor = ConsoleColor.Green;
                        Write("        > ");
                        ForegroundColor = ConsoleColor.White;
                        parsed = Parser.Parse(ReadOrDie(), parsed);
                    }

                    try {
                        var result = broccoli.Run(parsed);
                        if (result != null)
                            WriteLine(result);
                    } catch (Exception e) {
                        WriteLine($"Error: {e.Message}");
                    }
                }

            if (file == null || getHelp)
                GetHelp(options);

            broccoli.Run(file == "-" ? In.ReadToEnd() : File.ReadAllText(file));
        }

        /// <summary>
        /// Prints all the available options to stdout.
        /// </summary>
        /// <param name="o">The OptionSet to use.</param>
        private static void GetHelp(OptionSet o) {
            Error.WriteLine("Broccoli .NET: C# based Broccoli interpreter.\n");
            Error.WriteLine("Usage: broccoli [options] <filename>");
            o.WriteOptionDescriptions(Out);

            Environment.Exit(0);
        }
    }
}