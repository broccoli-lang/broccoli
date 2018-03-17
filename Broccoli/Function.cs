using System;
using System.Linq;

namespace Broccoli {
    public interface IFunction {
        IValue Invoke(Interpreter broccoli, params IValueExpressible[] args);
    }

    public struct Function : IFunction {
        public delegate IValue Call(Interpreter broccoli, IValue[] args);

        private readonly string _name;
        private readonly int _argc;
        private readonly Call _call;

        // If the function has variadic arguments, argc is -n - 1, where n is the number of required args
        public Function(string name, int argc, Call call) {
            _name = name;
            _argc = argc;
            _call = call;
        }

        // ReSharper disable once UnusedMethodReturnValue.Global
        public IValue Invoke(Interpreter broccoli, params IValueExpressible[] args) {
            var runArgs = args.ToList().Select(broccoli.Run).ToArray();
            ValidateArgs(_argc, runArgs, _name);
            return _call(broccoli, runArgs);
        }

        public static void ValidateArgs<T>(int argc, T[] args, string name) {
            bool isVariadic = argc < 0;
            int requiredArgc = isVariadic ? -argc - 1 : argc;

            if (isVariadic ? args.Length < requiredArgc : args.Length != requiredArgc)
                throw new Exception($"Function {name} requires {(isVariadic ? "at least" : "exactly")} {requiredArgc} arguments, {args.Length} provided");
        }
    }

    public struct ShortCircuitFunction : IFunction {
        public delegate IValue Call(Interpreter broccoli, IValueExpressible[] args);

        private readonly string _name;
        private readonly int _argc;
        private readonly Call _call;

        // If the function has variadic arguments, argc is -n-1, where n is the number of required args
        public ShortCircuitFunction(string name, int argc, Call call) {
            _name = name;
            _argc = argc;
            _call = call;
        }

        // ReSharper disable once UnusedMethodReturnValue.Global
        public IValue Invoke(Interpreter broccoli, params IValueExpressible[] args) {
            Function.ValidateArgs(_argc, args, _name);
            return _call(broccoli, args);
        }
    }

    public struct AnonymousFunction : IFunction, IValue {
        public delegate IValue Call(IValue[] args);

        private readonly int _argc;
        private readonly Call _call;

        public AnonymousFunction(int argc, Call call) {
            _argc = argc;
            _call = call;
        }

        public IValue Invoke(Interpreter broccoli, params IValueExpressible[] args) {
            var runArgs = args.ToList().Select(broccoli.Run).ToArray();
            Function.ValidateArgs(_argc, runArgs, "(anonymous)");
            return _call(runArgs);
        }

        public override string ToString() => $"(anonymous function with {_argc} arguments)";
    }
}