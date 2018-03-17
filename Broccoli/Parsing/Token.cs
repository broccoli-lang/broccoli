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
                    return new BString(Literal);
                case TokenType.Integer:
                    return new BInteger(int.Parse(Literal));
                case TokenType.Float:
                    return new BFloat(double.Parse(Literal));
                case TokenType.Atom:
                    return new BAtom(Literal);
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