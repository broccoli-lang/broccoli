using System.Collections.Generic;

namespace Broccoli {
    public interface IValue : IValueExpressible { }

    public interface IValue<out T> : IValue {
        T Value { get; }
    }

    public struct Integer : IValue<int> {
        public int Value { get; }

        public Integer(int i) {
            Value = i;
        }

        public override string ToString() {
            return $"Integer[{Value}]";
        }
    }

    public struct Float : IValue<double> {
        public double Value { get; }

        public Float(double f) {
            Value = f;
        }

        public override string ToString() {
            return $"Float[{Value}]";
        }
    }

    public struct String : IValue<string> {
        public string Value { get; }

        public String(string s) {
            Value = s;
        }

        public override string ToString() {
            return $"String[\"{Value}\"]";
        }
    }

    public struct Atom : IValue<string> {
        public string Value { get; }

        public Atom(string a) {
            Value = a;
        }

        public override string ToString() {
            return $"Atom[{Value}]";
        }
    }

    public struct ScalarVar : IValue<string> {
        public string Value { get; }

        public ScalarVar(string s) {
            Value = s;
        }

        public override string ToString() {
            return $"ScalarVar[{Value}]";
        }
    }

    public struct ListVar : IValue<string> {
        public string Value { get; }

        public ListVar(string l) {
            Value = l;
        }

        public override string ToString() {
            return $"ListVar[{Value}]";
        }
    }

    public struct ValueList : IValue<List<IValue>> {
        public List<IValue> Value { get; }

        public ValueList(List<IValue> values) {
            Value = values;
        }

        public override string ToString() {
            return $"ValueList[{string.Join(", ", Value)}]";
        }
    }
}