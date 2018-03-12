using System.Collections.Generic;

namespace Broccoli {
    public interface IValue { }

    public interface IValue<out T> : IValue {
        T Value { get; }
    }

    public struct Int : IValue<int> {
        public int Value { get; }

        public Int(int i) {
            Value = i;
        }
    }

    public struct Float : IValue<double> {
        public double Value { get; }

        public Float(double f) {
            Value = f;
        }
    }

    public struct String : IValue<string> {
        public string Value { get; }

        public String(string s) {
            Value = s;
        }
    }

    public struct Atom : IValue<string> {
        public string Value { get; }

        public Atom(string a) {
            Value = a;
        }
    }

    public struct ScalarVar : IValue<string> {
        public string Value { get; }

        public ScalarVar(string s) {
            Value = s;
        }
    }

    public struct ListVar : IValue<string> {
        public string Value { get; }

        public ListVar(string l) {
            Value = l;
        }
    }

    public struct ValueList : IValue {
        public readonly List<IValue> Values;

        public ValueList(List<IValue> values) {
            Values = values;
        }
    }

    public struct Function {
        public delegate IValue Call(Broccoli context, IValue[] argv);

        private uint _argc;
        private Call _call;

        public Function(uint argc, Call call) {
            _argc = argc;
            _call = call;
        }

        public IValue Invoke(Broccoli context, IValue[] argv) {
            // TODO: argc checking
            return _call(context, argv);
        }
    }
}