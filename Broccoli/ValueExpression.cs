using System;
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
                return new BList(node.Children.Select(Listify));
            }

            IValue Dictionarify(ParseNode node) {
                if (node.Token != null)
                    return node.Token.ToIValue();
                return new BDictionary(new Dictionary<IValue, IValue>(node.Children.Select(
                    child => child.Children?.Count == 2 ?
                        new KeyValuePair<IValue, IValue>((IValue) Selector(child.Children[0]), (IValue) Selector(child.Children[1])) :
                        throw new Exception($"Expected key value pair in dictionary literal, found {(child.Children == null ? child.Token.Literal.GetType().ToString() : child.Children.Count + " arguments")}")
                )));
            }

            IValueExpressible Selector(ParseNode node) {
                if (node.Token != null)
                    return node.Token.ToIValue();
                if (node.IsList)
                    return Listify(node);
                if (node.IsDictionary)
                    return Dictionarify(node);
                return new ValueExpression(node.Children.Select(Selector));
            }
            return n.Children == null ?
                new ValueExpression(n.Token.ToIValue()) :
                n.IsList ?
                new ValueExpression((IValueExpressible) new BList(n.Children.Select(Listify))) :
                n.IsDictionary ?
                new ValueExpression(Dictionarify(n)) :
                new ValueExpression(n.Children.Select(Selector));
        }

        public override string ToString() {
            return '(' + string.Join<IValueExpressible>(' ', Values) + ')';
        }
    }
}