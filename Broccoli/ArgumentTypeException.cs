using System;

namespace Broccoli {
    public class ArgumentTypeException : Exception {
        public object Object { get; }
        public string ExpectedType { get; }
        public int Argument { get; }
        public string Caller { get; }

        private static string TypeName(Type type) {
            var name = type.Name;
            var fullName = type.FullName;
            return name.Substring(name.Length > 2 && name[0] == 'B' && char.IsUpper(name[1]) && fullName.Contains("Broccoli") ? 1 : 0);
        }

        public ArgumentTypeException(object o, string expectedType, int n, string caller) : base(
            $"Recieved {TypeName(o.GetType())} instead of {expectedType} in argument {n} for '{caller}'"
        ) {
            Object = o;
            ExpectedType = expectedType;
            Argument = n;
            Caller = caller;
        }

        public ArgumentTypeException(string message) : base(message) { }
    }
}
