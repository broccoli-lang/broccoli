using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Broccoli;

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

        [TestMethod]
        public void TestLiterals() {
            Assert.AreEqual(((ValueList) _run("(list 42)"))[0], new Integer(42), "Integer does not work correctly");
            Assert.AreEqual(((ValueList) _run("(list -42)"))[0], new Integer(-42), "Negative integer does not work correctly");
            Assert.AreEqual(((ValueList) _run("(list 4.2)"))[0], new Float(4.2), "Float does not work correctly");
            Assert.AreEqual(((ValueList) _run("(list -4.2)"))[0], new Float(-4.2), "Negative float does not work correctly");
            Assert.AreEqual(((ValueList) _run("(list \"foo\")"))[0], new Broccoli.String("foo"), "String does not work correctly");
            Assert.AreEqual(((ValueList) _run("(list \"\\f\\o\\o\")"))[0], new Broccoli.String("foo"), "String escaping does not work correctly");
            Assert.AreEqual(((ValueList) _run("(list foo)"))[0], new Atom("foo"), "Atom does not work correctly");
        }

        [TestMethod]
        public void TestList() {
            Assert.AreEqual(_run("(list 0 1 2)"), ValueListFrom(0, 1, 2), "List does not work correctly");
            Assert.AreEqual(_run("(list)"), ValueListFrom(), "Zero-length list does not work correctly");
        }

        [TestMethod]
        public void TestFirst() {
            Assert.AreEqual(_run("(first (list 0 1 2))"), ValueListFrom(0), "First does not work correctly");
        }

        [TestMethod]
        public void TestRest() {
            Assert.AreEqual(_run("(rest (list 0 1 2))"), ValueListFrom(1, 2), "Rest does not work correctly");
            Assert.AreEqual(_run("(rest (list 0))"), ValueListFrom(), "Rest does not work correctly with 1-element list");
            Assert.AreEqual(_run("(rest (list))"), ValueListFrom(), "Rest does not work correctly with 1-element list");
        }

        [TestMethod]
        public void TestLen() {
            Assert.AreEqual(_run("(len (list 0 1 2))"), new Integer(3), "List len does not work correctly");
            Assert.AreEqual(_run("(len (list))"), new Integer(0), "Zero-length list len does not work correctly");
            Assert.ThrowsException<Exception>(() => _run("(len 1)"), "Len does not fail with non-list");
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

