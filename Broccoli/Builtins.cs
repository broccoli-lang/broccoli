using System;
using System.Collections.Generic;

namespace Broccoli {
    public partial class Broccoli {
        public readonly Dictionary<string, Function> Functions = new Dictionary<string, Function> {
            {":=", new Function(":=", 2, (context, argv) => {
                var arg = argv[0];
                switch (arg) {
                    // According to Rider, pattern matching isn't allowed to be used in lambdas in field initializers
                    // This was fixed in .NET in https://github.com/dotnet/roslyn/pull/17101
                    // Looks like it's allowed in Visual Studio, but not in Rider for whatever reason
                    // I'll just ignore it
                    case ScalarVar s:
                        if (argv[1] is ValueList) throw new Exception("Lists cannot be assigned to scalar ($) variables");
                        context.Scalars[s.Value] = argv[1];
                        break;
                    case ListVar l:
                        var list = argv[1] as ValueList?;
                        if (!list.HasValue) throw new Exception("Scalars cannot be assigned to list (@) variables");
                        context.Lists[l.Value] = list.Value;
                        break;
                    default:
                        throw new Exception("Values can only be assigned to scalar ($) or list (@) variables");
                }

                return argv[1];
            }, true)}
        };
    }
}