using System.Collections.Generic;
using System.Linq;
using Broccoli.Parsing;

namespace Broccoli {
    public interface IValueExpressible { }

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

        public static implicit operator ValueExpression(ParseNode n) {
            IValueExpressible Selector(ParseNode node) {
                if (node.Token != null)
                    return node.Token.ToIValue();
                return new ValueExpression(node.Children.Select(Selector));
            }
            return n.Children == null ? new ValueExpression(n.Token.ToIValue()) : new ValueExpression(n.Children.Select(Selector));
        }

        public override string ToString() {
            return '(' + string.Join<IValueExpressible>(' ', Values) + ')';
        }
    }
}