namespace Broccoli.Parsing {
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