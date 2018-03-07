using System;
using System.Collections.Generic;
using System.Linq;

namespace Broccoli.Tokenization
{
    public class Tokenizer
    {
        private readonly string[] _source;
        private uint _row = 1;
        private uint _column = 0;
        public readonly SExpression[] RootSExps;

        public Tokenizer(string source)
        {
            _source = source.Split('\n');

            var tokens = new List<Token>();
            do
            {
                var token = ScanToken();
                if (token != null) tokens.Add(token);
            } while (tokens.Last().Type != TokenType.Eof);

            // TODO: Figure out if Eof needs to stay in the overall tokens.
            tokens.RemoveAt(tokens.Count - 1);

            RootSExps = SExpression.GetSexpRangesList(tokens)
                .Select(t => new SExpression(tokens.GetRange(t.Item1, t.Item2 - t.Item1 + 1))).ToArray();
        }

        private Token ScanToken()
        {
            try
            {
                char c;
                switch (c = NextChar())
                {
                    // Single-char tokens
                    case '(':
                        return new Token(TokenType.LeftParen, "(", _row, _column);
                    case ')':
                        return new Token(TokenType.RightParen, ")", _row, _column);
                    // Variables
                    case '$':
                        return new Token(TokenType.Scalar, '$' + NextIdentifier(), _row, _column);
                    case '@':
                        return new Token(TokenType.List, '@' + NextIdentifier(), _row, _column);
                    // Strings
                    case '"':
                        string str = string.Empty;

                        while ((c = NextChar()) != '"')
                        {
                            // The original interpreter only supports \\ and \" escape sequences
                            if (c == '\\') c = NextChar();

                            str += c;
                        }

                        return new Token(TokenType.String, str, _row, _column);
                    // Numbers
                    case char d when char.IsDigit(c):
                        string num = string.Empty;

                        while (char.IsDigit(c = NextChar()) || c == '.')
                            num += c;

                        RewindChar();

                        return new Token(num.Contains('.') ? TokenType.Float : TokenType.Integer, d + num, _row, _column);
                    // Misc
                    case '\r': // TODO: Make this handle unexpected carriage returns
                    case '\n':
                        NextLine();
                        return null;
                    case ' ':
                        return null;
                    default:
                        RewindChar();
                        return new Token(TokenType.Identifier, NextIdentifier(), _row, _column);
                }
            }
            catch (IndexOutOfRangeException)
            {
                return new Token(TokenType.Eof, "", _row, _column);
            }
        }

        private void RewindChar()
        {
            if (_column == 0)
            {
                _row--;
                _column = (uint) _source[_row - 1].Length - 1;
            }
            else
            {
                _column--;
            }
        }

        private char NextChar()
        {
            return _source[_row - 1][(int) _column++];
        }

        private void NextLine()
        {
            _row++;
            _column = 0;
        }

        private string NextIdentifier()
        {
            string result = string.Empty;
            char c;

            while (char.IsLetterOrDigit(c = NextChar()) || c.In("_:=+-*/"))
            {
                result += c;
            }

            if (!IsValidIdentifier(result)) throw new Exception($"Invalid identifier at {_row}:{_column}"); // TODO: Custom exception class?

            _column--;
            return result;
        }

        private static bool IsValidIdentifier(string s)
        {
            if (s.Length == 0 || !s[0].In("_:=+-*/") && ! char.IsLetter(s[0]))
                return false;

            return s.Skip(1).All(i => i.In("_:=+-*/") || char.IsLetterOrDigit(i));
        }
    }
}