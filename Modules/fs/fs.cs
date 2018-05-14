using System.Linq;
using System.IO;
using System.Collections.Generic;
using Broccoli;
// ReSharper disable InconsistentNaming

#pragma warning disable IDE1006

// ReSharper disable once CheckNamespace
namespace cauliflower.core {
    public static class fs {
        private static Dictionary<string, IValue[]> args = new Dictionary<string, IValue[]> {
            {"path", new IValue[] { (ScalarVar) "path" } }
        };

        public static IValue exists(IValue path) {
            Function.ValidateArgs(1, args["path"], "fs#exists");
            if (!(path is BString pathS))
                throw new ArgumentTypeException(path, "string", 1, "fs#exists");
            return Interpreter.Boolean(File.Exists(pathS.Value) || Directory.Exists(pathS.Value));
        }

        public static IValue file_exists(IValue path) {
            Function.ValidateArgs(1, args["path"], "fs#file-exists");
            if (!(path is BString pathS))
                throw new ArgumentTypeException(path, "string", 1, "fs#file-exists");
            return Interpreter.Boolean(File.Exists(pathS.Value));
        }

        public static IValue directory_exists(IValue path) {
            Function.ValidateArgs(1, args["path"], "fs#directory-exists");
            if (!(path is BString pathS))
                throw new ArgumentTypeException(path, "string", 1, "fs#directory-exists");
            return Interpreter.Boolean(Directory.Exists(pathS.Value));
        }

        public static IValue list_directories(IValue path) {
            Function.ValidateArgs(1, args["path"], "fs#list-directories");
            if (!(path is BString pathS))
                throw new ArgumentTypeException(path, "string", 1, "fs#list-directories");
            return new BList(Directory.GetDirectories(pathS.Value).Select(CauliflowerInterpreter.CreateValue));
        }

        public static IValue list_files(IValue path) {
            Function.ValidateArgs(1, args["path"], "fs#list-files");
            if (!(path is BString pathS))
                throw new ArgumentTypeException(path, "string", 1, "fs#list-files");
            return new BList(Directory.GetFiles(pathS.Value).Select(CauliflowerInterpreter.CreateValue));
        }
    }
}
