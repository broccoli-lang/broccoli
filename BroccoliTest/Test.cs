using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Broccoli;
using String = Broccoli.String;

namespace BroccoliTest {
    [TestClass]
    public class Test {
        private Broccoli.Broccoli _broccoli;
        private Func<string, IValue> _run;

        public static ValueList ValueListFrom() {
            return new ValueList();
        }

        public static ValueList ValueListFrom(params int[] a) {
            return new ValueList(a.Select(i => (IValue) new Integer(i)));
        }

        [TestInitialize]
        public void Initialize() {
            _broccoli = new Broccoli.Broccoli();
            _run = _broccoli.Run;
        }

        // TODO: more imformative messages

        [TestMethod]
        public void TestLiterals() {
            Assert.AreEqual(_run("(first (list 42))"), new Integer(42), "Integer does not work correctly");
            Assert.AreEqual(_run("(first (list -42))"), new Integer(-42), "Negative integer does not work correctly");
            Assert.AreEqual(_run("(first (list 4.2))"), new Float(4.2), "Float does not work correctly");
            Assert.AreEqual(_run("(first (list -4.2))"), new Float(-4.2), "Negative float does not work correctly");
            Assert.AreEqual(_run("(first (list \"foo\"))"), new String("foo"), "String does not work correctly");
            Assert.AreEqual(_run("(first (list \"\\f\\o\\o\"))"), new String("foo"), "String escaping does not work correctly");
            Assert.AreEqual(_run("(first (list foo))"), new Atom("foo"), "Atom does not work correctly");
        }

        // Meta-commands
        [TestMethod]
        public void TestClear() {
            Assert.ThrowsException<Exception>(() => _run("(:= $a 2) (clear) $a"), "Clear does not clear scalars");
            Assert.ThrowsException<Exception>(() => _run("(:= @a (list 0 1)) (clear) @a"), "Clear does not clear lists");
            Assert.ThrowsException<Exception>(() => _run("(fn foo () 2) (clear) (foo)"), "Clear does not clear functions");
            Assert.ThrowsException<Exception>(() => _run("(:= $a 2) (reset) $a"), "Reset does not clear scalars");
            Assert.ThrowsException<Exception>(() => _run("(:= @a (list 0 1)) (reset) @a"), "Reset does not clear lists");
            Assert.AreEqual(_run("(fn foo () 2) (reset) (foo)"), new Integer(2), "Reset clears functions");
        }

        [TestMethod]
        public void TestEval() {
            Assert.AreEqual(_run("(eval \"(:= $a 2)\") $a"), new Integer(2), "Eval does not affect scope");
            Assert.AreEqual(_run("(eval \"(fn foo () 2)\") (foo)"), new Integer(2), "Eval does not affect scope");
        }

        // Basic Math

        [TestMethod]
        public void TestPlus() {
            Assert.AreEqual(_run("(+ 2 2)"), new Integer(4), "+ does not work correctly");
            Assert.AreEqual(_run("(+ 2 -3)"), new Integer(-1), "+ does not work correctly with negative");
            Assert.AreEqual(_run("(+ 2 2.2)"), new Float(2 + 2.2), "+ does not work correctly with float");
            Assert.AreEqual(_run("(+ 2 -2.2)"), new Float(2 - 2.2), "+ does not work correctly with negative float");
            Assert.AreEqual(_run("(+ -2 -2)"), new Integer(-4), "+ does not work correctly with both arguments negative");
            Assert.AreEqual(_run("(+ 2.2 2.2)"), new Float(2.2 + 2.2), "+ does not work correctly with both arguments negative");
            Assert.AreEqual(_run("(+ 3 4 5)"), new Integer(12), "+ does not work correctly with multiple arguments");
            Assert.AreEqual(_run("(+ 3)"), new Integer(3), "+ does not work correctly with one argument");
        }

        [TestMethod]
        public void TestTimes() {
            Assert.AreEqual(_run("(* 2 2)"), new Integer(4), "* does not work correctly");
            Assert.AreEqual(_run("(* 2 -3)"), new Integer(-6), "* does not work correctly with negative");
            Assert.AreEqual(_run("(* 2 2.2)"), new Float(2 * 2.2), "* does not work correctly with float");
            Assert.AreEqual(_run("(* 2 -2.2)"), new Float(2 * -2.2), "* does not work correctly with negative float");
            Assert.AreEqual(_run("(* -2 -2)"), new Integer(4), "* does not work correctly with both arguments negative");
            Assert.AreEqual(_run("(* 2.2 2.2)"), new Float(2.2 * 2.2), "* does not work correctly with both arguments negative");
            Assert.AreEqual(_run("(* 3 4 5)"), new Integer(60), "* does not work correctly with multiple arguments");
            Assert.AreEqual(_run("(* 3)"), new Integer(3), "* does not work correctly with one argument");
        }

        [TestMethod]
        public void TestMinus() {
            Assert.AreEqual(_run("(- 2 2)"), new Integer(0), "- does not work correctly");
            Assert.AreEqual(_run("(- 2 -3)"), new Integer(5), "- does not work correctly with negative");
            Assert.AreEqual(_run("(- 2 2.2)"), new Float(2 - 2.2), "- does not work correctly with float");
            Assert.AreEqual(_run("(- 2 -2.2)"), new Float(2 - -2.2), "- does not work correctly with negative float");
            Assert.AreEqual(_run("(- -2 -2)"), new Integer(0), "- does not work correctly with both arguments negative");
            Assert.AreEqual(_run("(- 2.2 2.2)"), new Float(2.2 - 2.2), "- does not work correctly with both arguments negative");
            Assert.AreEqual(_run("(- 1 2 2)"), new Integer(-3), "- does not work correctly with multiple arguments");
            Assert.AreEqual(_run("(- 3)"), new Integer(3), "- does not work correctly with one argument");
        }

        [TestMethod]
        public void TestDivide() {
            Assert.AreEqual(_run("(/ 2 2)"), new Float(1), "/ does not work correctly");
            Assert.AreEqual(_run("(/ 2 -3)"), new Float((double) 2 / -3), "/ does not work correctly with negative");
            Assert.AreEqual(_run("(/ 2 2.2)"), new Float(2 / 2.2), "/ does not work correctly with float");
            Assert.AreEqual(_run("(/ 2 -2.2)"), new Float(2 / -2.2), "/ does not work correctly with negative float");
            Assert.AreEqual(_run("(/ -2 -2)"), new Float(1), "/ does not work correctly with both arguments negative");
            Assert.AreEqual(_run("(/ 2.2 2.2)"), new Float(1), "/ does not work correctly with both arguments negative");
            Assert.AreEqual(_run("(/ 2.2 2.2)"), new Float(1), "/ does not work correctly with multiple arguments");
            Assert.AreEqual(_run("(/ 2.2 2.2 2.2)"), new Float(1 / 2.2), "/ does not work correctly with multiple arguments");
            Assert.AreEqual(_run("(/ 3)"), new Integer(3), "/ does not work correctly with one argument");
        }
        
        [TestMethod]
        public void TestAssign() {
            Assert.AreEqual(_run("(:= $e 2) $e"), new Integer(2), ":= does not work correctly for scalars");
            Assert.AreEqual(_run("(:= $e 2.2) $e"), new Float(2.2), ":= does not work correctly for scalars");
            Assert.AreEqual(_run("(:= $e a) $e"), new Atom("a"), ":= does not work correctly for scalars");
            Assert.AreEqual(_run("(:= @e (list 0 1 2)) @e"), ValueListFrom(0, 1, 2), ":= does not work correctly for lists");
        }

        [TestMethod]
        public void TestCast() {
            Assert.AreEqual(_run("(float 2)"), new Float(2), "Float does not work correctly");
            Assert.AreEqual(_run("(float 2.2)"), new Float(2.2), "Float does not work correctly");
            Assert.AreEqual(_run("(int 2)"), new Integer(2), "Int does not work correctly");
            Assert.AreEqual(_run("(int 2.2)"), new Integer(2), "Int does not work correctly");
        }

        // Comparison

        [TestMethod]
        public void TestEqual() {
            Assert.AreEqual(_run("(= 1 1 1)"), Atom.True, "= does not work correctly");
            Assert.AreEqual(_run("(= 1.1 1.1 1.1)"), Atom.True, "= does not work correctly");
            Assert.AreEqual(_run("(= 1 1 0)"), Atom.Nil, "= does not work correctly");
            Assert.AreEqual(_run("(= a b a)"), Atom.Nil, "= does not work correctly");
            Assert.AreEqual(_run("(= \"a\" \"b\" \"b\")"), Atom.Nil, "= does not work correctly");
            Assert.AreEqual(_run("(= 1)"), Atom.True, "= does not work correctly with one argument");
            Assert.ThrowsException<Exception>(() => _run("(=)"), "= does not fail with no arguments");
        }

        [TestMethod]
        public void TestNotEqual() {
            Assert.AreEqual(_run("(/= 1 1 1)"), Atom.Nil, "/= does not work correctly");
            Assert.AreEqual(_run("(/= 1.1 1.1 1.1)"), Atom.Nil, "/= does not work correctly");
            Assert.AreEqual(_run("(/= 1 1 0)"), Atom.Nil, "/= does not work correctly");
            Assert.AreEqual(_run("(/= 1 0 0)"), Atom.True, "/= does not work correctly");
            Assert.AreEqual(_run("(/= a b a)"), Atom.Nil, "/= does not work correctly");
            Assert.AreEqual(_run("(/= a b b)"), Atom.True, "/= does not work correctly");
            Assert.AreEqual(_run("(/= \"a\" \"b\" \"b\")"), Atom.True, "/= does not work correctly");
            Assert.AreEqual(_run("(/= 1)"), Atom.Nil, "= does not work correctly with one argument");
            Assert.ThrowsException<Exception>(() => _run("(/=)"), "/= does not fail with no arguments");
        }

        [TestMethod]
        public void TestComparison() {
            Assert.AreEqual(_run("(< 0 1 2)"), Atom.True, "< does not work correctly");
            Assert.AreEqual(_run("(< 0 1 1)"), Atom.Nil, "< does not work correctly");
            Assert.AreEqual(_run("(< 1.1 2 3.1)"), Atom.True, "< does not work correctly");
            Assert.AreEqual(_run("(< 1 2.1 2.1)"), Atom.Nil, "< does not work correctly");
            Assert.ThrowsException<Exception>(() => _run("(< a b)"), "< does not fail with non-numeric argument");
            Assert.ThrowsException<Exception>(() => _run("(< 1)"), "< does not fail with one argument");
            Assert.AreEqual(_run("(> 2 1 0)"), Atom.True, "> does not work correctly");
            Assert.AreEqual(_run("(> 1 0 0)"), Atom.Nil, "> does not work correctly");
            Assert.AreEqual(_run("(> 3.1 2 1.1)"), Atom.True, "> does not work correctly");
            Assert.AreEqual(_run("(> 2.1 2.1 1)"), Atom.Nil, "> does not work correctly");
            Assert.ThrowsException<Exception>(() => _run("(> a b)"), "> does not fail with non-numeric argument");
            Assert.ThrowsException<Exception>(() => _run("(> 1)"), "> does not fail with one argument");
            Assert.AreEqual(_run("(<= 0 1 1)"), Atom.True, "<= does not work correctly");
            Assert.AreEqual(_run("(<= 0 0 -1)"), Atom.Nil, "<= does not work correctly");
            Assert.AreEqual(_run("(<= 1.1 1.1 3.1)"), Atom.True, "<= does not work correctly");
            Assert.AreEqual(_run("(<= 1 2.1 2)"), Atom.Nil, "<= does not work correctly");
            Assert.ThrowsException<Exception>(() => _run("(<= a b)"), "<= does not fail with non-numeric argument");
            Assert.ThrowsException<Exception>(() => _run("(<= 1)"), "<= does not fail with one argument");
            Assert.AreEqual(_run("(>= 2 1 0)"), Atom.True, ">= does not work correctly");
            Assert.AreEqual(_run("(>= 0 0 1)"), Atom.Nil, ">= does not work correctly");
            Assert.AreEqual(_run("(>= 3.1 1.1 1.1)"), Atom.True, ">= does not work correctly");
            Assert.AreEqual(_run("(>= 2 2.1 1)"), Atom.Nil, ">= does not work correctly");
            Assert.ThrowsException<Exception>(() => _run("(>= a b)"), ">= does not fail with non-numeric argument");
            Assert.ThrowsException<Exception>(() => _run("(>= 1)"), ">= does not fail with one argument");
        }

        // Logic

        [TestMethod]
        public void TestNot() {
            Assert.AreEqual(_run("(not t)"), Atom.Nil, "Not does not work correctly");
            Assert.AreEqual(_run("(not nil)"), Atom.True, "Not does not work correctly");
            Assert.AreEqual(_run("(not 0)"), Atom.Nil, "Not does not work correctly");
            Assert.AreEqual(_run("(not a)"), Atom.Nil, "Not does not work correctly");
            Assert.ThrowsException<Exception>(() => _run("(not t t)"), "Not does not fail with two arguments");
            Assert.ThrowsException<Exception>(() => _run("(not)"), "Not does not fail with no arguments");
        }

        [TestMethod]
        public void TestAnd() {
            Assert.AreEqual(_run("(and 1 2 3)"), Atom.True, "And does not work correctly");
            Assert.AreEqual(_run("(and nil)"), Atom.Nil, "And does not work correctly");
            Assert.AreEqual(_run("(and nil 1 2)"), Atom.Nil, "And does not work correctly");
            Assert.AreEqual(_run("(and)"), Atom.True, "And does not work correctly");
        }

        [TestMethod]
        public void TestOr() {
            Assert.AreEqual(_run("(or 1 2 3)"), Atom.True, "Or does not work correctly");
            Assert.AreEqual(_run("(or nil)"), Atom.Nil, "Or does not work correctly");
            Assert.AreEqual(_run("(or nil 0)"), Atom.True, "Or does not work correctly");
            Assert.AreEqual(_run("(or 0 1 2)"), Atom.True, "Or does not work correctly");
            Assert.AreEqual(_run("(or)"), Atom.Nil, "Or does not work correctly");
        }

        // Control flow

        [TestMethod]
        public void TestIf() {
            Assert.AreEqual(_run("(if t 2)"), new Integer(2), "If does not work correctly");
            Assert.AreEqual(_run("(if t 2 else 0)"), new Integer(2), "If else does not work correctly");
            Assert.AreEqual(_run("(if nil 2 else 0)"), new Integer(0), "If else does not work correctly");
        }

        // List Functions

        [TestMethod]
        public void TestList() {
            Assert.AreEqual(_run("(list 0 1 2)"), ValueListFrom(0, 1, 2), "List does not work correctly");
            Assert.AreEqual(_run("(list)"), ValueListFrom(), "Zero-length list does not work correctly");
        }

        [TestMethod]
        public void TestLen() {
            Assert.AreEqual(_run("(len \"foo\")"), new Integer(3), "String len does not work correctly");
            Assert.AreEqual(_run("(len \"\")"), new Integer(0), "String len does not work correctly");
            Assert.AreEqual(_run("(len \"\\\\\")"), new Integer(1), "String len does not work correctly");
            Assert.AreEqual(_run("(len (list 0 1 2))"), new Integer(3), "List len does not work correctly");
            Assert.AreEqual(_run("(len (list))"), new Integer(0), "Zero-length list len does not work correctly");
            Assert.ThrowsException<Exception>(() => _run("(len 1)"), "Len does not fail with non-list");
            Assert.ThrowsException<Exception>(() => _run("(len (list) (list))"), "Len does not fail with two arguments");
            Assert.ThrowsException<Exception>(() => _run("(len)"), "Len does not fail with no arguments");
        }

        [TestMethod]
        public void TestFirst() {
            Assert.AreEqual(_run("(first (list 0 1 2))"), new Integer(0), "First does not work correctly");
            Assert.AreEqual(_run("(first (list))"), Atom.Nil, "First does not work correctly");
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
            Assert.ThrowsException<Exception>(() => _run("(slice (list) 1.1 2.1)"), "Range does not fail with non-integers");
        }

        [TestMethod]
        public void TestRange() {
            Assert.AreEqual(_run("(range 0 5)"), ValueListFrom(0, 1, 2, 3, 4, 5), "Range does not work correctly");
            Assert.AreEqual(_run("(range -5 5)"), ValueListFrom(-5, -4, -3, -2, -1, 0, 1, 2, 3, 4, 5), "Negative range does not work correctly");
            Assert.AreEqual(_run("(range -5 -1)"), ValueListFrom(-5, -4, -3, -2, -1), "Negative to negative range does not work correctly");
            Assert.ThrowsException<Exception>(() => _run("(range 1)"), "Range does not fail with one argument");
            Assert.ThrowsException<Exception>(() => _run("(range 1.1)"), "Range does not fail with non-integers");
        }

        [TestMethod]
        public void TestCat() {
            Assert.AreEqual(_run("(cat (list 0 1 2) (list 3 4 5))"), ValueListFrom(0, 1, 2, 3, 4, 5), "Cat does not work correctly");
            Assert.AreEqual(_run("(cat (list 0 1) (list 2 3) (list 4 5))"), ValueListFrom(0, 1, 2, 3, 4, 5), "Cat does not work correctly with multiple arguments");
            Assert.ThrowsException<Exception>(() => _run("(cat (list 0 1 2))"), "Cat does not fail with one argument");
            Assert.ThrowsException<Exception>(() => _run("(cat 0 1 2)"), "Cat does not fail with non-lists");
        }

        [TestCleanup]
        public void Cleanup() {
            _broccoli = null;
            _run = null;
        }
    }
}

