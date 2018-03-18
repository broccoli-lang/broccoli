using System;
using System.Linq;

namespace Broccoli {
    public class ArgumentTypeException : Exception {
        public object Object { get; }
        public string ExpectedType { get; }
        public int Argument { get; }
        public string Caller { get; }

        public ArgumentTypeException(object o, string expectedType, int n, string caller) : base(
            $"Recieved {o.GetType().ToString().Split('.').Last().ToLower().Substring(o.GetType().ToString().Contains("Broccoli") ? 1 : 0)} instead of {expectedType} in argument {n} for '{caller}'"
        ) {
            Object = o;
            ExpectedType = expectedType;
            Argument = n;
            Caller = caller;
        }

        public ArgumentTypeException(string message) : base(message) { }
    }
}
