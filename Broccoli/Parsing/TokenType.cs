namespace Broccoli.Parsing {
    /// <summary>
    /// Represents the different types of raw token before parsing.
    /// </summary>
    public enum TokenType {
        // Literals
        String,
        Integer,
        Float,

        // Identifiers
        Atom,
        Scalar,
        List,
        Dict,

        // Miscellaneous
        None
    }
}