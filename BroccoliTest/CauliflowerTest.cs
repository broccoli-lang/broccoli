using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Broccoli;
using System.Text;

namespace BroccoliTest {
    [TestClass]
    public class CauliflowerTest {
        private Interpreter _cauliflower;
        private Func<string, IValue> _run;
        private System.IO.StringReader _input;
        private Writer _output;

        private class Writer : System.IO.TextWriter {
            public override Encoding Encoding => Encoding.UTF8;

            private StringBuilder _lastOutput = new StringBuilder();

            public void Clear() {
                _lastOutput.Clear();
            }

            public override void Write(char c) {
                _lastOutput.Append(c);
            }

            public override string ToString() {
                return _lastOutput.ToString();
            }
        }

        private static BList ValueListFrom() {
            return new BList();
        }

        private static BList ValueListFrom(params int[] a) {
            return new BList(a.Select(i => (IValue) new BInteger(i)));
        }

        private void WriteInput(string input) {
            _input = new System.IO.StringReader(input);
            Console.SetIn(_input);
        }

        private string ReadOutput(params object[] ignored) {
            var result = _output.ToString();
            _output.Clear();
            return result;
        }

        [TestInitialize]
        public void Initialize() {
            _run = (_cauliflower = new CauliflowerInterpreter()).Run;
            Console.SetOut(_output = new Writer());
        }

        [TestMethod]
        public void TestLiterals() {
            Assert.AreEqual(_run("\"\\rfoo\\\"\\t\\n\""), new BString("\rfoo\"\t\n"), "String escaping does not work correctly");
            Assert.AreEqual(_run("\"\rfoo\n\""), new BString("\rfoo\n"), "Multiline strings do not work correctly");
            Assert.AreEqual(_run("'(1 2 (3 4 foo (\"bar\")))"), new BList(
                (BInteger) 1,
                (BInteger) 2,
                new BList(
                    (BInteger) 3,
                    (BInteger) 4,
                    (BAtom) "foo",
                    new BList(
                        (BString) "bar"
                    )
                )
            ), "Lists do not work correctly");
            Assert.AreEqual(_run("`((1 2) ('(baz quux) \"a\"))"), new BDictionary {
                { (BInteger) 1, (BInteger) 2},
                { new BList((BAtom) "baz", (BAtom) "quux"), (BString) "a"}
            }, "Dictionaries do not work correctly");
            Assert.AreEqual(_run("`((`((1 \"foo\") (asdf 123902.2)) 2) ('(baz quux) \"a\"))"), new BDictionary {
                { new BDictionary {
                    { (BInteger) 1, (BString) "foo" },
                    { (BAtom) "asdf", (BFloat) 123902.2 }
                }, (BInteger) 2},
                { new BList((BAtom) "baz", (BAtom) "quux"), (BString) "a"}
            }, "Dictionaries do not work correctly");
        }

        [TestMethod]
        public void TestComments() {
            Assert.AreEqual(_run("#| a #|;this is a comment.\n !@\r|#$%^&*()|#\n\"yey\""), new BString("yey"), "Comment does not work correctly");
        }

        [TestMethod]
        public void TestDicts() {
            Assert.AreEqual(_run("(mkdict)"), new BDictionary{}, "mkdict fails");
            Assert.AreEqual(_run("(setkey (mkdict) 1 2)"), new BDictionary{{(BInteger)1, (BInteger)2}},
             "setkey fails to add key");
            _run("(:= %dict (setkey (mkdict) 1 2))");
            Assert.AreEqual(_run("(setkey %dict 1 3)"), new BDictionary{{(BInteger)1, (BInteger)3}},
             "setkey fails to change key");
            Assert.AreEqual(_run("(rmkey %dict 1)"), new BDictionary{}, "rmkey fails");
            Assert.AreEqual(_run("(haskey %dict 1)"), BAtom.True, "haskey fails");
            Assert.AreNotEqual(_run("(haskey %dict 42)"), BAtom.True, "haskey fails");
            Assert.AreEqual(_run("(getkey %dict 1)"), (BInteger)2);
            _run("(:= %dict (setkey %dict \"foo\" \"bar\"))");
            CollectionAssert.AreEquivalent((BList)_run("(keys %dict)"), new BList((BInteger)1, (BString) "foo"));
            CollectionAssert.AreEquivalent((BList)_run("(values %dict)"), new BList((BInteger)2, (BString) "bar"));
            Assert.ThrowsException<Exception>(() => _run("(:= $s (mkdict))"), "Can assign dict to scalar var");
            Assert.ThrowsException<Exception>(() => _run("(:= %d 4)"), "Can assign scalar to dict var");
        }

        [TestMethod]
        public void TestIO() {
            WriteInput("asdf");
            Assert.AreEqual(_run("(input)"), (BString) "asdf", "Input does not work ocrrectly");
            Assert.AreEqual(ReadOutput(_run("(print foo)")), "foo", "Print does not work correctly for atoms");
            Assert.AreEqual(ReadOutput(_run("(print \"foo\")")), "foo", "Print does not work correctly for strings");
            Assert.AreEqual(ReadOutput(_run("(print 3.3)")), "3.3", "Print does not work correctly for numbers");
            Assert.AreEqual(ReadOutput(_run("(print '(foo bar))")), "(foo bar)", "Print does not work correctly for lists");
            Assert.AreEqual(ReadOutput(_run("(print `((foo bar)(baz quux))")), "(foo: bar, baz: quux)", "Print does not work correctly for dictionaries");
        }
        
        [TestMethod]
        public void TestInterop() {
            Assert.AreEqual(ReadOutput(_run("(c#-import System.Console) ($Console.Write \"foo {0} {1}\" t '(1 2))")), "foo True (1 2)", "Calling static methods directly does not work");
            Assert.AreEqual(ReadOutput(_run("(c#-import System.Console) (c#-static $Console.Write \"foo {0} {1}\" t '(1 2))")), "foo True (1 2)", "Calling static methods via value with c#-static does not work");
            Assert.AreEqual(ReadOutput(_run("(c#-import System.Console) (c#-static $Console Write \"foo {0} {1}\" t '(1 2))")), "foo True (1 2)", "Calling static methods via type and name with c#-static does not work");
            Assert.AreEqual(_run("(c#-import System.Text.RegularExpressions) (c#-method (c#-create $Regex \"asdf+\") IsMatch \"asdfghjkl\")"), BAtom.True, "Calling instance methods does not work");
            Assert.AreEqual(_run("(c#-import System.Text.RegularExpressions) (c#-method (c#-create $Regex \"asdf+\") IsMatch \"asfdghjkl\")"), BAtom.Nil, "Calling instance methods does not work");
        }

        [TestCleanup]
        public void Cleanup() {
            _cauliflower = null;
            _run = null;
            Console.SetOut(Console.Out);
            _output = null;
        }
    }
}

