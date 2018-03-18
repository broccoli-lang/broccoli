using System;
using System.Globalization;
namespace Broccoli {
    /// <summary>
    /// Represents a token with associated string representation before parsing to values.
    /// </summary>
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

                // Int32#Parse and Float#Parse are locale dependent!
                // E.g. in Russia it might switch the meaning of `.` and `,`.
                case TokenType.Integer:
                    return new BInteger(int.Parse(Literal, CultureInfo.InvariantCulture)); 
                case TokenType.Float:
                    return new BFloat(double.Parse(Literal, CultureInfo.InvariantCulture));
                case TokenType.Atom:
                    return new BAtom(Literal);
                case TokenType.ScalarName:
                    return new ScalarVar(Literal);
                case TokenType.ListName:
                    return new ListVar(Literal);
                case TokenType.DictionaryName:
                    return new DictVar(Literal);
                default:
                    throw new Exception($"{Type} cannot be converted to a value");
            }
        }
    }
}