using System;
using System.Text.RegularExpressions;
using System.Linq;
using Broccoli;

#pragma warning disable IDE1006

namespace cauliflower.core {
    public class regex : IScalar {
        private static IValue[] ctorArgs = new[] { (ScalarVar) "pattern" };
        public Regex Value { get; }

        public regex(IValue[] args) {
            Function.ValidateArgs(1, ctorArgs, "regex#regex");
            if (!(args[0] is BString pattern))
                throw new ArgumentTypeException(args[0], "string", 1, "match");
            Value = new Regex(pattern.Value);
        }

        public IValue match(Interpreter interpreter, IValue[] args) {
            if (args.Length == 0 || args.Length > 3)
                throw new Exception($"Function 'regex#match' requires from 1 to 3 arguments, {args.Length} provided");
            if (!(args[0] is BString input))
                throw new ArgumentTypeException(args[0], "string", 1, "match");
            if (args.Count() == 1)
                return CauliflowerInterpreter.CreateValue(Value.Match(input.Value));
            if (!(args[1] is BInteger beginning))
                throw new ArgumentTypeException(args[1], "integer", 2, "match");
            if (args.Count() == 2)
                return CauliflowerInterpreter.CreateValue(Value.Match(input.Value, (int) beginning.Value));
            if (!(args[2] is BInteger length))
                throw new ArgumentTypeException(args[2], "integer", 3, "match");
            return CauliflowerInterpreter.CreateValue(Value.Match(input.Value, (int) beginning.Value, (int) length.Value));
        }

        public override string ToString() => Value.ToString();

        public string Inspect() => ToString();

        public object ToCSharp() => Value;

        public Type Type() => typeof(Regex);
    }
}