using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Broccoli;

namespace BroccoliTest {
    // TODO: test print, input, dict hashcode (to test this, use dict as dict key) (remember order doesn't matter)

    [TestClass]
    public class CauliflowerTest {
        private Interpreter _cauliflower;
        private Func<string, IValue> _run;

        public static ValueList ValueListFrom() {
            return new ValueList();
        }

        public static ValueList ValueListFrom(params int[] a) {
            return new ValueList(a.Select(i => (IValue) new BInteger(i)));
        }

        [TestInitialize]
        public void Initialize() {
            _run = (_cauliflower = new CauliflowerInterpreter()).Run;
        }

        [TestMethod]
        public void TestLiterals() {
            Assert.AreEqual(_run("\"\\rfoo\\\"\\t\\n\""), new BString("\rfoo\"\t\n"), "String escaping does not work correctly");
            Assert.AreEqual(_run("\"\rfoo\n\""), new BString("\rfoo\n"), "Multiline strings do not work correctly");
            Assert.AreEqual(_run("'(1 2 (3 4 foo (\"bar\")))"), new ValueList(
                (BInteger) 1,
                (BInteger) 2,
                new ValueList(
                    (BInteger) 3,
                    (BInteger) 4,
                    (BAtom) "foo",
                    new ValueList(
                        (BString) "bar"
                    )
                )
            ), "Lists do not work correctly");
            Assert.AreEqual(_run("`((1 2) ('(baz quux) \"a\"))"), new ValueDictionary {
                { (BInteger) 1, (BInteger) 2},
                { new ValueList((BAtom) "baz", (BAtom) "quux"), (BString) "a"}
            }, "Dictionaries do not work correctly");
        }

        [TestMethod]
        public void TestComments() {
            Assert.AreEqual(_run("#| a #|;this is a comment.\n !@\r|#$%^&*()|#\n\"yey\""), new BString("yey"), "Comment does not work correctly");
        }

        [TestMethod]
        public void TestDicts() {
            Assert.AreEqual(_run("(mkdict)"), new ValueDictionary{}, "mkdict fails");
            Assert.AreEqual(_run("(setkey (mkdict) 1 2)"), new ValueDictionary{{(BInteger)1, (BInteger)2}},
             "setkey fails to add key");
            _run("(:= %dict (setkey (mkdict) 1 2))");
            Assert.AreEqual(_run("(setkey %dict 1 3)"), new ValueDictionary{{(BInteger)1, (BInteger)3}},
             "setkey fails to change key");
            Assert.AreEqual(_run("(rmkey %dict 1)"), new ValueDictionary{}, "rmkey fails");
            Assert.AreEqual(_run("(haskey %dict 1)"), BAtom.True, "haskey fails");
            Assert.AreNotEqual(_run("(haskey %dict 42)"), BAtom.True, "haskey fails");
            Assert.AreEqual(_run("(getkey %dict 1)"), (BInteger)2);
            _run("(:= %dict (setkey %dict \"foo\" \"bar\"))");
            CollectionAssert.AreEquivalent((ValueList)_run("(keys %dict)"), new ValueList((BInteger)1, (BString) "foo"));
            CollectionAssert.AreEquivalent((ValueList)_run("(values %dict)"), new ValueList((BInteger)2, (BString) "bar"));
            Assert.ThrowsException<Exception>(() => _run("(:= $s (mkdict))"), "Can assign dict to scalar var");
            Assert.ThrowsException<Exception>(() => _run("(:= %d 4)"), "Can assign scalar to dict var");
        }        

        [TestCleanup]
        public void Cleanup() {
            _cauliflower = null;
            _run = null;
        }
    }
}

