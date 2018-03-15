using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;

namespace Broccoli {
    public partial class Broccoli {
        private static string TypeName(object o) => o.GetType().ToString().Split('.').Last().ToLower();

        private static Atom Boolean(bool b) => b ? Atom.True : Atom.Nil;

        public static readonly Dictionary<string, IFunction> DefaultBuiltins = new Dictionary<string, IFunction> {
            // Core Language Features
            {"fn", new ShortCircuitFunction("fn", -3, (broccoli, args) => {
                if (!(broccoli.EvaluateExpression(args[0]) is Atom name))
                    throw new Exception($"Received {TypeName(args[0])} instead of atom in argument 1 for 'fn'");
                if (!(args[1] is ValueExpression argExpressions))
                    throw new Exception($"Received {TypeName(args[1])} instead of expression in argument 2 for 'fn'");
                var argNames = argExpressions.Values.ToArray();
                var statements = args.Skip(2);
                IValue result = null;
                broccoli.Scope.Functions[name.Value] = new Function(name.Value, argNames.Length, innerArgs => {
                    broccoli.Scope = new BroccoliScope(broccoli.Scope);
                    for (int i = 0; i < innerArgs.Length; i++) {
                        var toAssign = innerArgs[i];
                        switch (argNames[i]) {
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
                    }
                    foreach (var statement in statements.Take(statements.Count() - 1))
                        broccoli.EvaluateExpression(statement);
                    if (statements.Count() != 0)
                        result = broccoli.EvaluateExpression(statements.Last());
                    broccoli.Scope = broccoli.Scope.Parent;
                    return result;
                });
                return null;
            })},
            {"env", new ShortCircuitFunction("env", 1, (broccoli, args) => {
                if (!(args[0] is Atom a))
                    throw new Exception($"Received {TypeName(args[1])} instead of atom in argument 1 for 'env'");
                if (a.Value == "broccoli")
                    broccoli.Builtins = DefaultBuiltins;
                else if (AlternativeEnvironments.ContainsKey(a.Value))
                        broccoli.Builtins = AlternativeEnvironments[a.Value];
                else
                    throw new Exception($"Environment {a.Value} not found");
                return null;
            })},
            
            // Meta-commands
            {"quit", new Function("quit", 0, args => {
                Environment.Exit(0);
                return null;
            })},
            {"help", new ShortCircuitFunction("help", 0, (broccoli, args) => {
                Console.WriteLine('(' + string.Join(' ', broccoli.Builtins.Keys) + ')');
                return null;
            })},
            {"clear", new ShortCircuitFunction("clear", 0, (broccoli, args) => {
                broccoli.Scope.Scalars.Clear();
                broccoli.Scope.Lists.Clear();
                broccoli.Scope.Functions.Clear();
                return null;
            })},
            {"reset", new ShortCircuitFunction("reset", 0, (broccoli, args) => {
                broccoli.Scope.Scalars.Clear();
                broccoli.Scope.Lists.Clear();
                return null;
            })},
            {"bench", new ShortCircuitFunction("bench", -2, (broccoli, args) => {
                var start = new DateTime();
                foreach (var expression in args)
                    broccoli.EvaluateExpression(expression);
                return new Float((DateTime.Now - start).TotalSeconds);
            })},
            {"eval", new ShortCircuitFunction("eval", 1, (broccoli, args) => {
                if (!(broccoli.EvaluateExpression(args[0]) is String s))
                    throw new Exception($"Received {TypeName(args[0])} instead of string in argument 0 for 'eval'");
                return broccoli.Run(s.Value);
            })},
            {"call", new ShortCircuitFunction("call", -2, (broccoli, args) => {
                if (!(broccoli.EvaluateExpression(args[0]) is Atom a))
                    throw new Exception($"Received {TypeName(args[0])} instead of atom in argument 0 for 'call'");
                return (broccoli.Scope[a.Value] ?? broccoli.Builtins[a.Value]).Invoke(broccoli, args.Skip(1).ToArray());
            })},
            {"run", new ShortCircuitFunction("run", 1, (broccoli, args) => {
                if (!(broccoli.EvaluateExpression(args[0]) is String s))
                    throw new Exception($"Received {TypeName(args[0])} instead of string in argument 0 for 'run'");
                return broccoli.Run(File.ReadAllText(s.Value));
            })},
            {"import", new ShortCircuitFunction("import", 1, (broccoli, args) => {
                if (!(broccoli.EvaluateExpression(args[0]) is String s))
                    throw new Exception($"Received {TypeName(args[0])} instead of string in argument 0 for 'import'");
                var tempBroccoli = new Broccoli();
                tempBroccoli.Run(File.ReadAllText(s.Value));
                foreach (var (key, value) in tempBroccoli.Scope.Functions)
                    broccoli.Scope.Set(key, value, true);
                return null;
            })},

            // I/O Commands
            {"print", new Function("print", -2, args => {
                foreach (var value in args) {
                    string print = null;
                    if (value is Atom atom)
                        switch (atom.Value) {
                            case "tab":
                                print = "\t";
                                break;
                            case "endl":
                                print = "\n";
                                break;
                        }
                    Console.Write(print ?? value.ToString());
                }
                return null;
            })},

            // Basic Math
            {"+", new Function("+", -1, args => {
                foreach (var (value, index) in args.WithIndex())
                    if (!(value is Integer || value is Float))
                        throw new Exception($"Received {TypeName(value)} instead of integer or float in argument {index + 1} for '+'");

                return args.Aggregate((IValue) new Integer(0), (m, v) => {
                    if (m is Integer im && v is Integer iv)
                        return new Integer(im.Value + iv.Value);
                    double fm, fv;
                    if (m is Integer mValue)
                        fm = mValue.Value;
                    else
                        fm = ((Float) m).Value;
                    if (v is Integer vValue)
                        fv = vValue.Value;
                    else
                        fv = ((Float) v).Value;
                    return new Float(fm + fv);
                });
            })},
            {"*", new Function("*", -1, args => {
                foreach (var (value, index) in args.WithIndex())
                    if (!(value is Integer || value is Float))
                        throw new Exception($"Received {TypeName(value)} instead of integer or float in argument {index + 1} for '*'");

                return args.Aggregate((IValue) new Integer(1), (m, v) => {
                    if (m is Integer im && v is Integer iv)
                        return new Integer(im.Value * iv.Value);
                    double fm, fv;
                    if (m is Integer mValue)
                        fm = mValue.Value;
                    else
                        fm = ((Float) m).Value;
                    if (v is Integer vValue)
                        fv = vValue.Value;
                    else
                        fv = ((Float) v).Value;
                    return new Float(fm * fv);
                });
            })},
            {"-", new Function("-", -2, args => {
                foreach (var (value, index) in args.WithIndex())
                    if (!(value is Integer || value is Float))
                        throw new Exception($"Received {TypeName(value)} instead of integer or float in argument {index + 1} for '-'");

                return args.Skip(1).Aggregate(args[0], (m, v) => {
                    if (m is Integer im && v is Integer iv)
                        return new Integer(im.Value - iv.Value);
                    double fm, fv;
                    if (m is Integer mValue)
                        fm = mValue.Value;
                    else
                        fm = ((Float) m).Value;
                    if (v is Integer vValue)
                        fv = vValue.Value;
                    else
                        fv = ((Float) v).Value;
                    return new Float(fm - fv);
                });
            })},
            {"/", new Function("/", -2, args => {
                foreach (var (value, index) in args.WithIndex())
                    if (!(value is Integer || value is Float))
                        throw new Exception($"Received {TypeName(value)} instead of integer or float in argument {index + 1} for '/'");

                return args.Skip(1).Aggregate(args[0], (m, v) => {
                    double fm, fv;
                    if (m is Integer mValue)
                        fm = mValue.Value;
                    else
                        fm = ((Float) m).Value;
                    if (v is Integer vValue)
                        fv = vValue.Value;
                    else
                        fv = ((Float) v).Value;
                    return new Float(fm / fv);
                });
            })},
            {":=", new ShortCircuitFunction(":=", 2, (broccoli, args) => {
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
            {"int", new Function("int", 1, args => {
                if (args[0] is Integer)
                    return args[0];
                if (args[0] is Float f)
                    return new Integer((int) f.Value);

                throw new Exception($"Received {TypeName(args[0])} instead of integer or float in argument 1 for 'int'");
            })},
            {"float", new Function("float", 1, args => {
                if (args[0] is Float)
                    return args[0];
                if (args[0] is Integer integer)
                    return (Float) integer;

                throw new Exception($"Received {TypeName(args[0])} instead of integer or float in argument 1 for 'float'");
            })},

            // Comparison
            {"=", new Function("=", -2, args => {
                return Boolean(args.Skip(1).All(element => args[0].Equals(element)));
            })},
            {"/=", new Function("/=", -2, args => {
                if (args.Length == 1)
                    return Atom.Nil;
                return Boolean(args.Skip(1).All(element => !args[0].Equals(element)));
            })},
            // TODO: make this shorter
            {"<", new Function("<", -3, args => {
                foreach (var (value, index) in args.WithIndex())
                    if (!(value is Integer || value is Float))
                        throw new Exception($"Received {TypeName(value)} instead of integer or float in argument {index + 1} for '<'");
                var current = args[0];
                var rest = args.Skip(1);
                foreach (var next in rest) {
                    switch (current) {
                        case Integer i:
                            switch (next) {
                                case Integer i2:
                                    if (i.Value < i2.Value)
                                        current = next;
                                    else
                                        return Atom.Nil;
                                    break;
                                case Float f:
                                    if (i.Value < f.Value)
                                        current = next;
                                    else
                                        return Atom.Nil;
                                    break;
                            }
                            break;
                        case Float f:
                            switch (next) {
                                case Integer i:
                                    if (f.Value < i.Value)
                                        current = next;
                                    else
                                        return Atom.Nil;
                                    break;
                                case Float f2:
                                    if (f.Value < f2.Value)
                                        current = next;
                                    else
                                        return Atom.Nil;
                                    break;
                            }
                            break;
                    }
                }
                return Atom.True;
            })},
            {">", new Function(">", -3, args => {
                foreach (var (value, index) in args.WithIndex())
                    if (!(value is Integer || value is Float))
                        throw new Exception($"Received {TypeName(value)} instead of integer or float in argument {index + 1} for '>'");
                var current = args[0];
                var rest = args.Skip(1);
                foreach (var next in rest) {
                    switch (current) {
                        case Integer i:
                            switch (next) {
                                case Integer i2:
                                    if (i.Value > i2.Value)
                                        current = next;
                                    else
                                        return Atom.Nil;
                                    break;
                                case Float f:
                                    if (i.Value > f.Value)
                                        current = next;
                                    else
                                        return Atom.Nil;
                                    break;
                            }
                            break;
                        case Float f:
                            switch (next) {
                                case Integer i:
                                    if (f.Value > i.Value)
                                        current = next;
                                    else
                                        return Atom.Nil;
                                    break;
                                case Float f2:
                                    if (f.Value > f2.Value)
                                        current = next;
                                    else
                                        return Atom.Nil;
                                    break;
                            }
                            break;
                    }
                }
                return Atom.True;
            })},
            {"<=", new Function("<=", -3, args => {
                foreach (var (value, index) in args.WithIndex())
                    if (!(value is Integer || value is Float))
                        throw new Exception($"Received {TypeName(value)} instead of integer or float in argument {index + 1} for '<='");
                var current = args[0];
                var rest = args.Skip(1);
                foreach (var next in rest) {
                    switch (current) {
                        case Integer i:
                            switch (next) {
                                case Integer i2:
                                    if (i.Value <= i2.Value)
                                        current = next;
                                    else
                                        return Atom.Nil;
                                    break;
                                case Float f:
                                    if (i.Value <= f.Value)
                                        current = next;
                                    else
                                        return Atom.Nil;
                                    break;
                            }
                            break;
                        case Float f:
                            switch (next) {
                                case Integer i:
                                    if (f.Value <= i.Value)
                                        current = next;
                                    else
                                        return Atom.Nil;
                                    break;
                                case Float f2:
                                    if (f.Value <= f2.Value)
                                        current = next;
                                    else
                                        return Atom.Nil;
                                    break;
                            }
                            break;
                    }
                }
                return Atom.True;
            })},
            {">=", new Function(">=", -3, args => {
                foreach (var (value, index) in args.WithIndex())
                    if (!(value is Integer || value is Float))
                        throw new Exception($"Received {TypeName(value)} instead of integer or float in argument {index + 1} for '>='");
                if (args.Length == 1)
                    return Atom.Nil;
                var current = args[0];
                var rest = args.Skip(1);
                foreach (var next in rest) {
                    switch (current) {
                        case Integer i:
                            switch (next) {
                                case Integer i2:
                                    if (i.Value >= i2.Value)
                                        current = next;
                                    else
                                        return Atom.Nil;
                                    break;
                                case Float f:
                                    if (i.Value >= f.Value)
                                        current = next;
                                    else
                                        return Atom.Nil;
                                    break;
                            }
                            break;
                        case Float f:
                            switch (next) {
                                case Integer i:
                                    if (f.Value >= i.Value)
                                        current = next;
                                    else
                                        return Atom.Nil;
                                    break;
                                case Float f2:
                                    if (f.Value >= f2.Value)
                                        current = next;
                                    else
                                        return Atom.Nil;
                                    break;
                            }
                            break;
                    }
                }
                return Atom.True;
            })},

            // Logic
            {"not", new Function("not", 1, args => Boolean(args[0].Equals(Atom.Nil)))},
            {"and", new Function("and", -1, args => Boolean(args.All(arg => !arg.Equals(Atom.Nil))))},
            {"or", new Function("or", -1, args => Boolean(args.Any(arg => !arg.Equals(Atom.Nil))))},

            // Flow control
            {"if", new ShortCircuitFunction("if", -3, (broccoli, args) => {
                var condition = broccoli.EvaluateExpression(args[0]);
                if (!condition.Equals(Atom.True) && !condition.Equals(Atom.Nil))
                    throw new Exception($"Received {TypeName(args[0])} '{args[0].ToString()}' instead of boolean in argument 1 for 'if'");
                var elseIndex = Array.IndexOf(args.ToArray(), new Atom("else"), 1);
                if (elseIndex != -1) {
                    var expression = new ValueExpression(
                        condition.Equals(Atom.True) ?
                        args.Skip(1).Take(elseIndex - 1) :
                        args.Skip(elseIndex + 1)
                    );
                    return broccoli.EvaluateExpression(expression.Values.Length == 1 ? expression.Values[0] : expression);
                } else if (condition.Equals(Atom.True)) {
                    if (args.Length == 2)
                        return broccoli.EvaluateExpression(args[1]);
                    return broccoli.EvaluateExpression(new ValueExpression(args.Skip(1)));
                }
                return null;
            })},
            {"for", new ShortCircuitFunction("for", -3, (broccoli, args) => {
                var inValue = broccoli.EvaluateExpression(args[1]);
                if (!(inValue is Atom inAtom))
                    throw new Exception($"Received {TypeName(inValue)} instead of atom 'in' in argument 2 for 'for'");
                if (inAtom.Value != "in")
                    throw new Exception($"Received atom '{inAtom.Value}' instead of atom 'in' in argument 2 for 'for'");

                var iterable = broccoli.EvaluateExpression(args[2]);
                if (!(iterable is ValueList valueList))
                    throw new Exception($"Received {TypeName(iterable)} instead of list in argument 1 for 'for'");

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
                return null;
            })},

            // List Functions
            {"list", new Function("list", -1, args => new ValueList(args.ToList()))},
            {"len", new Function("len", 1, args => {
                if (args[0] is ValueList l)
                    return new Integer(l.Count);
                if (args[0] is String s)
                    return new Integer(s.Value.Length);

                throw new Exception($"Received {TypeName(args[0])} instead of list or string in argument 1 for 'len'");
            })},
            {"first", new Function("first", 1, args => {
                if (!(args[0] is ValueList list))
                    throw new Exception($"Received {TypeName(args[0])} instead of list in argument 1 for 'first'");

                return list.Count == 0 ? Atom.Nil : list[0];
            })},
            {"rest", new Function("rest", 1, args => {
                if (!(args[0] is ValueList list))
                    throw new Exception($"Received {TypeName(args[0])} instead of list in argument 1 for 'rest'");

                return new ValueList(list.Skip(1).ToList());
            })},
            {"slice", new Function("slice", -4, args => {
                if (!(args[0] is ValueList list))
                    throw new Exception($"Received {TypeName(args[0])} instead of list in argument 1 for 'slice'");
                if (!(args[1] is Integer i1))
                    throw new Exception($"Received {TypeName(args[0])} instead of integer in argument 2 for 'slice'");
                if (!(args[2] is Integer i2))
                    throw new Exception($"Received {TypeName(args[0])} instead of integer in argument 3 for 'slice'");

                var start = (int) i1.Value;
                var end = (int) i2.Value;
                return new ValueList(list.Skip(start).Take(end - Math.Max(0, start)).ToList());
            })},
            {"range", new Function("range", 2, args => {
                if (!(args[0] is Integer i1))
                    throw new Exception($"Received {TypeName(args[0])} instead of integer in argument 1 for 'range'");
                if (!(args[1] is Integer i2))
                    throw new Exception($"Received {TypeName(args[0])} instead of integer in argument 2 for 'range'");

                var start = (int) i1.Value;
                var end = (int) i2.Value;
                return new ValueList(Enumerable.Range(start, end - start + 1).Select(value => (IValue) new Integer(value)).ToList());
            })},
            {"cat", new Function("cat", -3, args => {
                foreach (var (value, index) in args.WithIndex())
                    if (!(value is ValueList))
                        throw new Exception($"Received {TypeName(args[0])} instead of list in argument {index + 1} for 'cat'");
                var result = new ValueList();
                foreach (ValueList list in args)
                    result.AddRange(list);
                return result;
            })},
        };

        public static readonly Dictionary<string, Dictionary<string, IFunction>> AlternativeEnvironments = new Dictionary<string, Dictionary<string, IFunction>> {
            {"cauliflower",  new Dictionary<string, IFunction> {
                {"and", new ShortCircuitFunction("and", -1, (broccoli, args) => {
                    if (args.Any(arg => broccoli.EvaluateExpression(arg).Equals(Atom.Nil))) {
                        return Atom.Nil;
                    }

                    return Atom.True;
                })},
                {"or", new ShortCircuitFunction("or", -1, (broccoli, args) => {
                    if (args.Any(arg => !broccoli.EvaluateExpression(arg).Equals(Atom.Nil))) {
                        return Atom.True;
                    }

                    return Atom.Nil;
                })},
                {"lam", new ShortCircuitFunction("lam", -2, (broccoli, args) => {
                    if (!(args[1] is ValueExpression argExpressions))
                        throw new Exception($"Received {TypeName(args[1])} instead of expression in argument 1 for 'lam'");
                    var argNames = argExpressions.Values.ToArray();
                    var statements = args.Skip(2);
                    IValue result = null;
                    return new AnonymousFunction(argNames.Length, innerArgs => {
                        broccoli.Scope = new BroccoliScope(broccoli.Scope);
                        for (int i = 0; i < innerArgs.Length; i++) {
                            var toAssign = innerArgs[i];
                            switch (argNames[i]) {
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
                        }
                        foreach (var statement in statements.Take(statements.Count() - 1))
                            broccoli.EvaluateExpression(statement);
                        if (statements.Count() != 0)
                            result = broccoli.EvaluateExpression(statements.Last());
                        broccoli.Scope = broccoli.Scope.Parent;
                        return result;
                    });
                })}
            }.Extend(DefaultBuiltins)}
        };
    }

    static class DictionaryExtensions {
        public static Dictionary<K, V> Extend<K, V>(this Dictionary<K, V> self, Dictionary<K, V> other) {
            foreach (var (key, value) in other)
                if (!self.ContainsKey(key))
                    self[key] = value;
            return self;
        }
    }
}