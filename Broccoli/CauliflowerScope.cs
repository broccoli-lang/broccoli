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
            get => Scalars.ContainsKey(s.Value) ? Scalars[s.Value] : Parent?[s] ?? this[new ListVar(s.Value)]?.ScalarContext() ?? this[new DictVar(s.Value)].ScalarContext();

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
            get => Lists.ContainsKey(l.Value) ? Lists[l.Value] : Parent?[l] ?? this[new ScalarVar(l.Value)]?.ListContext() ?? this[new DictVar(l.Value)].ListContext();

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
            get => Dictionaries.ContainsKey(d.Value) ? Dictionaries[d.Value] : Parent?[d] ?? this[new ScalarVar(d.Value)]?.DictionaryContext() ?? this[new ListVar(d.Value)].DictionaryContext();

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