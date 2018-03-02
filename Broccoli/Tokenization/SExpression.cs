using System.Collections.Generic;
using System.Collections.Immutable;

namespace Broccoli.Tokenization
{
    public interface ISExpressible { }
    
    public class SExpression : ISExpressible
    {
        public readonly ImmutableList<ISExpressible> Values;

        public SExpression(List<Token> tokens)
        {
            // No idea why I have to type cast here, but I do
            Values = ImmutableList.CreateRange((IEnumerable<ISExpressible>) tokens);
            // TODO: Get all values between a LeftParen and a RightParen and create new SExpressions inside them if nested.
        }
    }
}