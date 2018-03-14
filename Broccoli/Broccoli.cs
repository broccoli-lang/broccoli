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
            if (Parent != null)
                return Parent.Get(s);
            return null;
        }

        public bool Set(ScalarVar s, IValue value, bool self = true, bool initial = true) {
            if (self || Scalars.ContainsKey(s.Value)) {
                Scalars[s.Value] = value;
                return true;
            }
            if (Parent != null && Parent.Set(s, value, false))
                return true;
            if (initial) {
                Scalars[s.Value] = value;
                return true;
            }
            return false;
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
            if (Parent != null)
                return Parent.Get(l);
            return null;
        }

        public bool Set(ListVar l, ValueList value, bool self = true, bool initial = true) {
            if (self || Lists.ContainsKey(l.Value)) {
                Lists[l.Value] = value;
                return true;
            }
            if (Parent != null && Parent.Set(l, value, false))
                return true;
            if (initial) {
                Lists[l.Value] = value;
                return true;
            }
            return false;
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
            if (Parent != null)
                return Parent.Get(l);
            return null;
        }

        public bool Set(string l, IFunction value, bool self = true, bool initial = true) {
            if (self || Functions.ContainsKey(l)) {
                Functions[l] = value;
                return true;
            }
            if (Parent != null && Parent.Set(l, value, false))
                return true;
            if (initial) {
                Functions[l] = value;
                return true;
            }
            return false;
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

        // Functions dictionary defined in Builtins.cs

        private readonly IEnumerable<ValueExpression> _rootExpressions;

        public Broccoli() { }

        public Broccoli(string code) {
            _rootExpressions = Parser.Parse(code).Children.Select(s => (ValueExpression) s);
        }

        public void Run() {
            foreach (var e in _rootExpressions)
                // TODO: Does this need to be expanded?
                EvaluateExpression(e);
        }

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
            switch (expr) {
                case ScalarVar s:
                    return Scope[s];
                case ListVar l:
                    return Scope[l];
                case ValueList l:
                    return new ValueList(l.Value.Select(EvaluateExpression).ToList());
                case ValueExpression e:
                    var first = e.Values.First();
                    var fnAtom = first as Atom?;
                    if (fnAtom == null)
                        throw new Exception($"Function name {first} must be an identifier");

                    var args = e.Values.Skip(1).ToArray();
                    var fnName = fnAtom.Value.Value;
                    IFunction fn = Scope[fnName];
                    if (fn == null) {
                        if (Builtins.ContainsKey(fnName))
                            fn = Builtins[fnName];
                        else
                            throw new Exception($"Function {fnName} does not exist");
                    }
                    return fn.Invoke(this, args);
                default:
                    return (IValue) expr;
            }
        }
    }
}