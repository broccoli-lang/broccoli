using System;
using System.Text.RegularExpressions;
using Broccoli;

#pragma warning disable IDE1006

namespace cauliflower.core {
    public class regex : IScalar {
        public static Interpreter interpreter;
        private static IValue[] ctorArgs = new[] { (ScalarVar) "pattern" };
        public Regex Value { get; }

        public regex(IValue[] args) {
            Function.ValidateArgs(1, ctorArgs, "regex#regex");
            if (!(args[0] is BString pattern))
                throw new ArgumentTypeException(args[0], "string", 1, "match");
            Value = new Regex(pattern.Value);
        }

        public IValue match(IValue input) {
            if (!(input is BString inputS))
                throw new ArgumentTypeException(input, "string", 1, "match");
            return CauliflowerInterpreter.CreateValue(Value.Match(inputS.Value));
        }

        public IValue match(IValue input, IValue beginning) {
            if (!(input is BString inputS))
                throw new ArgumentTypeException(input, "string", 1, "match");
            if (!(beginning is BInteger beginningI))
                throw new ArgumentTypeException(beginning, "integer", 1, "match");
            return CauliflowerInterpreter.CreateValue(Value.Match(inputS.Value, (int) beginningI.Value));
        }

        public IValue match(IValue input, IValue beginning, IValue length) {
            if (!(input is BString inputS))
                throw new ArgumentTypeException(input, "string", 1, "match");
            if (!(beginning is BInteger beginningI))
                throw new ArgumentTypeException(beginning, "integer", 1, "match");
            if (!(length is BInteger lengthI))
                throw new ArgumentTypeException(length, "integer", 1, "match");
            return CauliflowerInterpreter.CreateValue(Value.Match(inputS.Value, (int) beginningI.Value, (int) lengthI.Value));
        }

        public override string ToString() => Value.ToString();

        public string Inspect() => ToString();

        public object ToCSharp() => Value;

        public Type Type() => typeof(Regex);
    }
}