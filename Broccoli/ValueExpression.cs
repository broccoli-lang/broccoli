using System.Collections.Generic;
using System.Linq;
using Broccoli.Parsing;

namespace Broccoli {
    public interface IValueExpressible { }

    public class ValueExpression : IValueExpressible {
        public readonly IValueExpressible[] Values;

        public ValueExpression(IValueExpressible[] values) {
            Values = values;
        }

        public ValueExpression(IEnumerable<IValueExpressible> values) {
            Values = values.ToArray();
        }

        public static explicit operator ValueExpression(ParseNode n) {
            IValueExpressible Selector(ParseNode node) {
                if (node.Token != null)
                    return node.Token.ToIValue();
                return new ValueExpression(node.Children.Select(Selector));
            }

            return new ValueExpression(n.Children.Select(Selector).ToArray());
        }

        public override string ToString() {
            return '(' + string.Join<IValueExpressible>(' ', Values) + ')';
        }
    }
}