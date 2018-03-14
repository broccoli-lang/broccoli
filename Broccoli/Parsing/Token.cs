using System;

namespace Broccoli.Parsing {
    public class Token {
        public TokenType Type { get; }
        public string Literal { get; }

        public Token(TokenType type, string literal) {
            Type = type;
            Literal = literal;
        }

        public override string ToString() {
            return (Type, Literal).ToString();
        }

        public IValue ToIValue() {
            switch (Type) {
                case TokenType.String:
                    return new String(Literal);
                case TokenType.Integer:
                    return new Integer(int.Parse(Literal));
                case TokenType.Float:
                    return new Float(double.Parse(Literal));
                case TokenType.Atom:
                    return new Atom(Literal);
                case TokenType.Scalar:
                    return new ScalarVar(Literal);
                case TokenType.List:
                    return new ListVar(Literal);
                default:
                    throw new Exception($"{Type} cannot be converted to a value");
            }
        }
    }
}