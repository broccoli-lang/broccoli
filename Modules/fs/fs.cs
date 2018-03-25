using System;
using System.Linq;
using System.IO;
using System.Collections.Generic;
using Broccoli;

#pragma warning disable IDE1006

namespace cauliflower.core {
    public static class fs {
        private static Dictionary<string, IValue[]> args = new Dictionary<string, IValue[]> {
            {"path", new[] { (ScalarVar) "path" } }
        };

        public static IValue exists(IValue[] values) {
            Function.ValidateArgs(1, args["path"], "fs#exists");
            if (!(values[0] is BString path))
                throw new ArgumentTypeException(values[0], "string", 1, "fs#exists");
            return Interpreter.Boolean(File.Exists(path.Value) || Directory.Exists(path.Value));
        }

        public static IValue file_exists(IValue[] values) {
            Function.ValidateArgs(1, args["path"], "fs#file-exists");
            if (!(values[0] is BString path))
                throw new ArgumentTypeException(values[0], "string", 1, "fs#file-exists");
            return Interpreter.Boolean(File.Exists(path.Value));
        }

        public static IValue directory_exists(IValue[] values) {
            Function.ValidateArgs(1, args["path"], "fs#directory-exists");
            if (!(values[0] is BString path))
                throw new ArgumentTypeException(values[0], "string", 1, "fs#directory-exists");
            return Interpreter.Boolean(Directory.Exists(path.Value));
        }

        public static IValue list_directories(IValue[] values) {
            Function.ValidateArgs(1, args["path"], "fs#list-directories");
            if (!(values[0] is BString path))
                throw new ArgumentTypeException(values[0], "string", 1, "fs#list-directories");
            return new BList(Directory.GetDirectories(path.Value).Select(CauliflowerInterpreter.CreateValue));
        }

        public static IValue list_files(IValue[] values) {
            Function.ValidateArgs(1, args["path"], "fs#list-files");
            if (!(values[0] is BString path))
                throw new ArgumentTypeException(values[0], "string", 1, "fs#list-files");
            return new BList(Directory.GetFiles(path.Value).Select(CauliflowerInterpreter.CreateValue));
        }
    }
}
