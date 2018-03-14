using System;

namespace Broccoli {
    public struct Function {
        public delegate IValue Call(IValue[] args);

        private readonly string _name;
        private readonly int _argc;
        private readonly Call _call;

        // If the function has variadic arguments, argc is -n-1, where n is the number of required args
        public Function(string name, int argc, Call call) {
            _name = name;
            _argc = argc;
            _call = call;
        }

        // ReSharper disable once UnusedMethodReturnValue.Global
        public IValue Invoke(IValue[] args) {
            ValidateArgs(_argc, args, _name);
            return _call(args);
        }

        public static void ValidateArgs<T>(int argc, T[] args, string name) {
            bool isVariadic = argc < 0;
            int requiredArgc = isVariadic ? -argc - 1 : argc;

            if (args.Length < requiredArgc) {
                throw new Exception($"Function {name} requires {(isVariadic ? "at least" : "exactly")} {requiredArgc} arguments, {args.Length} provided");
            }
        }
    }
}