using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Broccoli.Parsing {
    public class ParseNode {
        public readonly List<string> Source;
        private int _row;
        private int _column;
        public bool Finished;
        public Token Token { get; }
        public List<ParseNode> Children { get; }

        public ParseNode(IEnumerable<string> source, int row, int column) {
            Token = null;
            Children = new List<ParseNode>();
            _row = row;
            _column = column;
            Source = source.ToList();
            Finished = false;
        }

        public ParseNode(IEnumerable<ParseNode> nodes, IEnumerable<string> source, int row, int column) {
            Token = null;
            Children = nodes.ToList();
            _row = row;
            _column = column;
            Source = source.ToList();
            Finished = false;
        }

        public ParseNode(Token token, IEnumerable<string> source, int row, int column) {
            Token = token;
            Children = null;
            _row = row;
            _column = column;
            Source = source.ToList();
            Finished = true;
        }

        public override string ToString() {
            return Token != null ? Token.Literal : '(' + string.Join<ParseNode>(' ', Children) + ')';
        }

        public void Finish() {
            Finished = true;
        }
    }

    public static class Parser {
        private static Regex _rSpaces = new Regex(@"\G\s+");
        private static Regex _rString = new Regex(@"\G""(?:\\.|.)+""");
        private static Regex _rNumber = new Regex(@"\G-?(?:\d*\.\d+|\d+\.?\d*)");
        private static Regex _rScalar = new Regex(@"\G\$[^\s()$@]+");
        private static Regex _rList = new Regex(@"\G@[^\s()$@]+");
        private static Regex _rName = new Regex(@"\G[^\s\d()$@][^\s()$@]*");
        private static Regex _rEscapes = new Regex(@"\\(.)");

        public static ParseNode Parse(string s, ParseNode p = null) {
            var source = s.Split('\n').ToList();
            var result = new ParseNode(source, 0, 0);
            var stack = new List<ParseNode> { result };
            var current = result;
            var depth = 0;
            if (p != null) {
                result = p;
                stack = new List<ParseNode> { p };
                p.Source.AddRange(source);
                current = result;
                depth = 1;
                while (true) {
                    if (current.Children.Count == 0)
                        break;
                    var last = current.Children.Last();
                    if (last.Children == null || last.Finished)
                        break;
                    current = last;
                    stack.Add(current);
                    depth++;
                }
            }
            foreach (var (line, row) in source.WithIndex()) {
                var column = 0;
                while (column < line.Length) {
                    var c = line[column];
                    TokenType type = TokenType.None;
                    string value = null;
                    string match = null;
                    if (depth == 0 && c != ' ' && c != '(')
                        throw new Exception($"Expected S-expression, found character '{c}' instead");
                    switch (c) {
                        // S-expressions
                        case '(':
                            depth++;
                            column++;
                            var next = new ParseNode(source, row, column);
                            current.Children.Add(next);
                            current = next;
                            stack.Add(current);
                            continue;
                        case ')':
                            depth--;
                            column++;
                            current.Finish();
                            stack.RemoveAt(stack.Count - 1);
                            current = stack.Last();
                            continue;
                        // Variables
                        case '$':
                            match = _rScalar.Match(line, column).ToString();
                            value = match.Substring(1);
                            type = TokenType.Scalar;
                            break;
                        case '@':
                            match = _rList.Match(line, column).ToString();
                            value = match.Substring(1);
                            type = TokenType.List;
                            break;
                        // Strings
                        case '"':
                            match = _rString.Match(line, column).ToString();
                            value = _rEscapes.Replace(match.Substring(1, match.Length - 2), "$1");
                            type = TokenType.String;
                            break;
                        // Numbers
                        case '-':
                        case char _ when char.IsDigit(c):
                            match = value = _rNumber.Match(line, column).ToString();
                            type = value == "-" ? TokenType.Atom : value.Contains('.') ? TokenType.Float : TokenType.Integer;
                            break;
                        // Whitespace
                        case ' ':
                            match = _rSpaces.Match(line, column).ToString();
                            break;
                        // Identifiers (default)
                        default:
                            match = value = _rName.Match(line, column).ToString();
                            type = TokenType.Atom;
                            break;
                    }
                    if (type != TokenType.None)
                        current.Children.Add(new ParseNode(new Token(type, value), source, row, column));
                    if (match.Length == 0)
                        throw new Exception($"Could not match token at {row + 1}:{column}");
                    column += match.Length;
                }
            }
            if (result.Children.Count() == 0 || result.Children.Last().Finished)
                result.Finished = true;
            return result;
        }
    }
}