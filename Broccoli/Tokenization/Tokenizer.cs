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
        private List<Token> _tokens = new List<Token>();

        public Tokenizer(string _source)
        {
            this._source = _source.Split('\n');
        }

        public void ScanToken()
        {
            try
            {
                switch (NextChar())
                {
                    // Single-char tokens
                    case '(':
                        _tokens.Add(new Token(TokenType.LeftParen, "(", _row, _column));
                        break;
                    case ')':
                        _tokens.Add(new Token(TokenType.RightParen, ")", _row, _column));
                        break;
                    // Variables
                    case '$':
                        _tokens.Add(new Token(TokenType.Scalar, "$" + NextIdentifier(), _row, _column));
                        break;
                    case '@':
                        _tokens.Add(new Token(TokenType.List, "@" + NextIdentifier(), _row, _column));
                        break;
                }
            }
            catch (IndexOutOfRangeException)
            {
                _tokens.Add(new Token(TokenType.Eof, "", _row, _column));
            }
        }

        private char NextChar()
        {
            return _source[_row - 1][(int) _column];
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

            while (Char.IsLetterOrDigit(c = NextChar()) || c == '_')
            {
                result += c;
            }
            
            if (!IsValidIdentifier(result)) throw new Exception(); // TODO: Custom exception class?

            return result;
        }

        private bool IsValidIdentifier(string s)
        {
            if (s[0]!= '_' && ! char.IsLetter(s[0]))
                return false;

            foreach (var i in s.Skip(1))
            {
                if (i != '_' && ! char.IsLetterOrDigit(i))
                    return false;
            }
            return true;
        }
    }
}