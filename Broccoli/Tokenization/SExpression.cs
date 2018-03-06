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
            var sexpRanges = new List<(int, int)>();
            int parenIndex = 0;

            while (parenIndex != -1)
            {
                parenIndex = innerTokens.Select(t => t.Type).ToList().IndexOf(TokenType.LeftParen, parenIndex + 1);
                int rightParenIndex = MatchingCloseParenIndex(innerTokens.Skip(parenIndex + 1).ToList());

                sexpRanges.Add((parenIndex, rightParenIndex));
                parenIndex = rightParenIndex;
            }

            Values = ImmutableList.CreateRange((IEnumerable<ISExpressible>) innerTokens);
        }

        private int MatchingCloseParenIndex(List<Token> tokens)
        {
            uint amountOpen = 1;

            foreach ((var token, var index) in tokens.WithIndex())
            {
                if (token.Type == TokenType.LeftParen) amountOpen++;
                if (token.Type == TokenType.RightParen) amountOpen--;
                if (amountOpen == 0) return index;
                Console.WriteLine($"amount open: {amountOpen}, index: {index}");
            }

            throw new Exception("Unmatched left parenthesis!");
        }
    }
}