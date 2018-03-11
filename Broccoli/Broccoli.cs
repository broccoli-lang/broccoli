using System;
using System.Collections.Generic;

namespace Broccoli {
    public class Broccoli
    {
        public readonly Dictionary<string, IValue> Scalars = new Dictionary<string, IValue>();
        // public readonly Dictionary<string, ?> arrays;
        // public readonly Dictionary<string, ?> functions;

        private string _code;
        public Broccoli(string code) {
            _code = code;
        }

        public void Run() {
            throw new NotImplementedException();
        }
    }
}