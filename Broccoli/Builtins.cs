using System;
using System.Collections.Generic;
using System.Linq;

namespace Broccoli {
    public partial class Broccoli {
        public readonly Dictionary<string, Function> Functions = new Dictionary<string, Function> {
            {"+", new Function("+", -1, args => {
                foreach (var (value, index) in args.WithIndex()) {
                    if (!(value is Integer || value is Float))
                        throw new Exception($"Recieved {value.GetType()} instead of integer or float in argument {index + 1} for +");
                }

                return args.Aggregate((IValue) new Integer(0), (m, v) => {
                    if (m is Integer && v is Integer)
                        return new Integer(((Integer) m).Value + ((Integer) v).Value);

                    return new Float(((Float) m).Value + ((Float) v).Value);
                });
            })},
            {"list", new Function("list", -1, args => new ValueList(args.ToList()))},
            {"print", new Function("print", -2, args => {
                foreach (var value in args) {
                    string print;
                    var atom = value as Atom?;
                    if (atom != null) {
                        switch (atom.Value.Value) {
                            case "tab":
                                print = "\t";
                                break;
                            case "endl":
                                print = "\n";
                                break;
                            default:
                                print = atom.ToString();
                                break;
                        }
                    } else {
                        print = value.ToString();
                    }
                    Console.Write(print);
                }

                return Atom.Nil;
            })}
        };
    }
}