using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Broccoli.Tokenization
{
    public interface ISExpressible { }

    public class SExpression : ISExpressible
    {
        public readonly ImmutableList<ISExpressible> Values;

        public SExpression(List<Token> tokens)
        {
            if (tokens.First().Type != TokenType.LeftParen) throw new Exception("First token in sexp must be left paren!");
            if (tokens.Last().Type != TokenType.RightParen) throw new Exception("Last token in sexp must be right paren!");

            var innerTokens = tokens.GetRange(1, tokens.Count - 1);
            // TODO: Get all inner tokens and create new SExpressions inside them if nested.

            Values = ImmutableList.CreateRange((IEnumerable<ISExpressible>) innerTokens);
        }
    }
}