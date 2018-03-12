using System;
using System.Collections.Generic;

namespace Broccoli {
    public class Broccoli {
        public readonly Dictionary<string, IValue> Scalars = new Dictionary<string, IValue>();
        public readonly Dictionary<string, ValueList> Lists = new Dictionary<string, ValueList>();

        public readonly Dictionary<string, Function> Functions = new Dictionary<string, Function> {
            {":=", new Function(2, (context, argv) => context.Scalars[((ScalarVar) argv[0]).Value] = argv[1])}
        };

        private string _code;

        public Broccoli(string code) {
            _code = code;
        }

        public void Run() {
            throw new NotImplementedException();
        }
    }
}