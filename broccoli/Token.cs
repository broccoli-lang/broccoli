namespace broccoli
{
    public struct Token
    {
        public enum TokenType
        {
            // Structure
            LEFT_PAREN, RIGHT_PAREN,
            
            // Literals
            STRING, INTEGER, FLOAT,
            
            // Identifiers
            IDENTIFIER, SCALAR, LIST,
            
            // Miscellaneous
            EOF
        }
        
        public TokenType Type { get; }
        public string Literal { get; }
        public int LineNumber { get; }

        public Token(TokenType type, string literal, int line)
        {
            Type = type;
            Literal = literal;
            LineNumber = line;
        }
    }
}