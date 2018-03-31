using System;
using System.CodeDom;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NDesk.Options;
using static System.Console;

namespace Broccoli {
    /// <summary>
    ///     The main class of the Broccoli .NET interpreter.
    /// </summary>
    public static class Program {

        public static void Main(string[] args) {
            // TODO: remove as many try/catches as possible, this isn't Python, nor is it Java
            // TODO: do we even need row/col in ParseNode
            string file;
            bool getHelp = false, useREPL = args.Length == 0, isCauliflower = false;

            var options = new OptionSet {
                {
                    "h|help", "Show help", n => getHelp = n != null
                }, {
                    "r|repl", "Use REPL", n => useREPL = n != null
                }, {
                    "c|cauliflower", "Use Cauliflower", n => isCauliflower = n != null
                }
            };
            IEnumerable<string> argv = options.Parse(args);
            var interpreter = isCauliflower
                ? new CauliflowerInterpreter(new BList(argv.Skip(1).Select(i => new BString(i))))
                : new Interpreter();
            var prompt = isCauliflower ? "cauliflower> " : "broccoli> ";
            var continuationPrompt = isCauliflower ? "           > " : "        > ";
            file = argv.FirstOrDefault();
            if (file is null)
                useREPL = true;
            argv = argv.Skip(1);

            if (useREPL)
                while (true) {
                    if (CursorLeft != 0)
                        WriteLine();
                    ForegroundColor = ConsoleColor.Green;
                    Write(prompt);
                    ForegroundColor = ConsoleColor.White;

                    string ReadOrDie() {
                        var line = ReadLine();
                        if (line is null)
                            Environment.Exit(0);
                        return line;
                    }

                    ParseNode parsed = null;
                    try {
                        parsed = interpreter.Parse(ReadOrDie() + '\n');
                    } catch (Exception e) {
                        WriteLine(e);
                        WriteLine($"{e.GetType().ToString().Split('.').Last()}: {e.Message}");
                        continue;
                    }

                    while (!parsed.Finished) {
                        ForegroundColor = ConsoleColor.Green;
                        Write(continuationPrompt);
                        ForegroundColor = ConsoleColor.White;
                        try {
                            parsed = interpreter.Parse(ReadOrDie() + '\n', parsed);
                        } catch (Exception e) {
                            WriteLine(e);
                            WriteLine($"{e.GetType().ToString().Split('.').Last()}: {e.Message}");
                        }
                    }

                    try {
                        var result = interpreter.Run(parsed);
                        if (result != null)
                            WriteLine(result.Inspect());
                    } catch (Exception e) {
                        WriteLine(e);
                        WriteLine($"{e.GetType().ToString().Split('.').Last()}: {e.Message}");
                    }
                }

            if (file == null || getHelp)
                GetHelp(options);

            interpreter.Run(file == "-" ? In.ReadToEnd() : File.ReadAllText(file));
        }

        /// <summary>
        ///     Prints all the available options to stdout.
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