using System;
using System.Collections.Generic;
using System.Linq;
using Broccoli.Parsing;

namespace Broccoli {
    /// <summary>
    /// Represents a nested scope with various scoped variables.
    /// </summary>
    public class BroccoliScope {
        public readonly Dictionary<string, IValue> Scalars = new Dictionary<string, IValue>();
        public readonly Dictionary<string, ValueList> Lists = new Dictionary<string, ValueList>();
        public readonly Dictionary<string, IFunction> Functions = new Dictionary<string, IFunction>();
        public readonly Dictionary<string, ValueDict> Dicts = new Dictionary<string, ValueDict>();
        public readonly BroccoliScope Parent;

        public BroccoliScope() { }

        public BroccoliScope(BroccoliScope parent) {
            Parent = parent;
        }

        /// <summary>
        /// Gets or sets the value of the given scalar variable.
        /// </summary>
        /// <param name="s">The scalar variable to access.</param>
        public IValue this[ScalarVar s] {
            get => Scalars.ContainsKey(s.Value) ? Scalars[s.Value] : Parent?[s];

            set {
                if (Scalars.ContainsKey(s.Value)) {
                    Scalars[s.Value] = value;
                    return;
                }
                if (Parent != null) {
                    Parent[s] = value;
                    return;
                }

                Scalars[s.Value] = value;
            }
        }

        /// <summary>
        /// Gets or sets the value of the given list variable.
        /// </summary>
        /// <param name="l">The list variable to access.</param>
        public ValueList this[ListVar l] {
            get => Lists.ContainsKey(l.Value) ? Lists[l.Value] : Parent?[l];

            set {
                if (Lists.ContainsKey(l.Value)) {
                    Lists[l.Value] = value;
                    return;
                }
                if (Parent != null) {
                    Parent[l] = value;
                    return;
                }

                Lists[l.Value] = value;
            }
        }

        /// <summary>
        /// Gets or sets the value of the given dictionary variable.
        /// </summary>
        /// <param name="d">The dictionary variable to access.</param>
        public ValueDict this[DictVar d] {
            get => Dicts.ContainsKey(d.Value) ? Dicts[d.Value] : Parent?[d];

            set {
                if (Dicts.ContainsKey(d.Value)) {
                    Dicts[d.Value] = value;
                    return;
                }
                if (Parent != null) {
                    Parent[d] = value;
                    return;
                }

                Dicts[d.Value] = value;
            }
        }

        /// <summary>
        /// Gets or sets the value of the given function.
        /// </summary>
        /// <param name="l">The name of the function to access.</param>
        public IFunction this[string l] {
            get => Functions.ContainsKey(l) ? Functions[l] : Parent?[l];

            set {
                if (Functions.ContainsKey(l)) {
                    Functions[l] = value;
                    return;
                }
                if (Parent != null) {
                    Parent[l] = value;
                    return;
                }

                Functions[l] = value;
            }
        }
    }

    /// <summary>
    /// The core interpreter interpreter of a Broccoli program.
    /// </summary>
    public partial class Interpreter {
        public BroccoliScope Scope = new BroccoliScope();
        public Dictionary<string, IFunction> Builtins = DefaultBuiltins;

        public Interpreter() { }

        /// <summary>
        /// Runs the given string as Broccoli code.
        /// </summary>
        /// <param name="code">The code to run.</param>
        /// <returns>The final value returned by the overall expression.</returns>
        public IValue Run(string code) => Run(Parser.Parse(code));

        /// <summary>
        /// Evaluates the already-parsed tree as Broccoli code.
        /// </summary>
        /// <param name="node">The node to evaluate.</param>
        /// <returns>The final value returned by the root node.</returns>
        public IValue Run(ParseNode node) {
            IValue result = null;
            foreach (var e in node.Children.Select(s => (ValueExpression) s))
                result = Run(e);
            return result;
        }

        /// <summary>
        /// Takes an expression or value and evaluates it down to a value.
        /// </summary>
        /// <param name="expr">The expression or value to evaluate.</param>
        /// <returns>The value represented by the expression or value.</returns>
        /// <exception cref="Exception">Throws when a variable is undefined or the expression is unhandled.</exception>
        public IValue Run(IValueExpressible expr) {
            IValue result;
            switch (expr) {
                case ScalarVar s:
                    result = Scope[s];
                    return result ?? throw new Exception($"Scalar {s.Value} not found");
                case ListVar l:
                    result = Scope[l];
                    return result ?? throw new Exception($"List {l.Value} not found");
                case DictVar d:
                    result = Scope[d];
                    return result ?? throw new Exception($"Dict {d.Value} not found");
                case ValueList l:
                    return new ValueList(l.Value.Select(Run).ToList());
                case ValueExpression e:
                    return Builtins[""].Invoke(this, e);
                case IValue i:
                    return i;
                default:
                    throw new Exception($"Unexpected expression type {expr.GetType()}");
            }
        }
    }
}