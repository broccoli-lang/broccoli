namespace Broccoli.Tokenization
{
    public enum TokenType
        {
            // Structure
            LeftParen, RightParen,
            
            // Literals
            String, Integer, Float,
            
            // Identifiers
            Identifier, Scalar, List,
            
            // Miscellaneous
            Eof
        }
}
