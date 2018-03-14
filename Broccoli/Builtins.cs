using System;
using System.Collections.Generic;
using System.Linq;

namespace Broccoli {
    public partial class Broccoli {
        public readonly Dictionary<string, IFunction> Builtins = new Dictionary<string, IFunction> {
            // Meta-commands
            {"quit", new Function("quit", -1, args => {
                Environment.Exit(0);
                return Atom.Nil;
            })},

            // I/O Commands
            { "print", new Function("print", -2, args => {
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
                return null;
            })},

            // Basic Math
            {":=", new ShortCircuitFunction(":=", -3, (broccoli, args) => {
                var toAssign = broccoli.EvaluateExpression(args[1]);
                switch (args[0]) {
                    case ScalarVar s:
                        if (toAssign is ValueList)
                            throw new Exception("Lists cannot be assigned to scalar ($) variables");
                        broccoli.Scope[s] = toAssign;
                        break;
                    case ListVar l:
                        if (!(toAssign is ValueList valueList))
                            throw new Exception("Scalars cannot be assigned to list (@) variables");
                        broccoli.Scope[l] = valueList;
                        break;
                    default:
                        throw new Exception("Values can only be assigned to scalar ($) or list (@) variables");
                }
                return toAssign;
            })},
            {"+", new Function("+", -1, args => {
                foreach (var (value, index) in args.WithIndex())
                    if (!(value is Integer || value is Float))
                        throw new Exception($"Received {value.GetType()} instead of integer or float in argument {index + 1} for '+'");

                return args.Aggregate((IValue) new Integer(0), (m, v) => {
                    if (m is Integer im && v is Integer iv)
                        return new Integer(im.Value + iv.Value);
                    return new Float(((Float) m).Value + ((Float) v).Value);
                });
            })},
            {"*", new Function("*", -1, args => {
                foreach (var (value, index) in args.WithIndex())
                    if (!(value is Integer || value is Float))
                        throw new Exception($"Received {value.GetType()} instead of integer or float in argument {index + 1} for '*'");

                return args.Aggregate((IValue) new Integer(0), (m, v) => {
                    if (m is Integer im && v is Integer iv)
                        return new Integer(im.Value * iv.Value);
                    return new Float(((Float) m).Value * ((Float) v).Value);
                });
            })},
            {"-", new Function("-", -2, args => {
                foreach (var (value, index) in args.WithIndex())
                    if (!(value is Integer || value is Float))
                        throw new Exception($"Received {value.GetType()} instead of integer or float in argument {index + 1} for '-'");

                return args.Skip(1).Aggregate(args[0], (m, v) => {
                    if (m is Integer im && v is Integer iv)
                        return new Integer(im.Value - iv.Value);
                    return new Float(((Float) m).Value - ((Float) v).Value);
                });
            })},
            {"/", new Function("/", -2, args => {
                foreach (var (value, index) in args.WithIndex())
                    if (!(value is Integer || value is Float))
                        throw new Exception($"Received {value.GetType()} instead of integer or float in argument {index + 1} for '/'");

                return args.Aggregate((IValue) new Integer(0), (m, v) => {
                    return new Float(((Float) m).Value / ((Float) v).Value);
                });
            })},
            {"int", new Function("int", -2, args => {
                if (args[0] is Integer)
                    return args[0];
                if (args[0] is Float f)
                    return new Integer((int) f.Value);

                throw new Exception($"Received {args[0].GetType()} instead of integer or float in argument 0 for 'int'");
            })},
            {"float", new Function("float", -2, args => {
                if (args[0] is Float)
                    return args[0];
                if (args[0] is Integer integer)
                    return (Float) integer;

                throw new Exception($"Received {args[0].GetType()} instead of integer or float in argument 0 for 'float'");
            })},

            // Flow control
            {"if", new ShortCircuitFunction("if", -4, (broccoli, args) => {
                // TODO: truthiness
                var condition = broccoli.EvaluateExpression(args[0]);
                if (!(condition is Atom atomCond))
                    throw new Exception($"Received {condition.GetType()} instead of atom in argument 0 for 'if'");
                if (atomCond == Atom.True)
                    broccoli.EvaluateExpression(args[1]);
                else
                    broccoli.EvaluateExpression(args[2]);
                return Atom.Nil;
            })},
            {"for", new ShortCircuitFunction("for", -3, (broccoli, args) => {
                var inValue = broccoli.EvaluateExpression(args[1]);
                if (!(inValue is Atom inAtom))
                    throw new Exception($"Received {inValue.GetType()} instead of atom in argument 1 for 'for'");
                if (inAtom.Value != "in")
                    throw new Exception($"Received atom '{inAtom.Value}' instead of atom 'in' in argument 1 for 'for'");

                var iterable = broccoli.EvaluateExpression(args[2]);
                if (!(iterable is ValueList valueList))
                    throw new Exception($"Received {iterable.GetType()} instead of list in argument 0 for 'for'");

                broccoli.Scope = new BroccoliScope(broccoli.Scope);
                var statements = args.Skip(3).ToList();
                foreach (var value in valueList) {
                    switch (args[0]) {
                        case ScalarVar s:
                            if (value is ValueList)
                                throw new Exception("Lists cannot be assigned to scalar ($) variables");

                            broccoli.Scope[s] = value;
                            break;
                        case ListVar l:
                            if (!(value is ValueList list))
                                throw new Exception("Scalars cannot be assigned to list (@) variables");

                            broccoli.Scope[l] = list;
                            break;
                    }
                    foreach (var statement in statements)
                        broccoli.EvaluateExpression(statement);
                }

                broccoli.Scope = broccoli.Scope.Parent;
                return Atom.Nil;
            })},

            // Conditionals
            {"=", new Function("=", -2, args => {
                return args.Skip(1).All(element => args[0].Equals(element)) ? Atom.True : Atom.Nil;
            })},
            {"/=", new Function("=", -2, args => {
                return args.Skip(1).All(element => !args[0].Equals(element)) ? Atom.True : Atom.Nil;
            })},

            // List commands
            {"list", new Function("list", -1, args => new ValueList(args.ToList()))},
            {"len", new Function("len", -2, args => {
                if (!(args[0] is ValueList list))
                    throw new Exception($"Received {args[0].GetType()} instead of list in argument 0 for 'len'");

                return new Integer(list.Count);
            })},
            {"first", new Function("first", -2, args => {
                if (!(args[0] is ValueList list))
                    throw new Exception($"Received {args[0].GetType()} instead of list in argument 0 for 'first'");

                return new ValueList(new []{list[0]});
            })},
            {"rest", new Function("rest", -2, args => {
                if (!(args[0] is ValueList list))
                    throw new Exception($"Received {args[0].GetType()} instead of list in argument 0 for 'rest'");

                return new ValueList(list.Skip(1).ToList());
            })},
            {"slice", new Function("slice", -4, args => {
                if (!(args[0] is ValueList list))
                    throw new Exception($"Received {args[0].GetType()} instead of list in argument 0 for 'slice'");
                if (!(args[1] is Integer i1))
                    throw new Exception($"Received {args[0].GetType()} instead of integer in argument 1 for 'slice'");
                if (!(args[2] is Integer i2))
                    throw new Exception($"Received {args[0].GetType()} instead of integer in argument 2 for 'slice'");

                var start = (int) i1.Value;
                var end = (int) i2.Value;
                return new ValueList(list.Skip(start).Take(end - start).ToList());
            })},
            {"range", new Function("range", -3, args => {
                if (!(args[0] is Integer i1))
                    throw new Exception($"Received {args[0].GetType()} instead of integer in argument 0 for 'range'");
                if (!(args[1] is Integer i2))
                    throw new Exception($"Received {args[0].GetType()} instead of integer in argument 1 for 'range'");

                var start = (int) i1.Value;
                var end = (int) i2.Value;
                return new ValueList(Enumerable.Range(start, end - start + 1).Select(value => (IValue) new Integer(value)).ToList());
            })},
            {"cat", new Function("cat", -3, args => {
                var result = new ValueList();
                foreach (var value in args) {
                    if (!(value is ValueList list))
                        throw new Exception($"Received {value.GetType()} instead of list for 'cat'");
                    result.AddRange(list);
                }
                return result;
            })},
            {"help", new ShortCircuitFunction("help", -1, (broccoli, args) => {
                Console.WriteLine('(' + string.Join(' ', broccoli.Builtins.Keys) + ')');
                return null;
            })},
        };
    }
}