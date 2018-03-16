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

        public IValue Get(ScalarVar s) {
            if (Scalars.ContainsKey(s.Value))
                return Scalars[s.Value];

            return Parent?.Get(s);
        }

        public bool Set(ScalarVar s, IValue value, bool self = false, bool initial = true) {
            if (self || Scalars.ContainsKey(s.Value)) {
                Scalars[s.Value] = value;
                return true;
            }
            if (Parent != null && Parent.Set(s, value))
                return true;
            if (!initial) return false;

            Scalars[s.Value] = value;
            return true;
        }

        public IValue this[ScalarVar s] {
            get {
                return Get(s);
            }

            set {
                Set(s, value);
            }
        }

        public ValueList Get(ListVar l) {
            if (Lists.ContainsKey(l.Value))
                return Lists[l.Value];

            return Parent?.Get(l);
        }

        public bool Set(ListVar l, ValueList value, bool self = false, bool initial = true) {
            if (self || Lists.ContainsKey(l.Value)) {
                Lists[l.Value] = value;
                return true;
            }
            if (Parent != null && Parent.Set(l, value, false))
                return true;
            if (!initial) return false;

            Lists[l.Value] = value;
            return true;
        }

        public ValueList this[ListVar l] {
            get {
                return Get(l);
            }

            set {
                Set(l, value);
            }
        }

        public IFunction Get(string l) {
            if (Functions.ContainsKey(l))
                return Functions[l];

            return Parent?.Get(l);
        }

        public bool Set(string l, IFunction value, bool self = false, bool initial = true) {
            if (self || Functions.ContainsKey(l)) {
                Functions[l] = value;
                return true;
            }
            if (Parent != null && Parent.Set(l, value, false)) return true;
            if (!initial) return false;

            Functions[l] = value;
            return true;
        }

        public IFunction this[string l] {
            get {
                return Get(l);
            }

            set {
                Set(l, value);
            }
        }
    }

    public partial class Broccoli {
        public BroccoliScope Scope = new BroccoliScope();
        public Dictionary<string, IFunction> Builtins = DefaultBuiltins;

        public Broccoli() { }

        public IValue Run(string code) {
            IValue result = null;
            foreach (var e in Parser.Parse(code).Children.Select(s => (ValueExpression) s))
                result = EvaluateExpression(e);
            return result;
        }

        public IValue Run(ParseNode node) {
            IValue result = null;
            foreach (var e in node.Children.Select(s => (ValueExpression) s))
                result = EvaluateExpression(e);
            return result;
        }

        public IValue EvaluateExpression(IValueExpressible expr) {
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
                    return new ValueList(l.Value.Select(EvaluateExpression).ToList());
                case ValueExpression e:
                    return Builtins[""].Invoke(this, new[] { e });
                case IValue i:
                    return i;
                default:
                    throw new Exception($"Unexpected expression type {expr.GetType()}");
            }
        }
    }
}