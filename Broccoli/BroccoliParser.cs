using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Broccoli {
    public partial class Interpreter {
        private static Regex _rNewline = new Regex(@"(?<=[\r\n][\s\r\n]*[\r\n]|[\r\n])", RegexOptions.Compiled);
        private static Regex _rComment = new Regex(@"\G;[\s\S]*$", RegexOptions.Compiled);
        private static Regex _rWhitespace = new Regex(@"\G\s+", RegexOptions.Compiled);
        private static Regex _rString = new Regex(@"\G""(?:\\[\s\S]|[^""])*""", RegexOptions.Compiled);
        private static Regex _rStringStart = new Regex(@"\G""(?:\\[\s\S]|[^""])*$", RegexOptions.Compiled);
        private static Regex _rStringEnd = new Regex(@"^(?:\\[\s\S]|[^""])*""", RegexOptions.Compiled);
        private static Regex _rNumber = new Regex(@"\G-?(?:\d*\.\d+|\d+\.?\d*|\d*)", RegexOptions.Compiled);
        private static Regex _rScalar = new Regex(@"\G\$[^\s()$@%&|~][^\s()&|~""]*", RegexOptions.Compiled);
        private static Regex _rList = new Regex(@"\G@[^\s()$@%&|~][^\s()&|~""]*", RegexOptions.Compiled);
        private static Regex _rName = new Regex(@"\G[^\s\d()$@%&|~][^\s()&|~""]*", RegexOptions.Compiled);
        private static Regex _rEscapes = new Regex(@"\\(.)", RegexOptions.Compiled);

        /// <summary>
        /// Takes a string and traverses it to create a ParseNode with associated values.
        /// </summary>
        /// <param name="s">The string to parse.</param>
        /// <param name="p">A partially-parsed node coming from multiline inputs in the REPL.</param>
        /// <param name="keepComments">Whether to keep comments in the parse tree or not.</param>
        /// <returns>Returns the root ParseNode that represents the string.</returns>
        /// <exception cref="Exception">Thrown when the parser fails to parse an token.</exception>
        public virtual ParseNode Parse(string s, ParseNode p = null, bool keepComments = false) {
            var source = _rNewline.Split(s).ToList();
            var result = new ParseNode();
            var stack = new List<ParseNode> { result };
            var current = result;
            var depth = 0;
            if (p != null) {
                result = p;
                stack = new List<ParseNode> { p };
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
                    Match rawMatch;
                    string value = null;
                    string match;
                    if (current.UnfinishedString != null) {
                        rawMatch = _rStringEnd.Match(line);
                        if (!rawMatch.Success) {
                            current.UnfinishedString += _rEscapes.Replace(line, "$1");
                            column = line.Length;
                            continue;
                        }
                        match = rawMatch.ToString();
                        value = current.UnfinishedString + _rEscapes.Replace(match.Substring(0, match.Length - 1), "$1");
                        current.UnfinishedString = null;
                        type = TokenType.String;
                    } else
                        switch (c) {
                            // S-expressions
                            case '(':
                                depth++;
                                column++;
                                var next = new ParseNode();
                                current.Children.Add(next);
                                current = next;
                                stack.Add(current);
                                continue;
                            case ')':
                                if (--depth < 0)
                                    throw new Exception("Unexpected stray ')' at top level");
                                column++;
                                current.Finish();
                                stack.RemoveAt(stack.Count - 1);
                                current = stack.Last();
                                continue;
                            // Variables
                            case '$':
                                match = _rScalar.Match(line, column).ToString();
                                value = match.Substring(1);
                                type = TokenType.ScalarName;
                                break;
                            case '@':
                                match = _rList.Match(line, column).ToString();
                                value = match.Substring(1);
                                type = TokenType.ListName;
                                break;
                            // Strings
                            case '"':
                                rawMatch = _rString.Match(line, column);
                                if (!rawMatch.Success) {
                                    current.UnfinishedString = _rEscapes.Replace(_rStringStart.Match(line, column).ToString().Substring(1), "$1");
                                    column = line.Length;
                                    continue;
                                }
                                match = rawMatch.ToString();
                                value = _rEscapes.Replace(match.Substring(1, match.Length - 2), "$1");
                                type = TokenType.String;
                                break;
                            // Numbers
                            case '-':
                            case char _ when char.IsDigit(c):
                                match = value = _rNumber.Match(line, column).ToString();
                                if (value == "-") {
                                    match = value = _rName.Match(line, column).ToString();
                                    type = TokenType.Atom;
                                } else
                                    type = value.Contains('.') ? TokenType.Float : TokenType.Integer;
                                break;
                            // Whitespace
                            case ' ':
                            case '\t':
                            case '\f':
                            case '\v':
                            case '\r':
                            case '\n':
                                match = _rWhitespace.Match(line, column).ToString();
                                break;
                            // Comments
                            case ';':
                                match = _rComment.Match(line, column).ToString();
                                if (keepComments) {
                                    value = match.Substring(match.Length > 2 && match[1] == ' ' ? 2 : 1);
                                    type = TokenType.Comment;
                                }
                                break;
                            // Identifiers (default)
                            default:
                                match = value = _rName.Match(line, column).ToString();
                                type = TokenType.Atom;
                                break;
                        }
                        if (type != TokenType.None)
                            current.Children.Add(new ParseNode(new Token(type, value)));
                        if (match.Length == 0)
                            throw new Exception($"Could not match token '{Regex.Escape("" + c)}' at {row + 1}:{column}");
                        column += match.Length;
                    }
                }
            if (current.UnfinishedString == null && (result.Children.Count() == 0 || result.Children.Last().Finished))
                result.Finished = true;
            return result;
        }
    }
}
