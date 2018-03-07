namespace Broccoli.Tokenization
{
    public class Token : ISExpressible
    {

        public TokenType Type { get; }
        public string Literal { get; }
        public (uint Line, uint Column) Position { get; }

        public Token(TokenType type, string literal, uint line, uint column)
        {
            Type = type;
            Literal = literal;
            Position = (line, column);
        }

        public override string ToString()
        {
            return (Type, Literal).ToString();
        }
    }
}