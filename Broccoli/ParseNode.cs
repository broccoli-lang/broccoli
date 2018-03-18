using System.Collections.Generic;
using System.Linq;

namespace Broccoli {
    /// <summary>
    /// Represents a node in the overall parse tree that represents a Broccoli program.
    /// </summary>
    public class ParseNode {
        public bool Finished;
        public bool ExpectsList = false;
        public string UnfinishedString = null;
        public Token Token { get; }
        public List<ParseNode> Children { get; }

        public ParseNode() {
            Token = null;
            Children = new List<ParseNode>();
            Finished = false;
        }

        public ParseNode(IEnumerable<ParseNode> nodes) {
            Token = null;
            Children = nodes.ToList();
            Finished = false;
        }

        public ParseNode(Token token) {
            Token = token;
            Children = null;
            Finished = true;
        }

        public override string ToString() {
            return Token != null ? Token.Literal : '(' + string.Join<ParseNode>(' ', Children) + ')';
        }

        public void Finish() {
            Finished = true;
        }
    }
}
