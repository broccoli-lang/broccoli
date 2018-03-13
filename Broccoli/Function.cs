using System;

namespace Broccoli {
    public struct Function {
        public delegate IValue Call(Broccoli context, IValue[] argv);

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
        public IValue Invoke(Broccoli context, IValue[] argv) {
            ValidateArgs(_argc, argv, _name);
            return _call(context, argv);
        }

        public static void ValidateArgs<T>(int argc, T[] argv, string name) {
            bool isVariadic = argc < 0;
            int requiredArgc = isVariadic ? -argc - 1 : argc;

            if (argv.Length < requiredArgc) {
                throw new Exception($"Function {name} requires {(isVariadic ? "at least" : "exactly")} {requiredArgc} arguments, {argv.Length} provided");
            }
        }
    }
}