using System;

namespace Broccoli {
    public class NoScalarContextException : Exception {
        public NoScalarContextException(object instance) : base("Object of type '" + instance.GetType().ToString() + "' cannot be used in scalar context by default") { }
    }

    public class NoListContextException : Exception {
        public NoListContextException(object instance) : base("Object of type '" + instance.GetType().ToString() + "' cannot be used in list context by default") { }
    }

    public class NoDictionaryContextException : Exception {
        public NoDictionaryContextException(object instance) : base("Object of type '" + instance.GetType().ToString() + "' cannot be used in dictionary context by default") { }
    }
}
