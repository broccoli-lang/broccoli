using System;
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

    public struct ValueList : IValue<List<IValue>> {
        public List<IValue> Value { get; }

        public ValueList(List<IValue> values) {
            Value = values;
        }
    }

    public struct Function {
        public delegate IValue Call(Broccoli context, IValue[] argv);

        private string _name;
        private int _argc;
        private readonly Call _call;

        // If the function has variadic arguments, argc is -n-1, where n is the number of required args
        public Function(string name, int argc, Call call) {
            _name = name;
            _argc = argc;
            _call = call;
        }

        // ReSharper disable once UnusedMethodReturnValue.Global
        public IValue Invoke(Broccoli context, IValue[] argv) {
            bool isVariadic = _argc < 0;
            int requiredArgc = isVariadic ? -_argc - 1 : _argc;

            if (argv.Length < requiredArgc) {
                throw new Exception($"Function {_name} requires {(isVariadic ? "at least" : "exactly")} {requiredArgc} arguments, only {argv.Length} provided");
            }

            return _call(context, argv);
        }
    }
}