﻿using System.Collections.Generic;
using BCSharpType = Broccoli.CauliflowerInterpreter.BCSharpType;

namespace Broccoli {
    /// <summary>
    /// Represents a nested scope with various scoped variables.
    /// </summary>
    public class Scope {
        public class Tree<TKey, TValue> : Dictionary<TKey, Tree<TKey, TValue>> {
            public TValue Value;

            public void Add(Tree<TKey, TValue> other) {
                foreach (var (key, value) in other)
                    if (ContainsKey(key))
                        this[key].Add(value);
                    else
                        this[key] = value;
            }
        }

        public readonly Dictionary<string, IScalar> Scalars = new Dictionary<string, IScalar>();
        public readonly Dictionary<string, IList> Lists = new Dictionary<string, IList>();
        public readonly Dictionary<string, IDictionary> Dictionaries = new Dictionary<string, IDictionary>();
        public readonly Dictionary<string, IFunction> Functions = new Dictionary<string, IFunction>();
        public readonly Tree<string, Scope> Namespaces = new Tree<string, Scope>();
        public readonly Dictionary<string, BCSharpType> Types = new Dictionary<string, BCSharpType>();
        public readonly Scope Parent;

        public Scope() { }

        public Scope(Scope parent) {
            Parent = parent;
        }

        /// <summary>
        /// Gets or sets the value of the given scalar variable.
        /// </summary>
        /// <param name="s">The scalar variable to access.</param>
        public virtual IScalar this[ScalarVar s] {
            get => Scalars.ContainsKey(s.Value) ? Scalars[s.Value] : Parent?[s];

            set {
                var current = this;
                do {
                    if (current.Scalars.ContainsKey(s.Value)) {
                        current.Scalars[s.Value] = value;
                        return;
                    }

                    current = current.Parent;
                } while (current != null);
                Scalars[s.Value] = value;
            }
        }

        /// <summary>
        /// Gets or sets the value of the given list variable.
        /// </summary>
        /// <param name="l">The list variable to access.</param>
        public virtual IList this[ListVar l] {
            get => Lists.ContainsKey(l.Value) ? Lists[l.Value] : Parent?[l];

            set {
                var current = this;
                do {
                    if (current.Lists.ContainsKey(l.Value)) {
                        current.Lists[l.Value] = value;
                        return;
                    }

                    current = current.Parent;
                } while (current != null);
                Lists[l.Value] = value;
            }
        }

        /// <summary>
        /// Gets or sets the value of the given dictionary variable.
        /// </summary>
        /// <param name="d">The dictionary variable to access.</param>
        public virtual IDictionary this[DictVar d] {
            get => Dictionaries.ContainsKey(d.Value) ? Dictionaries[d.Value] : Parent?[d];

            set {
                var current = this;
                do {
                    if (current.Dictionaries.ContainsKey(d.Value)) {
                        current.Dictionaries[d.Value] = value;
                        return;
                    }

                    current = current.Parent;
                } while (current != null);
                Dictionaries[d.Value] = value;
            }
        }

        /// <summary>
        /// Gets or sets the value of the given function.
        /// </summary>
        /// <param name="f">The name of the function to access.</param>
        public virtual IFunction this[string f] {
            get => Functions.ContainsKey(f) ? Functions[f] : Parent?[f];

            set {
                var current = this;
                do {
                    if (current.Functions.ContainsKey(f)) {
                        current.Functions[f] = value;
                        return;
                    }

                    current = current.Parent;
                } while (current != null);
                Functions[f] = value;
            }
        }

        /// <summary>
        /// Gets the type corresponding to the given type name.
        /// </summary>
        /// <param name="t">The type name to retrieve.</param>
        public BCSharpType this[TypeName t] {
            get => Types.ContainsKey(t) ? Types[t] : Parent?[t];

            set => Types[t] = value;
        }
    }
}
