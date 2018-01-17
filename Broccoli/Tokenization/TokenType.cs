namespace Broccoli.Tokenization
{
    public enum TokenType
        {
            // Structure
            LeftParen, RIGHT_PAREN,
            
            // Literals
            String, Integer, Float,
            
            // Identifiers
            Identifier, Scalar, List,
            
            // Miscellaneous
            Eof
        }
}