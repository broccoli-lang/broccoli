namespace Broccoli.Tokenization
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
}