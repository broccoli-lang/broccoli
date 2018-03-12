using System;
using System.Collections.Generic;

namespace Broccoli {
    public class Broccoli
    {
        public readonly Dictionary<string, IValue> Scalars = new Dictionary<string, IValue>();
        public readonly Dictionary<string, ValueList> Lists = new Dictionary<string, ValueList>();
        public readonly Dictionary<string, Function> Functions = new Dictionary<string, Function>();

        private string _code;
        public Broccoli(string code) {
            _code = code;
        }

        public void Run() {
            throw new NotImplementedException();
        }
    }
}