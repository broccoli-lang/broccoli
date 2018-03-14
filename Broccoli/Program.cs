using System;
using NDesk.Options;
using System.IO;
using System.Linq;
using Broccoli.Parsing;

namespace Broccoli {
    class Program {
        public static int Main(string[] args) {
            // TODO: remove as many try/catches as possible, this isn't Python, nor is it Java
            // TODO: do we even need row/col in ParseNode
            Broccoli broccoli;

            if (args.Length == 0) {
                broccoli = new Broccoli();
                while (true) {
                    if (Console.CursorLeft != 0)
                        Console.WriteLine();
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.Write("broccoli> ");
                    Console.ForegroundColor = ConsoleColor.White;

                    var parsed = Parser.Parse(Console.ReadLine());
                    while (!parsed.Finished) {
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.Write("        > ");
                        Console.ForegroundColor = ConsoleColor.White;
                        parsed = Parser.Parse(Console.ReadLine(), parsed);
                    }

                    var result = broccoli.Run(parsed);
                    if (result != null)
                        Console.WriteLine(result);
                }
            }

            string file = null;
            bool getHelp = false;
            OptionSet options = new OptionSet {
                {
                    "h|help", "Show help", n => {
                        if (n != null) getHelp = true;
                    }
                }, {
                    "<>", "File containing code to read, or - for stdin.", f => {
                        if (file == null) file = f;
                    }
                }
            };

            options.Parse(args);

            if (file == null || getHelp) {
                GetHelp(options);
            }

            var code = file == "-" ? Console.In.ReadToEnd() : File.ReadAllText(file);
            broccoli = new Broccoli(code);

            broccoli.Run();

            Console.WriteLine("\n--- Debugging Info ---");
            broccoli.Scope.Scalars.ToList().ForEach(kv => Console.WriteLine($"{kv.Key} => {kv.Value}"));
            broccoli.Scope.Lists.ToList().ForEach(kv => Console.WriteLine($"{kv.Key} => {kv.Value}"));

            return 0;
        }

        private static void GetHelp(OptionSet o) {
            Console.Error.WriteLine("Broccoli .NET: C# based Broccoli interpreter.\n");
            Console.Error.WriteLine("Usage: broccoli [options] <filename>");
            o.WriteOptionDescriptions(Console.Out);

            Environment.Exit(0);
        }
    }
}