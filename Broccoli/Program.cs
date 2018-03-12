using System;
using NDesk.Options;
using System.IO;
using Broccoli.Tokenization;

namespace Broccoli {
    class Program {
        static int Main(string[] args) {
            TestTokenizer();

            if (args.Length == 0) {
                // todo start repl
            }

            string file = null;
            OptionSet options = new OptionSet() {
                {
                    "h|help", "Show help", n => {
                        if (n != null) GetHelp();
                    }
                }, {
                    "<>", "File containing code to read, or - for StdIn.", f => {
                        if (file == null)
                            file = f;
                        else // Can't read code from multiple files
                            GetHelp();
                    }
                }
            };

            options.Parse(args);

            if (file == null) {
                GetHelp();
            }

            var code = (file == "-") ? Console.In.ReadToEnd() : File.ReadAllText(file);
            var broccoli = new Broccoli(code);

            broccoli.Run();

            return 0;
        }

        private static void GetHelp() {
            Console.Error.WriteLine("\tBroccoli .NET: C# based Broccoli interpreter.");
            Console.Error.WriteLine("Usage: broccoli <filename>");

            Environment.Exit(1);
        }

        private static void TestTokenizer() {
            var tokenizer = new Tokenizer(
                @"(fn p ($a)
    (:= $d 0)
    (for $i in (range 2 $a)
        (if (= $a (* $i (int (/ $a $i))))
            (:= $d (+ $d 1))
        )
    )
    (= $d 1)
)
(map p (range 1 20))"
            );

            Console.WriteLine(string.Join<SExpression>('\n', tokenizer.RootSExps));
            Environment.Exit(0);
        }
    }
}