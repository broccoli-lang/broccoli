using System.Collections.Generic;
using System.Collections.Immutable;

namespace Broccoli.Tokenization
{
    public struct SExpression
    {
        // TODO: Make this more typesafe. It should only be able to store Tokens and other SExpressions.
        public ImmutableList<object> Values { get; }

        public SExpression(List<Token> tokens)
        {
            // TODO: Get all values between a LeftParen and a RightParen and create new SExpressions if nested.
            Values = ImmutableList<object>.Empty;
        }
    }
}