using System.Collections.Generic;

namespace Broccoli {
    public interface IValue : IValueExpressible { }

    public struct Integer : IValue {
        public int Value { get; }

        public Integer(int i) {
            Value = i;
        }

        public override string ToString() => Value.ToString();
    }

    public struct Float : IValue {
        public double Value { get; }

        public Float(double f) {
            Value = f;
        }

        public override string ToString() => Value.ToString();
    }

    public struct String : IValue {
        public string Value { get; }

        public String(string s) {
            Value = s;
        }

        public override string ToString() => Value;
    }

    public struct Atom : IValue {
        public static readonly Atom True = new Atom("t");
        public static readonly Atom Nil = new Atom("nil");

        public string Value { get; }

        public Atom(string a) {
            Value = a;
        }

        public override string ToString() => Value;
    }

    public struct ScalarVar : IValue {
        public string Value { get; }

        public ScalarVar(string s) {
            Value = s;
        }

        public override string ToString() => Value;
    }

    public struct ListVar : IValue {
        public string Value { get; }

        public ListVar(string l) {
            Value = l;
        }

        public override string ToString() => Value;
    }

    public struct ValueList : IValue {
        public List<IValue> Value { get; }

        public ValueList(List<IValue> values) {
            Value = values;
        }

        public override string ToString() => '(' + string.Join(" ", Value) + ')';
    }
}