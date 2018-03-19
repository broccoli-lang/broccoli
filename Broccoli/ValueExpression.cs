using System.Collections.Generic;
using System.Linq;

namespace Broccoli {
    /// <summary>
    /// Represents a class that can be stored in a <see cref="ValueExpression"/>.
    /// </summary>
    public interface IValueExpressible { }

    /// <summary>
    /// Represents an unevaluated Broccoli expression full of values.
    /// </summary>
    public class ValueExpression : IValueExpressible {
        public readonly IValueExpressible[] Values;
        public bool IsValue { get; }

        public ValueExpression(params IValueExpressible[] values) {
            Values = values;
            IsValue = false;
        }

        public ValueExpression(IEnumerable<IValueExpressible> values) {
            Values = values.ToArray();
            IsValue = false;
        }

        public ValueExpression(IValueExpressible value) {
            Values = new[] { value };
            IsValue = true;
        }

        /// <summary>
        /// Converts a <see cref="ParseNode"/> to the equivalent <see cref="ValueExpression"/>.
        /// </summary>
        /// <param name="n">The <see cref="ParseNode"/> to convert.</param>
        /// <returns>THe expression represented by the <see cref="ParseNode"/>.</returns>
        public static implicit operator ValueExpression(ParseNode n) {
            IValue Listify(ParseNode node) {
                if (node.Token != null)
                    return node.Token.ToIValue();
                return new ValueList(node.Children.Select(Listify));
            }

            IValue Dictionarify(ParseNode node) {
                if (node.Token != null)
                    return node.Token.ToIValue();
                return new ValueDictionary(new Dictionary<IValue, IValue>(node.Children.Select(
                    child => new KeyValuePair<IValue, IValue>(child.Children[0].Token.ToIValue(), child.Children[1].Token.ToIValue())
                )));
            }

            IValueExpressible Selector(ParseNode node) {
                if (node.Token != null)
                    return node.Token.ToIValue();
                if (node.IsList)
                    return Listify(node);
                return new ValueExpression(node.Children.Select(Selector));
            }
            return n.Children == null ?
                new ValueExpression(n.Token.ToIValue()) :
                n.IsList ?
                new ValueExpression((IValueExpressible) new ValueList(n.Children.Select(Listify))) :
                n.IsDictionary ?
                new ValueExpression(Dictionarify(n)) :
                new ValueExpression(n.Children.Select(Selector));
        }

        public override string ToString() {
            return '(' + string.Join<IValueExpressible>(' ', Values) + ')';
        }
    }
}