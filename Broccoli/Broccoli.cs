using System;
using System.Collections.Generic;
using System.Linq;
using Broccoli.Parsing;

namespace Broccoli {
    public class BroccoliScope {
        public readonly Dictionary<string, IValue> Scalars = new Dictionary<string, IValue>();
        public readonly Dictionary<string, ValueList> Lists = new Dictionary<string, ValueList>();
        public readonly Dictionary<string, IFunction> Functions = new Dictionary<string, IFunction>();
        public readonly BroccoliScope Parent;

        public BroccoliScope() { }

        public BroccoliScope(BroccoliScope parent) {
            Parent = parent;
        }

        public IValue Get(ScalarVar s) => Scalars.ContainsKey(s.Value) ? Scalars[s.Value] : Parent?.Get(s);

        public bool Set(ScalarVar s, IValue value, bool self = false, bool initial = true) {
            if (self || Scalars.ContainsKey(s.Value)) {
                Scalars[s.Value] = value;
                return true;
            }
            if (Parent != null && Parent.Set(s, value))
                return true;
            if (!initial)
                return false;

            Scalars[s.Value] = value;
            return true;
        }

        public IValue this[ScalarVar s] {
            get => Get(s);

            set => Set(s, value);
        }

        public ValueList Get(ListVar l) => Lists.ContainsKey(l.Value) ? Lists[l.Value] : Parent?.Get(l);

        public bool Set(ListVar l, ValueList value, bool self = false, bool initial = true) {
            if (self || Lists.ContainsKey(l.Value)) {
                Lists[l.Value] = value;
                return true;
            }
            if (Parent != null && Parent.Set(l, value, false))
                return true;
            if (!initial)
                return false;

            Lists[l.Value] = value;
            return true;
        }

        public ValueList this[ListVar l] {
            get => Get(l);

            set => Set(l, value);
        }

        public IFunction Get(string l) => Functions.ContainsKey(l) ? Functions[l] : Parent?.Get(l);

        public bool Set(string l, IFunction value, bool self = false, bool initial = true) {
            if (self || Functions.ContainsKey(l)) {
                Functions[l] = value;
                return true;
            }
            if (Parent != null && Parent.Set(l, value, false))
                return true;
            if (!initial)
                return false;

            Functions[l] = value;
            return true;
        }

        public IFunction this[string l] {
            get => Get(l);

            set => Set(l, value);
        }
    }

    public partial class Interpreter {
        public BroccoliScope Scope = new BroccoliScope();
        public Dictionary<string, IFunction> Builtins = DefaultBuiltins;

        public Interpreter() { }

        public IValue Run(string code) => Run(Parser.Parse(code));

        public IValue Run(ParseNode node) {
            IValue result = null;
            foreach (var e in node.Children.Select(s => (ValueExpression) s))
                result = Run(e);
            return result;
        }

        public IValue Run(IValueExpressible expr) {
            IValue result;
            switch (expr) {
                case ScalarVar s:
                    result = Scope[s];
                    if (result == null)
                        throw new Exception($"Scalar {s.Value} not found");
                    return result;
                case ListVar l:
                    result = Scope[l];
                    if (result == null)
                        throw new Exception($"List {l.Value} not found");
                    return result;
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