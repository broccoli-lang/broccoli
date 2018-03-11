using System;
using System.Collections.Generic;

namespace Broccoli {
    public class Broccoli
    {
        // Nothing I can do right now to prevent an IValueList from being assigned to a scalar on a type level
        // Just don't do it, k?
        public readonly Dictionary<string, IValue> Scalars = new Dictionary<string, IValue>();
        public readonly Dictionary<string, IValueList> Lists = new Dictionary<string, IValueList>();
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