using System.Collections.Generic;

namespace Broccoli {
    /// <summary>
    /// Represents a nested scope with various scoped variables.
    /// </summary>
    public class CauliflowerScope : Scope {
        public CauliflowerScope() { }

        public CauliflowerScope(Scope parent) : base(parent) { }

        /// <summary>
        /// Gets or sets the value of the given scalar variable.
        /// </summary>
        /// <param name="s">The scalar variable to access.</param>
        public override IScalar this[ScalarVar s] {
            get => Scalars.ContainsKey(s.Value) ?
                Scalars[s.Value] :
                Parent?[s] ?? (Lists.ContainsKey(s.Value) ?
                    Lists[s.Value].ScalarContext() :
                    Parent?[new ListVar(s)]?.ScalarContext() ?? (Dictionaries.ContainsKey(s.Value) ?
                        Dictionaries[s.Value].ScalarContext() :
                        Parent?[new DictVar(s)]?.ScalarContext()
                    )
                );

            set {
                Scope current = this;
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
        public override IList this[ListVar l] {
            get => Lists.ContainsKey(l.Value) ?
                Lists[l.Value] :
                Parent?[l] ?? (Scalars.ContainsKey(l.Value) ?
                    Scalars[l.Value].ListContext() :
                    Parent?[new ScalarVar(l)]?.ListContext() ?? (Dictionaries.ContainsKey(l.Value) ?
                        Dictionaries[l.Value].ListContext() :
                        Parent?[new DictVar(l)]?.ListContext()
                    )
                );

            set {
                Scope current = this;
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
        public override IDictionary this[DictVar d] {
            get => Dictionaries.ContainsKey(d.Value) ?
                Dictionaries[d.Value] :
                Parent?[d] ?? (Scalars.ContainsKey(d.Value) ?
                    Scalars[d.Value].DictionaryContext() :
                    Parent?[new ScalarVar(d)]?.DictionaryContext() ?? (Lists.ContainsKey(d.Value) ?
                        Lists[d.Value].DictionaryContext() :
                        Parent?[new ListVar(d)]?.DictionaryContext()
                    )
                );

            set {
                Scope current = this;
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
    }
}