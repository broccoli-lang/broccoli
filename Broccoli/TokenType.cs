namespace Broccoli {
    /// <summary>
    /// Represents the different types of raw token before parsing.
    /// </summary>
    public enum TokenType {
        // Literals
        List, 
        String,
        Integer,
        Float,

        // Identifiers
        Atom,
        ScalarName,
        ListName,
        DictionaryName,

        // Miscellaneous
        Comment,
        None,
    }
}