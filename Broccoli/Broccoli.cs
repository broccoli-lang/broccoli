using System;
using System.Collections.Generic;
using System.Linq;

namespace Broccoli {
    /// <summary>
    /// The interpreter for the Broccoli language.
    /// </summary>
    public partial class Interpreter {
        public Scope Scope = new Scope();
        public Dictionary<string, IFunction> Builtins = StaticBuiltins;

        /// <summary>
        /// Runs the given string as Broccoli code.
        /// </summary>
        /// <param name="code">The code to run.</param>
        /// <returns>The final value returned by the overall expression.</returns>
        public virtual IValue Run(string code) => Run(Parse(code));

        /// <summary>
        /// Evaluates the already-parsed tree as Broccoli code.
        /// </summary>
        /// <param name="node">The node to evaluate.</param>
        /// <returns>The final value returned by the root node.</returns>
        public virtual IValue Run(ParseNode node) {
            IValue result = null;
            foreach (var e in node.Children.Select(s => (ValueExpression) s))
                result = Run(e);
            return result;
        }

        /// <summary>
        /// Takes an expression or value and evaluates it.
        /// </summary>
        /// <param name="expr">The expression or value to evaluate.</param>
        /// <returns>The value represented by the expression or value.</returns>
        /// <exception cref="Exception">Throws when a variable is undefined or the expression is unhandled.</exception>
        public virtual IValue Run(IValueExpressible expr) {
            switch (expr) {
                case ScalarVar s:
                    return Scope[s] ?? throw new Exception($"Scalar {s.Value} not found");
                case ListVar l:
                    return Scope[l] ?? throw new Exception($"List {l.Value} not found");
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
