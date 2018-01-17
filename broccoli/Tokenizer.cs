namespace broccoli
{
    public class Tokenizer
    {
        private string source;
        private Token[] tokens;

        public Tokenizer(string _source, Token[] _tokens)
        {
            source = _source;
            tokens = _tokens;
        }

        public Token[] scanToken()
        {
            // TODO
        }
    }
}