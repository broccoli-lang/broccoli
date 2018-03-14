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
                        if (!(toAssign is ValueList))
                            throw new Exception("Scalars cannot be assigned to list (@) variables");
                        broccoli.Scope[l] = (ValueList) toAssign;
                        break;
                    default:
                        throw new Exception("Values can only be assigned to scalar ($) or list (@) variables");
                }
                return toAssign;
            })},
            {"+", new Function("+", -1, args => {
                foreach (var (value, index) in args.WithIndex())
                    if (!(value is Integer || value is Float))
                        throw new Exception($"Recieved {value.GetType()} instead of integer or float in argument {index + 1} for '+'");
                return args.Aggregate((IValue) new Integer(0), (m, v) => {
                    if (m is Integer && v is Integer)
                        return new Integer(((Integer) m).Value + ((Integer) v).Value);
                    return new Float(((Float) m).Value + ((Float) v).Value);
                });
            })},
            {"*", new Function("*", -1, args => {
                foreach (var (value, index) in args.WithIndex())
                    if (!(value is Integer || value is Float))
                        throw new Exception($"Recieved {value.GetType()} instead of integer or float in argument {index + 1} for '*'");
                return args.Aggregate((IValue) new Integer(0), (m, v) => {
                    if (m is Integer && v is Integer)
                        return new Integer(((Integer) m).Value * ((Integer) v).Value);
                    return new Float(((Float) m).Value * ((Float) v).Value);
                });
            })},
            {"-", new Function("-", -2, args => {
                foreach (var (value, index) in args.WithIndex())
                    if (!(value is Integer || value is Float))
                        throw new Exception($"Recieved {value.GetType()} instead of integer or float in argument {index + 1} for '-'");
                return args.Skip(1).Aggregate(args[0], (m, v) => {
                    if (m is Integer && v is Integer)
                        return new Integer(((Integer) m).Value - ((Integer) v).Value);
                    return new Float(((Float) m).Value - ((Float) v).Value);
                });
            })},
            {"/", new Function("/", -2, args => {
                foreach (var (value, index) in args.WithIndex())
                    if (!(value is Integer || value is Float))
                        throw new Exception($"Recieved {value.GetType()} instead of integer or float in argument {index + 1} for '/'");
                return args.Aggregate((IValue) new Integer(0), (m, v) => {
                    return new Float(((Float) m).Value / ((Float) v).Value);
                });
            })},
            {"int", new Function("int", -2, args => {
                if (args[0] is Integer)
                    return args[0];
                if (args[0] is Float)
                    return new Integer((int) ((Float) args[0]).Value);
                throw new Exception($"Recieved {args[0].GetType()} instead of integer or float in argument 0 for 'int'");
            })},
            {"float", new Function("float", -2, args => {
                if (args[0] is Float)
                    return args[0];
                if (args[0] is Integer)
                    return new Float((float) ((Integer) args[0]).Value);
                throw new Exception($"Recieved {args[0].GetType()} instead of integer or float in argument 0 for 'float'");
            })},

            // Flow control
            {"if", new ShortCircuitFunction("if", -4, (broccoli, args) => {
                // TODO: truthiness
                var condition = broccoli.EvaluateExpression(args[0]);
                if (!(condition is Atom))
                    throw new Exception($"Recieved {condition.GetType()} instead of atom in argument 0 for 'if'");
                if (((Atom) condition) == Atom.True)
                    broccoli.EvaluateExpression(args[1]);
                else
                    broccoli.EvaluateExpression(args[2]);
                return Atom.Nil;
            })},
            {"for", new ShortCircuitFunction("for", -3, (broccoli, args) => {
                var inAtom = broccoli.EvaluateExpression(args[1]);
                if (!(inAtom is Atom))
                    throw new Exception($"Recived {inAtom.GetType()} instead of atom in argument 1 for 'for'");
                if (((Atom) inAtom).Value != "in")
                    throw new Exception($"Recived atom '{((Atom) inAtom).Value}' instead of atom 'in' in argument 1 for 'for'");
                var iterable = broccoli.EvaluateExpression(args[2]);
                if (!(iterable is ValueList))
                    throw new Exception($"Recieved {iterable.GetType()} instead of list in argument 0 for 'for'");
                broccoli.Scope = new BroccoliScope(broccoli.Scope);
                var statements = args.Skip(3).ToList();
                foreach (var value in (ValueList) iterable) {
                    switch (args[0]) {
                        case ScalarVar s:
                            if (value is ValueList)
                                throw new Exception("Lists cannot be assigned to scalar ($) variables");
                            broccoli.Scope.Set(s, value, self: true);
                            break;
                        case ListVar l:
                            if (!(value is ValueList))
                                throw new Exception("Scalars cannot be assigned to list (@) variables");
                            broccoli.Scope.Set(l, (ValueList) value, self: true);
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
                var value = args[0];
                return args.Skip(1).All(element => args[0].Equals(element)) ? Atom.True : Atom.Nil;
            })},
            {"/=", new Function("=", -2, args => {
                var value = args[0];
                return args.Skip(1).All(element => !args[0].Equals(element)) ? Atom.True : Atom.Nil;
            })},

            // List commands
            {"list", new Function("list", -1, args => new ValueList(args.ToList()))},
            {"len", new Function("len", -2, args => {
                if (!(args[0] is ValueList))
                    throw new Exception($"Recieved {args[0].GetType()} instead of list in argument 0 for 'len'");
                return new Integer(((ValueList) args[0]).Count);
            })},
            {"first", new Function("first", -2, args => {
                if (!(args[0] is ValueList))
                    throw new Exception($"Recieved {args[0].GetType()} instead of list in argument 0 for 'first'");
                return new ValueList(new []{((ValueList)args[0])[0]});
            })},
            {"rest", new Function("rest", -2, args => {
                if (!(args[0] is ValueList))
                    throw new Exception($"Recieved {args[0].GetType()} instead of list in argument 0 for 'rest'");
                return new ValueList(((ValueList)args[0]).Skip(1).ToList());
            })},
            {"slice", new Function("slice", -4, args => {
                if (!(args[0] is ValueList))
                    throw new Exception($"Recieved {args[0].GetType()} instead of list in argument 0 for 'slice'");
                if (!(args[1] is Integer))
                    throw new Exception($"Recieved {args[0].GetType()} instead of integer in argument 1 for 'slice'");
                if (!(args[2] is Integer))
                    throw new Exception($"Recieved {args[0].GetType()} instead of integer in argument 2 for 'slice'");
                var start = (int) ((Integer) args[1]).Value;
                var end = (int) ((Integer) args[2]).Value;
                return new ValueList(((ValueList)args[0]).Skip(start).Take(end - start).ToList());
            })},
            {"range", new Function("range", -3, args => {
                if (!(args[0] is Integer))
                    throw new Exception($"Recieved {args[0].GetType()} instead of integer in argument 0 for 'range'");
                if (!(args[1] is Integer))
                    throw new Exception($"Recieved {args[0].GetType()} instead of integer in argument 1 for 'range'");
                var start = (int) ((Integer) args[0]).Value;
                var end = (int) ((Integer) args[1]).Value;
                return new ValueList(Enumerable.Range(start, end - start + 1).Select(value => (IValue) new Integer(value)).ToList());
            })},
            {"cat", new Function("cat", -3, args => {
                var result = new ValueList();
                foreach (var value in args) {
                    if (!(value is ValueList))
                        throw new Exception($"Recieved {value.GetType()} instead of list for 'cat'");
                    result.AddRange((ValueList) value);
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