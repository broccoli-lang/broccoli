using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Broccoli;
using BString = Broccoli.BString;

namespace BroccoliTest {
    [TestClass]
    public class CauliflowerTest {
        private Interpreter _broccoli;
        private Func<string, IValue> _run;

        public static ValueList ValueListFrom() {
            return new ValueList();
        }

        public static ValueList ValueListFrom(params int[] a) {
            return new ValueList(a.Select(i => (IValue) new BInteger(i)));
        }

        [TestInitialize]
        public void Initialize() {
            _broccoli = new Interpreter();
            _run = _broccoli.Run;
            _run("(env cauliflower)");
        }

        [TestMethod]
        public void TestDicts() {
            Console.WriteLine(Program.IsCauliflower);
            Assert.AreEqual(_run("(mkdict)"), new ValueDict{}, "mkdict fails");
            Assert.AreEqual(_run("(setkey (mkdict) 1 2)"), new ValueDict{{(BInteger)1, (BInteger)2}},
             "setkey fails to add key");
            _run("(:= %dict (setkey (mkdict) 1 2))");
            Assert.AreEqual(_run("(setkey %dict 1 3)"), new ValueDict{{(BInteger)1, (BInteger)3}},
             "setkey fails to change key");
            Assert.AreEqual(_run("(rmkey %dict 1)"), new ValueDict{}, "rmkey fails");
            Assert.AreEqual(_run("(haskey %dict 1)"), BAtom.True, "haskey fails");
            Assert.AreNotEqual(_run("(haskey %dict 42)"), BAtom.True, "haskey fails");
            Assert.AreEqual(_run("(getkey %dict 1)"), (BInteger)2);
            Assert.ThrowsException<Exception>(() => _run("(:= $s (mkdict))"), "Can assign dict to scalar var");
                        Assert.ThrowsException<Exception>(() => _run("(:= %d 4)"), "Can assign scalar to dict var");
        }        

        [TestCleanup]
        public void Cleanup() {
            _broccoli = null;
            _run = null;
            Program.IsCauliflower = false;
        }
    }
}

