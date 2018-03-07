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

            var innerTokens = tokens.GetRange(1, tokens.Count - 2);
            var sexpRanges = GetSexpRangesList(innerTokens);
            var newValues = new List<ISExpressible>();

            for (var i = 0; i < innerTokens.Count; i++)
            {
                try
                {
                    var range = sexpRanges.First(r => (i >= r.Item1) && (i <= r.Item2));
                    newValues.Add(new SExpression(innerTokens.GetRange(range.Item1, range.Item2 - range.Item1 + 1)));
                    i = range.Item2; // Easy way to skip ahead in a for loop
                }
                catch
                {
                    newValues.Add(innerTokens[i]);
                }
            }

            Values = ImmutableList.CreateRange(newValues);
        }

        public static IEnumerable<(int, int)> GetSexpRangesList(List<Token> tokens)
        {
            var sexpRanges = new List<(int, int)>();
            int parenIndex = 0;

            while (true)
            {
                parenIndex = tokens.Select(t => t.Type).ToList().IndexOf(TokenType.LeftParen, parenIndex);
                if (parenIndex == -1) return sexpRanges;

                int rightParenIndex = MatchingCloseParenIndex(tokens.Skip(parenIndex + 1).ToList()) + parenIndex + 1;

                sexpRanges.Add((parenIndex, rightParenIndex));
                parenIndex = rightParenIndex;
            }
        }

        private static int MatchingCloseParenIndex(List<Token> tokens)
        {
            uint amountOpen = 1;

            foreach (var (token, index) in tokens.WithIndex())
            {
                if (token.Type == TokenType.LeftParen) amountOpen++;
                if (token.Type == TokenType.RightParen) amountOpen--;
                if (amountOpen == 0) return index;
            }

            throw new Exception("Unmatched left parenthesis!");
        }

        public override string ToString()
        {
            var newVals = Values.Select(v =>
            {
                if (v is Token token)
                    return token.Literal;

                return v.ToString();
            });
            return '(' + string.Join(' ', newVals) + ')';
        }
    }
}