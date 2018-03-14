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

                            var toAssign = EvaluateExpression(args[1]);
                            switch (args[0]) {
                                case ScalarVar s:
                                    if (toAssign is ValueList) throw new Exception("Lists cannot be assigned to scalar ($) variables");
                                    Scalars[s.Value] = toAssign;
                                    break;
                                case ListVar l:
                                    var list = toAssign as ValueList?;
                                    if (!list.HasValue) throw new Exception("Scalars cannot be assigned to list (@) variables");
                                    Lists[l.Value] = list.Value;
                                    break;
                                default:
                                    throw new Exception("Values can only be assigned to scalar ($) or list (@) variables");
                            }

                            return toAssign;
                        case "if":
                            // TODO
                            return null;
                        case "fn":
                            // TODO
                            return null;
                        case "for":
                            // TODO
                            return null;
                        default:
                            return Functions[fnName].Invoke(args.Select(EvaluateExpression).ToArray());
                    }
                default:
                    return (IValue) expr;
            }
        }
    }
}