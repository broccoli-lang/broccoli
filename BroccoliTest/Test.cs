using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Broccoli;
using BString = Broccoli.BString;
using System.Text;

namespace BroccoliTest {
    [TestClass]
    public class Test {
        private Interpreter _broccoli;
        private Func<string, IValue> _run;
        private System.IO.StringReader _input;
        private Writer _output;

        private class Writer : System.IO.TextWriter {
            public override Encoding Encoding => Encoding.UTF8;

            private StringBuilder _lastOutput = new StringBuilder();

            public void Clear() => _lastOutput.Clear();

            public override void Write(char c) => _lastOutput.Append(c);

            public override string ToString() => _lastOutput.ToString();
        }

        private static BList ValueListFrom() => new BList();

        private static BList ValueListFrom(params int[] a) => new BList(a.Select(i => (IValue)new BInteger(i)));

        private void WriteInput(string input) {
            _input = new System.IO.StringReader(input);
            Console.SetIn(_input);
        }

        // ReSharper disable once UnusedParameter.Local
        private string ReadOutput(params object[] ignored) {
            var result = _output.ToString();
            _output.Clear();
            return result;
        }

        [TestInitialize]
        public void Initialize() {
            _run = (_broccoli = new Interpreter()).Run;
            Console.SetOut(_output = new Writer());
        }

        [TestMethod]
        public void TestIO() {
            Assert.AreEqual(ReadOutput(_run("(print foo)")), "foo", "Print does not work correctly for atoms");
            Assert.AreEqual(ReadOutput(_run("(print \"foo\")")), "foo", "Print does not work correctly for strings");
            Assert.AreEqual(ReadOutput(_run("(print 3.3)")), "3.3", "Print does not work correctly for numbers");
            Assert.AreEqual(ReadOutput(_run("(print (list foo bar))")), "(foo bar)", "Print does not work correctly for lists");
        }

        [TestMethod]
        public void TestLiterals() {
            Assert.AreEqual(_run("42"), new BInteger(42), "Integer does not work correctly");
            Assert.AreEqual(_run("-42"), new BInteger(-42), "Negative integer does not work correctly");
            Assert.AreEqual(_run("4.2"), new BFloat(4.2), "Float does not work correctly");
            Assert.AreEqual(_run("-4.2"), new BFloat(-4.2), "Negative float does not work correctly");
            Assert.AreEqual(_run("\"foo\""), new BString("foo"), "String does not work correctly");
            Assert.AreEqual(_run("\"\\f\\o\\o\""), new BString("foo"), "String escaping does not work correctly");
            Assert.AreEqual(_run("\"\r\\f\t\\o\\o\n\""), new BString("\rf\too\n"), "String escaping does not work correctly");
            Assert.AreEqual(_run("foo"), new BAtom("foo"), "Atom does not work correctly");
        }

        [TestMethod]
        public void TestComments() => Assert.AreEqual(_run(";this is a comment. !@#$%^&*()\n\"yey\""), new BString("yey"), "Comment does not work correctly");

        // Meta-commands
        [TestMethod]
        public void TestClear() {
            Assert.ThrowsException<Exception>(() => _run("(:= $a 2) (clear) $a"), "Clear does not clear scalars");
            Assert.ThrowsException<Exception>(() => _run("(:= @a (list 0 1)) (clear) @a"), "Clear does not clear lists");
            Assert.ThrowsException<Exception>(() => _run("(fn foo () 2) (clear) (foo)"), "Clear does not clear functions");
            Assert.ThrowsException<Exception>(() => _run("(:= $a 2) (reset) $a"), "Reset does not clear scalars");
            Assert.ThrowsException<Exception>(() => _run("(:= @a (list 0 1)) (reset) @a"), "Reset does not clear lists");
            Assert.AreEqual(_run("(fn foo () 2) (reset) (foo)"), new BInteger(2), "Reset clears functions");
        }

        [TestMethod]
        public void TestEval() {
            Assert.AreEqual(_run("(eval \"(:= $a 2)\") $a"), new BInteger(2), "Eval does not affect scope");
            Assert.AreEqual(_run("(eval \"(fn foo () 2)\") (foo)"), new BInteger(2), "Eval does not affect scope");
        }

        // Basic Math

        [TestMethod]
        public void TestPlus() {
            Assert.AreEqual(_run("(+ 2 2)"), new BInteger(4), "+ does not work correctly");
            Assert.AreEqual(_run("(+ 2 -3)"), new BInteger(-1), "+ does not work correctly with negative");
            Assert.AreEqual(_run("(+ 2 2.2)"), new BFloat(2 + 2.2), "+ does not work correctly with float");
            Assert.AreEqual(_run("(+ 2 -2.2)"), new BFloat(2 - 2.2), "+ does not work correctly with negative float");
            Assert.AreEqual(_run("(+ -2 -2)"), new BInteger(-4), "+ does not work correctly with both arguments negative");
            Assert.AreEqual(_run("(+ 2.2 2.2)"), new BFloat(2.2 + 2.2), "+ does not work correctly with both arguments negative");
            Assert.AreEqual(_run("(+ 3 4 5)"), new BInteger(12), "+ does not work correctly with multiple arguments");
            Assert.AreEqual(_run("(+ 3)"), new BInteger(3), "+ does not work correctly with one argument");
        }

        [TestMethod]
        public void TestTimes() {
            Assert.AreEqual(_run("(* 2 2)"), new BInteger(4), "* does not work correctly");
            Assert.AreEqual(_run("(* 2 -3)"), new BInteger(-6), "* does not work correctly with negative");
            Assert.AreEqual(_run("(* 2 2.2)"), new BFloat(2 * 2.2), "* does not work correctly with float");
            Assert.AreEqual(_run("(* 2 -2.2)"), new BFloat(2 * -2.2), "* does not work correctly with negative float");
            Assert.AreEqual(_run("(* -2 -2)"), new BInteger(4), "* does not work correctly with both arguments negative");
            Assert.AreEqual(_run("(* 2.2 2.2)"), new BFloat(2.2 * 2.2), "* does not work correctly with both arguments negative");
            Assert.AreEqual(_run("(* 3 4 5)"), new BInteger(60), "* does not work correctly with multiple arguments");
            Assert.AreEqual(_run("(* 3)"), new BInteger(3), "* does not work correctly with one argument");
        }

        [TestMethod]
        public void TestMinus() {
            Assert.AreEqual(_run("(- 2 2)"), new BInteger(0), "- does not work correctly");
            Assert.AreEqual(_run("(- 2 -3)"), new BInteger(5), "- does not work correctly with negative");
            Assert.AreEqual(_run("(- 2 2.2)"), new BFloat(2 - 2.2), "- does not work correctly with float");
            Assert.AreEqual(_run("(- 2 -2.2)"), new BFloat(2 - -2.2), "- does not work correctly with negative float");
            Assert.AreEqual(_run("(- -2 -2)"), new BInteger(0), "- does not work correctly with both arguments negative");
            Assert.AreEqual(_run("(- 2.2 2.2)"), new BFloat(2.2 - 2.2), "- does not work correctly with both arguments negative");
            Assert.AreEqual(_run("(- 1 2 2)"), new BInteger(-3), "- does not work correctly with multiple arguments");
            Assert.AreEqual(_run("(- 3)"), new BInteger(3), "- does not work correctly with one argument");
        }

        [TestMethod]
        public void TestDivide() {
            Assert.AreEqual(_run("(/ 2 2)"), new BFloat(1), "/ does not work correctly");
            Assert.AreEqual(_run("(/ 2 -3)"), new BFloat((double) 2 / -3), "/ does not work correctly with negative");
            Assert.AreEqual(_run("(/ 2 2.2)"), new BFloat(2 / 2.2), "/ does not work correctly with float");
            Assert.AreEqual(_run("(/ 2 -2.2)"), new BFloat(2 / -2.2), "/ does not work correctly with negative float");
            Assert.AreEqual(_run("(/ -2 -2)"), new BFloat(1), "/ does not work correctly with both arguments negative");
            Assert.AreEqual(_run("(/ 2.2 2.2)"), new BFloat(1), "/ does not work correctly with both arguments negative");
            Assert.AreEqual(_run("(/ 2.2 2.2)"), new BFloat(1), "/ does not work correctly with multiple arguments");
            Assert.AreEqual(_run("(/ 2.2 2.2 2.2)"), new BFloat(1 / 2.2), "/ does not work correctly with multiple arguments");
            Assert.AreEqual(_run("(/ 3)"), new BInteger(3), "/ does not work correctly with one argument");
        }

        [TestMethod]
        public void TestAssign() {
            Assert.AreEqual(_run("(:= $e 2) $e"), new BInteger(2), ":= does not work correctly for scalars");
            Assert.AreEqual(_run("(:= $e 2.2) $e"), new BFloat(2.2), ":= does not work correctly for scalars");
            Assert.AreEqual(_run("(:= $e a) $e"), new BAtom("a"), ":= does not work correctly for scalars");
            Assert.AreEqual(_run("(:= @e (list 0 1 2)) @e"), ValueListFrom(0, 1, 2), ":= does not work correctly for lists");
        }

        [TestMethod]
        public void TestCast() {
            Assert.AreEqual(_run("(float 2)"), new BFloat(2), "Float does not work correctly with integer value");
            Assert.AreEqual(_run("(float 2.2)"), new BFloat(2.2), "Float does not work correctly with float value");
            Assert.AreEqual(_run("(int 2)"), new BInteger(2), "Int does not work correctly with integer value");
            Assert.AreEqual(_run("(int 2.2)"), new BInteger(2), "Int does not work correctly with float value");
        }

        // Comparison

        [TestMethod]
        public void TestEqual() {
            Assert.AreEqual(_run("(= 1 1 1)"), BAtom.True, "= does not succeed correctly with integers");
            Assert.AreEqual(_run("(= 1.1 1.1 1.1)"), BAtom.True, "= does not succeed correctly with floats");
            Assert.AreEqual(_run("(= aa aa aa)"), BAtom.True, "= does not succeed correctly with atoms");
            Assert.AreEqual(_run("(= \"aa\" \"aa\" \"aa\")"), BAtom.True, "= does not succeed correctly with strings");
            Assert.AreEqual(_run("(= (list 1 2) (list 1 2) (list 1 2))"), BAtom.True, "= does not succeed correctly with lists");
            Assert.AreEqual(_run("(= 1.1 1.1 0.1)"), BAtom.Nil, "= does not fail correctly with floats");
            Assert.AreEqual(_run("(= 1 1 0)"), BAtom.Nil, "= does not fail correctly with integers");
            Assert.AreEqual(_run("(= aa ab aa)"), BAtom.Nil, "= does not fail correctly with atoms");
            Assert.AreEqual(_run("(= \"aa\" \"ab\" \"ab\")"), BAtom.Nil, "= does not fail correctly with strings");
            Assert.AreEqual(_run("(= (list 1 1) (list 1 2) (list 1 2))"), BAtom.Nil, "= does not fail correctly with lists");
            Assert.AreEqual(_run("(= 1)"), BAtom.True, "= does not work correctly with one argument");
            Assert.ThrowsException<Exception>(() => _run("(=)"), "= does not fail with no arguments");
        }

        [TestMethod]
        public void TestNotEqual() {
            Assert.AreEqual(_run("(/= 1 1 1)"), BAtom.Nil, "/= does not fail correctly with integers");
            Assert.AreEqual(_run("(/= 1.1 1.1 1.1)"), BAtom.Nil, "/= does fail work correctly with floats");
            Assert.AreEqual(_run("(/= 1 1 0)"), BAtom.Nil, "/= does not fail correctly when some integers match");
            Assert.AreEqual(_run("(/= 1 0 0)"), BAtom.True, "/= does not succeed correctly with integers");
            Assert.AreEqual(_run("(/= a b a)"), BAtom.Nil, "/= does not fail correctly with atoms");
            Assert.AreEqual(_run("(/= a b b)"), BAtom.True, "/= does not succeed correctly with atoms");
            Assert.AreEqual(_run("(/= \"a\" \"b\" \"b\")"), BAtom.True, "/= does not succeed correctly with strings");
            Assert.AreEqual(_run("(/= 1)"), BAtom.Nil, "= does not work correctly with one argument");
            Assert.ThrowsException<Exception>(() => _run("(/=)"), "/= does not fail with no arguments");
        }

        [TestMethod]
        public void TestComparison() {
            Assert.AreEqual(_run("(< 0 1 2)"), BAtom.True, "< does not succeed correctly with integers");
            Assert.AreEqual(_run("(< 0 1 1)"), BAtom.Nil, "< does not fail correctly with integers");
            Assert.AreEqual(_run("(< 1.1 2 3.1)"), BAtom.True, "< does not succeed correctly with numbers");
            Assert.AreEqual(_run("(< 1 2.1 2.1)"), BAtom.Nil, "< does not fail correctly with numbers");
            Assert.ThrowsException<ArgumentTypeException>(() => _run("(< a b)"), "< does not fail with non-numeric argument");
            Assert.ThrowsException<Exception>(() => _run("(< 1)"), "< does not fail with one argument");
            Assert.AreEqual(_run("(> 2 1 0)"), BAtom.True, "> does not succeed correctly with integers");
            Assert.AreEqual(_run("(> 1 0 0)"), BAtom.Nil, "> does not fail correctly with integers");
            Assert.AreEqual(_run("(> 3.1 2 1.1)"), BAtom.True, "> does not succeed correctly with numbers");
            Assert.AreEqual(_run("(> 2.1 2.1 1)"), BAtom.Nil, "> does not fail correctly with numbers");
            Assert.ThrowsException<ArgumentTypeException>(() => _run("(> a b)"), "> does not fail with non-numeric argument");
            Assert.ThrowsException<Exception>(() => _run("(> 1)"), "> does not fail with one argument");
            Assert.AreEqual(_run("(<= 0 1 1)"), BAtom.True, "<= does not succeed correctly with integers");
            Assert.AreEqual(_run("(<= 0 0 -1)"), BAtom.Nil, "<= does not fail correctly with integers");
            Assert.AreEqual(_run("(<= 1.1 1.1 3)"), BAtom.True, "<= does not succeed correctly with numbers");
            Assert.AreEqual(_run("(<= 1 2.1 2)"), BAtom.Nil, "<= does not fail correctly with numbers");
            Assert.ThrowsException<ArgumentTypeException>(() => _run("(<= a b)"), "<= does not fail with non-numeric argument");
            Assert.ThrowsException<Exception>(() => _run("(<= 1)"), "<= does not fail with one argument");
            Assert.AreEqual(_run("(>= 2 1 0)"), BAtom.True, ">= does not succeed correctly with integers");
            Assert.AreEqual(_run("(>= 0 0 1)"), BAtom.Nil, ">= does not fail correctly with integers");
            Assert.AreEqual(_run("(>= 3 1.1 1.1)"), BAtom.True, ">= does not succeed correctly with numbers");
            Assert.AreEqual(_run("(>= 2 2.1 1)"), BAtom.Nil, ">= does not fail correctly with numbers");
            Assert.ThrowsException<ArgumentTypeException>(() => _run("(>= a b)"), ">= does not fail with non-numeric argument");
            Assert.ThrowsException<Exception>(() => _run("(>= 1)"), ">= does not fail with one argument");
        }

        // Logic

        [TestMethod]
        public void TestNot() {
            Assert.AreEqual(_run("(not t)"), BAtom.Nil, "Not does not work correctly with true");
            Assert.AreEqual(_run("(not nil)"), BAtom.True, "Not does not work correctly with nil");
            Assert.AreEqual(_run("(not 0)"), BAtom.Nil, "Not does not work correctly with non-boolean");
            Assert.AreEqual(_run("(not a)"), BAtom.Nil, "Not does not work correctly with non-boolean");
            Assert.ThrowsException<Exception>(() => _run("(not t t)"), "Not does not fail with two arguments");
            Assert.ThrowsException<Exception>(() => _run("(not)"), "Not does not fail with no arguments");
        }

        [TestMethod]
        public void TestAnd() {
            Assert.AreEqual(_run("(and 1 2 3)"), BAtom.True, "And does not work correctly with integers");
            Assert.AreEqual(_run("(and nil)"), BAtom.Nil, "And does not work correctly with nil");
            Assert.AreEqual(_run("(and nil 1 2)"), BAtom.Nil, "And does not work correctly");
            Assert.AreEqual(_run("(and)"), BAtom.True, "And does not work correctly with no arguments");
        }

        [TestMethod]
        public void TestOr() {
            Assert.AreEqual(_run("(or 1 2 3)"), BAtom.True, "Or does not work correctly with integers");
            Assert.AreEqual(_run("(or nil)"), BAtom.Nil, "Or does not work correctly with nil");
            Assert.AreEqual(_run("(or nil 0)"), BAtom.True, "Or does not work correctly with zero");
            Assert.AreEqual(_run("(or 0 1 2)"), BAtom.True, "Or does not work correctly");
            Assert.AreEqual(_run("(or)"), BAtom.Nil, "Or does not work correctly with no arguments");
        }

        // Control flow

        [TestMethod]
        public void TestIf() {
            Assert.AreEqual(_run("(if t 2)"), new BInteger(2), "If does not work correctly");
            Assert.AreEqual(_run("(if t 2 else 0)"), new BInteger(2), "If else does not work correctly with true");
            Assert.AreEqual(_run("(if nil 2 else 0)"), new BInteger(0), "If else does not work correctly with zero");
        }

        [TestMethod]
        public void TestFor() => Assert.AreEqual(_run("(:= $a 0) (for $i in (list 1 10 100) (:= $a (+ $a $i))) $a"), new BInteger(111), "For loop does not work correctly");

        // List Functions

        [TestMethod]
        public void TestList() {
            Assert.AreEqual(_run("(list 0 1 2)"), ValueListFrom(0, 1, 2), "List does not work correctly");
            Assert.AreEqual(_run("(list)"), ValueListFrom(), "Zero-length list does not work correctly");
        }

        [TestMethod]
        public void TestLen() {
            Assert.AreEqual(_run("(len \"foo\")"), new BInteger(3), "String len does not work correctly");
            Assert.AreEqual(_run("(len \"\")"), new BInteger(0), "String len does not work correctly");
            Assert.AreEqual(_run("(len \"\\\\\")"), new BInteger(1), "String len does not work correctly");
            Assert.AreEqual(_run("(len (list 0 1 2))"), new BInteger(3), "List len does not work correctly");
            Assert.AreEqual(_run("(len (list))"), new BInteger(0), "Zero-length list len does not work correctly");
            Assert.ThrowsException<ArgumentTypeException>(() => _run("(len 1)"), "Len does not fail with non-list");
            Assert.ThrowsException<Exception>(() => _run("(len (list) (list))"), "Len does not fail with two arguments");
            Assert.ThrowsException<Exception>(() => _run("(len)"), "Len does not fail with no arguments");
        }

        [TestMethod]
        public void TestFirst() {
            Assert.AreEqual(_run("(first (list 0 1 2))"), new BInteger(0), "First does not work correctly");
            Assert.AreEqual(_run("(first (list))"), BAtom.Nil, "First does not work correctly");
        }

        [TestMethod]
        public void TestRest() {
            Assert.AreEqual(_run("(rest (list 0 1 2))"), ValueListFrom(1, 2), "Rest does not work correctly");
            Assert.AreEqual(_run("(rest (list 0))"), ValueListFrom(), "Rest does not work correctly with 1-element list");
            Assert.AreEqual(_run("(rest (list))"), ValueListFrom(), "Rest does not work correctly with 1-element list");
        }

        [TestMethod]
        public void TestSlice() {
            Assert.AreEqual(_run("(slice (list 0 1 2 3 4 5) 0 3)"), ValueListFrom(0, 1, 2), "Slice does not work correctly");
            Assert.AreEqual(_run("(slice (list 0 1 2 3 4 5) -5 3)"), ValueListFrom(0, 1, 2), "Slice does not work correctly with negative start");
            Assert.AreEqual(_run("(slice (list 0 1 2 3 4 5) -5 -3)"), ValueListFrom(), "Slice does not work correctly with negative end");
            Assert.AreEqual(_run("(slice (list 0 1 2 3 4 5) 3 -5)"), ValueListFrom(), "Slice does not work correctly with end less than start");
            Assert.ThrowsException<Exception>(() => _run("(slice (list) 1)"), "Range does not fail with two arguments");
            Assert.ThrowsException<ArgumentTypeException>(() => _run("(slice (list) 1.1 2.1)"), "Range does not fail with non-integers");
        }

        [TestMethod]
        public void TestRange() {
            Assert.AreEqual(_run("(range 0 5)"), ValueListFrom(0, 1, 2, 3, 4, 5), "Range does not work correctly");
            Assert.AreEqual(_run("(range -5 5)"), ValueListFrom(-5, -4, -3, -2, -1, 0, 1, 2, 3, 4, 5), "Negative range does not work correctly");
            Assert.AreEqual(_run("(range -5 -1)"), ValueListFrom(-5, -4, -3, -2, -1), "Negative to negative range does not work correctly");
            Assert.ThrowsException<Exception>(() => _run("(range 1)"), "Range does not fail with one argument");
            Assert.ThrowsException<ArgumentTypeException>(() => _run("(range 1.1 2.1)"), "Range does not fail with non-integers");
        }

        [TestMethod]
        public void TestCat() {
            Assert.AreEqual(_run("(cat (list 0 1 2) (list 3 4 5))"), ValueListFrom(0, 1, 2, 3, 4, 5), "Cat does not work correctly");
            Assert.AreEqual(_run("(cat (list 0 1) (list 2 3) (list 4 5))"), ValueListFrom(0, 1, 2, 3, 4, 5), "Cat does not work correctly with multiple arguments");
            Assert.ThrowsException<Exception>(() => _run("(cat (list 0 1 2))"), "Cat does not fail with one argument");
            Assert.ThrowsException<ArgumentTypeException>(() => _run("(cat 0 1 2)"), "Cat does not fail with non-lists");
        }

        [TestCleanup]
        public void Cleanup() {
            _broccoli = null;
            _run = null;
        }
    }
}
