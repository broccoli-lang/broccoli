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

        private IValue EvaluateExpression(IValueExpressible expr) {
            switch (expr) {
                case ScalarVar s:
                    return Scalars[s.Value];
                case ListVar l:
                    return Lists[l.Value];
                case ValueList l:
                    return new ValueList(l.Value.Select(EvaluateExpression).ToList());
                case ValueExpression vexp:
                    var first = vexp.Values.First();
                    var fnAtom = first as Atom?;
                    if (fnAtom == null)
                        throw new Exception($"Function name {first} must be an identifier");

                    var args = vexp.Values.Skip(1).ToArray();
                    var fnName = fnAtom.Value.Value;
                    switch (fnName) {
                        // Pre-evaluation/substitution syntactic "functions"
                        case ":=":
                            Function.ValidateArgs(2, args, ":=");
                            // TODO
                            return null;
                        case "if":
                            // TODO
                            return null;
                        case "for":
                            // TODO
                            return null;
//                        default:
//                            Functions[fnName].Invoke(this, args);
                    }
                    break;
                default:
                    return (IValue) expr;
            }

            // The compiler yells at me if this isn't here
            return null;
        }
    }
}