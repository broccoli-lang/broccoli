using System;
using System.Collections.Generic;
using System.Linq;
using Broccoli.Tokenization;

namespace Broccoli {
    public partial class Broccoli {
        public readonly Dictionary<string, IValue> Scalars = new Dictionary<string, IValue>();
        public readonly Dictionary<string, ValueList> Lists = new Dictionary<string, ValueList>();

        // Functions dictionary defined in Builtins.cs

        private readonly ValueExpression[] _rootExpressions;

        public Broccoli(string code) {
            _rootExpressions = new Tokenizer(code).RootSExps.Select(s => (ValueExpression) s).ToArray();
        }

        public void Run() {
            foreach (var vexp in _rootExpressions) {
                // TODO: Does this need to be expanded?
                EvaluateExpression(vexp);
            }
        }

        private IValue EvaluateExpression(ValueExpression vexp) {
            throw new NotImplementedException();
        }
    }
}