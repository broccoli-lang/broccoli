using System.Collections.Generic;
using System.Linq;

namespace Broccoli {
    /// <summary>
    ///     Represents a node in the overall parse tree that represents a Broccoli program.
    /// </summary>
    public class ParseNode {
        public int    CommentDepth      = 0;
        public bool   ExpectsDictionary = false;
        public bool   ExpectsList       = false;
        public bool   Finished;
        public bool   IsComment         = false;
        public bool   IsDictionary      = false;
        public bool   IsList            = false;
        public string UnfinishedComment = null;
        public string UnfinishedString  = null;

        public ParseNode() {
            Token    = null;
            Children = new List<ParseNode>();
            Finished = false;
        }

        public ParseNode(IEnumerable<ParseNode> nodes) {
            Token    = null;
            Children = nodes.ToList();
            Finished = false;
        }

        public ParseNode(Token token) {
            Token    = token;
            Children = null;
            Finished = true;
        }

        public Token           Token    { get; }
        public List<ParseNode> Children { get; }

        public override string ToString() => Token != null ? Token.Literal : '(' + string.Join(' ', Children) + ')';

        public void Finish() => Finished = true;
    }
}
