using System.Collections.Generic;
using Broccoli.Tokenization;

namespace Broccoli {
    public partial class Broccoli {
        public readonly Dictionary<string, IValue> Scalars = new Dictionary<string, IValue>();
        public readonly Dictionary<string, ValueList> Lists = new Dictionary<string, ValueList>();

        // Functions dictionary defined in Builtins.cs

        private readonly SExpression[] _rootExpressions;

        public Broccoli(string code) {
            _rootExpressions = new Tokenizer(code).RootSExps;
        }

        public void Run() {
            foreach (var sexp in _rootExpressions) {
                // TODO
            }
        }
    }
}