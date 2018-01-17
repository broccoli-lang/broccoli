namespace Broccoli.Tokenization
{
    public struct Token
    {
               
        public TokenType Type { get; }
        public string Literal { get; }
        public uint LineNumber { get; }

        public Token(TokenType type, string literal, uint line)
        {
            Type = type;
            Literal = literal;
            LineNumber = line;
        }
    }
}