using System;
using System.Collections.Generic;
using System.Linq;

namespace Broccoli.Tokenization
{
    public class Tokenizer
    {
        private string[] _source;
        private uint _row = 1;
        private uint _column = 0;
        public readonly List<Token> Tokens = new List<Token>() ;

        public Tokenizer(string source)
        {
            _source = source.Split('\n');
        }

        public void ScanToken()
        {
            try
            {
                char c;
                switch (c = NextChar())
                {
                    // Single-char tokens
                    case '(':
                        Tokens.Add(new Token(TokenType.LeftParen, "(", _row, _column));
                        break;
                    case ')':
                        Tokens.Add(new Token(TokenType.RightParen, ")", _row, _column));
                        break;
                    // Variables
                    case '$':
                        Tokens.Add(new Token(TokenType.Scalar, "$" + NextIdentifier(), _row, _column));
                        break;
                    case '@':
                        Tokens.Add(new Token(TokenType.List, "@" + NextIdentifier(), _row, _column));
                        break;
                    // Strings
                    case '"':
                        string str = string.Empty;

                        while ((c = NextChar()) != '"')
                        {
                            // The original interpreter only supports \\ and \" escape sequences
                            if (c == '\\') c = NextChar();

                            str += c;
                        }

                        Tokens.Add(new Token(TokenType.String, str, _row, _column));
                        break;
                    // Numbers
                    case char d when char.IsDigit(c):
                        string num = string.Empty;

                        while (char.IsDigit(c = NextChar()) || c == '.')
                            num += c;

                        Tokens.Add(new Token(num.Contains('.') ? TokenType.Float : TokenType.Integer, d + num, _row, _column));
                        break;
                    // Misc
                    case '\r': // TODO: Make this handle unexpected carriage returns
                    case '\n':
                        NextLine();
                        break;
                    case ' ':
                        break;
                    default:
                        _column--;
                        Tokens.Add(new Token(TokenType.Identifier, NextIdentifier(), _row, _column));
                        break;
                }
            }
            catch (IndexOutOfRangeException)
            {
                Tokens.Add(new Token(TokenType.Eof, "", _row, _column));
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

        private bool IsValidIdentifier(string s)
        {
            if (s.Length == 0 || !s[0].In("_:=+-*/") && ! char.IsLetter(s[0]))
                return false;

            foreach (var i in s.Skip(1))
            {
                if (!(i.In("_:=+-*/") || char.IsLetterOrDigit(i)))
                    return false;
            }
            return true;
        }
    }
}