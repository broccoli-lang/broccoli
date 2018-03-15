using System;
using static System.Console;
using NDesk.Options;
using System.IO;
using Broccoli.Parsing;

namespace Broccoli {
    class Program {
        public static void Main(string[] args) {
            // TODO: remove as many try/catches as possible, this isn't Python, nor is it Java
            // TODO: do we even need row/col in ParseNode
            Broccoli broccoli = new Broccoli();
            string file = null;
            bool getHelp = false, useREPL = args.Length == 0, useCauliflower = false;

            OptionSet options = new OptionSet {
                {
                    "h|help", "Show help", n => getHelp = n != null
                }, {
                    "<>", "File containing code to read, or - for stdin.", f => file = file ?? f
                }, {
                    "r|repl", "Use REPL", n => useREPL = n != null
                }, {
                    "c|cauliflower", "Use Cauliflower", n => useCauliflower = n != null
                }
            };

            options.Parse(args);

            if (useCauliflower)
                broccoli.Builtins = Broccoli.AlternativeEnvironments["cauliflower"];

            if (useREPL)
                while (true) {
                    if (CursorLeft != 0)
                        WriteLine();
                    ForegroundColor = ConsoleColor.Green;
                    Write("broccoli> ");
                    ForegroundColor = ConsoleColor.White;

                    var parsed = Parser.Parse(ReadLine());
                    while (!parsed.Finished) {
                        ForegroundColor = ConsoleColor.Green;
                        Write("        > ");
                        ForegroundColor = ConsoleColor.White;
                        parsed = Parser.Parse(ReadLine(), parsed);
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

        private static void GetHelp(OptionSet o) {
            Error.WriteLine("Broccoli .NET: C# based Broccoli interpreter.\n");
            Error.WriteLine("Usage: broccoli [options] <filename>");
            o.WriteOptionDescriptions(Out);

            Environment.Exit(0);
        }
    }
}