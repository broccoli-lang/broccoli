using System;
using System.Linq;

namespace Broccoli {
    public struct Function {
        public delegate IValue Call(Broccoli context, IValue[] argv);

        private readonly string _name;
        private readonly int _argc;
        private readonly Call _call;
        // It would be so cool if I made this an attribute, but I don't care enough
        private readonly bool _preserveVars;

        // If the function has variadic arguments, argc is -n-1, where n is the number of required args
        public Function(string name, int argc, Call call, bool preserveVars = false) {
            _name = name;
            _argc = argc;
            _call = call;
            _preserveVars = preserveVars;
        }

        // ReSharper disable once UnusedMethodReturnValue.Global
        public IValue Invoke(Broccoli context, IValue[] argv) {
            bool isVariadic = _argc < 0;
            int requiredArgc = isVariadic ? -_argc - 1 : _argc;

            if (argv.Length < requiredArgc) {
                throw new Exception($"Function {_name} requires {(isVariadic ? "at least" : "exactly")} {requiredArgc} arguments, {argv.Length} provided");
            }

            if (_preserveVars) return _call(context, argv);

            IValue Replacer(IValue a) {
                switch (a) {
                    case ScalarVar s:
                        return context.Scalars[s.Value];
                    case ListVar l:
                        return context.Lists[l.Value];
                    case ValueList l:
                        return new ValueList(l.Value.Select(Replacer).ToList());
                    default:
                        return a;
                }
            }

            argv = argv.Select(Replacer).ToArray();

            return _call(context, argv);
        }
    }
}