using System;
using NDesk.Options;
using System.IO;
using System.Linq;
using Broccoli.Tokenization;

namespace Broccoli {
    class Program {
        public static int Main(string[] args) {
//            TestFunctions();
//            TestTokenizer();

            if (args.Length == 0) {
                // todo start repl
            }

            string file = null;
            bool getHelp = false;
            OptionSet options = new OptionSet {
                {
                    "h|help", "Show help", n => {
                        if (n != null) getHelp = true;
                    }
                }, {
                    "<>", "File containing code to read, or - for StdIn.", f => {
                        if (file == null) file = f;
                    }
                }
            };

            options.Parse(args);

            if (file == null || getHelp) {
                GetHelp(options);
            }

            var code = file == "-" ? Console.In.ReadToEnd() : File.ReadAllText(file);
            var broccoli = new Broccoli(code);

            broccoli.Run();

            Console.WriteLine("\n--- Debugging Info ---");
            broccoli.Scalars.ToList().ForEach(kv => Console.WriteLine($"{kv.Key} => {kv.Value}"));
            broccoli.Lists.ToList().ForEach(kv => Console.WriteLine($"{kv.Key} => {kv.Value}"));

            return 0;
        }

        private static void GetHelp(OptionSet o) {
            Console.Error.WriteLine("Broccoli .NET: C# based Broccoli interpreter.\n");
            Console.Error.WriteLine("Usage: broccoli [options] <filename>");
            o.WriteOptionDescriptions(Console.Out);

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

        private static void TestFunctions() {
            var brocc = new Broccoli("");
            var assignFn = brocc.Functions[":="];

            assignFn.Invoke(new IValue[] {new ScalarVar("test"), new Integer(5)});
            Console.WriteLine(((Integer) brocc.Scalars["test"]).Value);

            assignFn.Invoke(new IValue[] {new ScalarVar("thisShouldThrow")});
        }
    }
}