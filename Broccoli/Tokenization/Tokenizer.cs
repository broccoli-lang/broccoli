using System;
using System.Collections.Generic;

namespace Broccoli.Tokenization
{
    public class Tokenizer
    {
        private string[] source;
        private uint row = 1;
        private uint column = 0;
        private List<Token> tokens = new List<Token>();

        public Tokenizer(string _source)
        {
            source = _source.Split('\n');
        }

        public void ScanToken()
        {
            try
            {
                switch (NextChar())
                {
                    // Single-char tokens
                    case '(':
                        tokens.Add(new Token(TokenType.LeftParen, "(", row, column));
                        break;
                    case ')':
                        tokens.Add(new Token(TokenType.RightParen, ")", row, column));
                        break;
                    // Variables
                    case '$':
                        tokens.Add(new Token(TokenType.Scalar, "$" + NextIdentifier(), row, column));
                        break;
                    case '@':
                        tokens.Add(new Token(TokenType.List, "@" + NextIdentifier(), row, column));
                        break;
                }
            }
            catch (IndexOutOfRangeException)
            {
                tokens.Add(new Token(TokenType.Eof, "", row, column));
            }
        }

        private char NextChar()
        {
            return source[row - 1][(int) column];
        }

        private void NextLine()
        {
            row++;
            column = 0;
        }

        private string NextIdentifier()
        {
            string result = string.Empty;
            char c;

            while (!IsValidIdentifier(c = NextChar()))
            {
                result += c;
            }

            return result;
        }

        private bool IsValidIdentifier(char c)
        {
            return c == '_' || !(Char.IsWhiteSpace(c) || Char.IsPunctuation(c));
        }
    }
}