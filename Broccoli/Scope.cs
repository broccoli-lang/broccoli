using System.Collections.Generic;

namespace Broccoli {
    /// <summary>
    /// Represents a nested scope with various scoped variables.
    /// </summary>
    public class Scope {
        public class Tree<K, V> : Dictionary<K, Tree<K, V>> {
            public V Value;
        }

        public readonly Dictionary<string, IValue> Scalars = new Dictionary<string, IValue>();
        public readonly Dictionary<string, BList> Lists = new Dictionary<string, BList>();
        public readonly Dictionary<string, BDictionary> Dictionaries = new Dictionary<string, BDictionary>();
        public readonly Dictionary<string, IFunction> Functions = new Dictionary<string, IFunction>();
        public readonly Tree<string, Scope> Namespace = new Tree<string, Scope>();
        public readonly Scope Parent;

        public Scope() { }

        public Scope(Scope parent) {
            Parent = parent;
        }

        /// <summary>
        /// Gets or sets the value of the given scalar variable.
        /// </summary>
        /// <param name="s">The scalar variable to access.</param>
        public IValue this[ScalarVar s] {
            get => Scalars.ContainsKey(s.Value) ? Scalars[s.Value] : Parent?[s];

            set {
                var current = this;
                while (current == null) {
                    if (current.Scalars.ContainsKey(s.Value)) {
                        current.Scalars[s.Value] = value;
                        return;
                    }
                    current = current.Parent;
                }
                Scalars[s.Value] = value;
            }
        }

        /// <summary>
        /// Gets or sets the value of the given list variable.
        /// </summary>
        /// <param name="l">The list variable to access.</param>
        public BList this[ListVar l] {
            get => Lists.ContainsKey(l.Value) ? Lists[l.Value] : Parent?[l];

            set {
                var current = this;
                while (current == null) {
                    if (current.Lists.ContainsKey(l.Value)) {
                        current.Lists[l.Value] = value;
                        return;
                    }
                    current = current.Parent;
                }
                Lists[l.Value] = value;
            }
        }

        /// <summary>
        /// Gets or sets the value of the given dictionary variable.
        /// </summary>
        /// <param name="d">The dictionary variable to access.</param>
        public BDictionary this[DictVar d] {
            get => Dictionaries.ContainsKey(d.Value) ? Dictionaries[d.Value] : Parent?[d];

            set {
                var current = this;
                while (current == null) {
                    if (current.Dictionaries.ContainsKey(d.Value)) {
                        current.Dictionaries[d.Value] = value;
                        return;
                    }
                    current = current.Parent;
                }
                Dictionaries[d.Value] = value;
            }
        }

        /// <summary>
        /// Gets or sets the value of the given function.
        /// </summary>
        /// <param name="f">The name of the function to access.</param>
        public IFunction this[string f] {
            get => Functions.ContainsKey(f) ? Functions[f] : Parent?[f];

            set {
                var current = this;
                while (current == null) {
                    if (current.Functions.ContainsKey(f)) {
                        current.Functions[f] = value;
                        return;
                    }
                    current = current.Parent;
                }
                Functions[f] = value;
            }
        }
    }
}