using System;
using System.Linq;
using Broccoli.Tokenization;

namespace Broccoli {
    public interface IValueExpressible { }

    public class ValueExpression : IValueExpressible {
        public readonly IValueExpressible[] Values;

        public ValueExpression(IValueExpressible[] values) {
            Values = values;
        }

        public static explicit operator ValueExpression(SExpression sexp) {
            IValueExpressible Selector(ISExpressible v) {
                switch (v) {
                    case Token t:
                        return t.ToIValue();
                    case SExpression s:
                        return new ValueExpression(s.Values.Select(Selector).ToArray());
                    default:
                        // Should never happen
                        throw new ArgumentOutOfRangeException();
                }
            }

            return new ValueExpression(sexp.Values.Select(Selector).ToArray());
        }

        public override string ToString() {
            return '(' + string.Join<IValueExpressible>(' ', Values) + ')';
        }
    }
}