using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Runtime.CompilerServices;

namespace Broccoli {
    public partial class Broccoli {
        private static string TypeName(object o) => o.GetType().ToString().Split('.').Last().ToLower();

        private static Atom Boolean(bool b) => b ? Atom.True : Atom.Nil;

        public static readonly Dictionary<string, IFunction> DefaultBuiltins = new Dictionary<string, IFunction> {
            // Core Language Features
            {"", new ShortCircuitFunction("", 1, (broccoli, args) => {
                var e = (ValueExpression) args[0];
                if (e.IsValue)
                    return broccoli.Run(e.Values.First());
                var first = e.Values.First();
                IFunction fn = null;
                if (!(first is Atom a))
                    throw new Exception($"Function name {first} must be an identifier");

                var fnName = a.Value;
                fn = broccoli.Scope[fnName] ?? broccoli.Builtins.GetValueOrDefault(fnName, null);
                if (fn == null)
                    throw new Exception($"Function {fnName} does not exist");

                return fn.Invoke(broccoli, e.Values.Skip(1).ToArray());
            })},
            {"fn", new ShortCircuitFunction("fn", -3, (broccoli, args) => {
                if (!(broccoli.Run(args[0]) is Atom name))
                    throw new Exception($"Received {TypeName(args[0])} instead of atom in argument 1 for 'fn'");
                if (!(args[1] is ValueExpression argExpressions))
                    throw new Exception($"Received {TypeName(args[1])} instead of expression in argument 2 for 'fn'");
                var argNames = argExpressions.Values.ToArray();
                var statements = args.Skip(2);
                broccoli.Scope.Functions[name.Value] = new Function(name.Value, argNames.Length, (_, innerArgs) => {
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
                    IValue result = null;
                    foreach (var statement in statements)
                        result = broccoli.Run(statement);
                    broccoli.Scope = broccoli.Scope.Parent;
                    return result;
                });
                return null;
            })},
            {"env", new Function("env", 1, (broccoli, args) => {
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
            // Note: Many of these technically short-circuit because they have no args
            {"quit", new ShortCircuitFunction("quit", 0, (broccoli, args) => {
                Environment.Exit(0);
                return null;
            })},
            {"help", new ShortCircuitFunction("help", 0, (broccoli, args) => {
                return new ValueList(broccoli.Builtins.Keys.Where(key => !new []{ "", "fn", "env" }.Contains(key)).Select(key => (IValue) new String(key)));
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
                    broccoli.Run(expression);
                return new Float((DateTime.Now - start).TotalSeconds);
            })},
            {"eval", new Function("eval", 1, (broccoli, args) => {
                if (!(args[0] is String s))
                    throw new Exception($"Received {TypeName(args[0])} instead of string in argument 0 for 'eval'");
                return broccoli.Run(s.Value);
            })},
            {"call", new ShortCircuitFunction("call", -2, (broccoli, args) => {
                IFunction fn = null;
                var value = broccoli.Run(args[0]);
                if (!(broccoli.Run(value) is Atom a))
                    throw new Exception($"Received {TypeName(args[0])} instead of atom in argument 0 for 'call'");

                var fnName = a.Value;
                fn = broccoli.Scope[fnName] ?? broccoli.Builtins.GetValueOrDefault(fnName, null);
                if (fn == null)
                    throw new Exception($"Function {fnName} does not exist");

                return fn.Invoke(broccoli, args.Skip(1).ToArray());
            })},
            {"run", new Function("run", 1, (broccoli, args) => {
                if (!(args[0] is String s))
                    throw new Exception($"Received {TypeName(args[0])} instead of string in argument 0 for 'run'");
                return broccoli.Run(File.ReadAllText(s.Value));
            })},
            {"import", new Function("import", 1, (broccoli, args) => {
                if (!(args[0] is String s))
                    throw new Exception($"Received {TypeName(args[0])} instead of string in argument 0 for 'import'");
                var tempBroccoli = new Broccoli();
                tempBroccoli.Run(File.ReadAllText(s.Value));
                foreach (var (key, value) in tempBroccoli.Scope.Functions)
                    broccoli.Scope[key] = value;
                return null;
            })},

            // I/O Commands
            {"print", new Function("print", -2, (broccoli, args) => {
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
            {"+", new Function("+", -1, (broccoli, args) => {
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
            {"*", new Function("*", -1, (broccoli, args) => {
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
            {"-", new Function("-", -2, (broccoli, args) => {
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
            {"/", new Function("/", -2, (broccoli, args) => {
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
                var toAssign = broccoli.Run(args[1]);
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
            {"int", new Function("int", 1, (broccoli, args) => {
                switch (args[0]) {
                    case Integer i:
                        return i;
                    case Float f:
                        return new Integer((int) f.Value);
                    default:
                        throw new Exception($"Received {TypeName(args[0])} instead of integer or float in argument 1 for 'int'");
                }
            })},
            {"float", new Function("float", 1, (broccoli, args) => {
                switch (args[0]) {
                    case Integer i:
                        return new Float(i.Value);
                    case Float f:
                        return f;
                    default:
                        throw new Exception($"Received {TypeName(args[0])} instead of integer or float in argument 1 for 'float'");
                }
            })},

            // Comparison
            {"=", new Function("=", -2, (broccoli, args) => {
                return Boolean(args.Skip(1).All(element => args[0].Equals(element)));
            })},
            {"/=", new Function("/=", -2, (broccoli, args) => {
                if (args.Length == 1)
                    return Atom.Nil;
                return Boolean(args.Skip(1).All(element => !args[0].Equals(element)));
            })},
            // TODO: make this shorter
            {"<", new Function("<", -3, (broccoli, args) => {
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
            {">", new Function(">", -3, (broccoli, args) => {
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
            {"<=", new Function("<=", -3, (broccoli, args) => {
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
            {">=", new Function(">=", -3, (broccoli, args) => {
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
            {"not", new Function("not", 1, (broccoli, args) => Boolean(args[0].Equals(Atom.Nil)))},
            {"and", new Function("and", -1, (broccoli, args) => Boolean(args.All(arg => !arg.Equals(Atom.Nil))))},
            {"or", new Function("or", -1, (broccoli, args) => Boolean(args.Any(arg => !arg.Equals(Atom.Nil))))},

            // Flow control
            {"if", new ShortCircuitFunction("if", -3, (broccoli, args) => {
                var condition = broccoli.Run(args[0]);
                if (!condition.Equals(Atom.True) && !condition.Equals(Atom.Nil))
                    throw new Exception($"Received {TypeName(args[0])} '{args[0].ToString()}' instead of boolean in argument 1 for 'if'");
                var elseIndex = Array.IndexOf(args.ToArray(), new Atom("else"), 1);
                IEnumerable<IValueExpressible> statements = elseIndex != -1 ?
                    condition.Equals(Atom.True) ?
                        args.Skip(1).Take(elseIndex - 1) :
                        args.Skip(elseIndex + 1) :
                    condition.Equals(Atom.True) ?
                        args.Skip(1) :
                        args.Skip(args.Length);
                IValue result = null;
                foreach (var statement in statements)
                    result = broccoli.Run(statement);
                return result;
            })},
            {"for", new ShortCircuitFunction("for", -3, (broccoli, args) => {
                var inValue = broccoli.Run(args[1]);
                if (!(inValue is Atom inAtom))
                    throw new Exception($"Received {TypeName(inValue)} instead of atom 'in' in argument 2 for 'for'");
                if (inAtom.Value != "in")
                    throw new Exception($"Received atom '{inAtom.Value}' instead of atom 'in' in argument 2 for 'for'");

                var iterable = broccoli.Run(args[2]);
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
                        broccoli.Run(statement);
                }

                broccoli.Scope = broccoli.Scope.Parent;
                return null;
            })},

            // List Functions
            {"list", new Function("list", -1, (broccoli, args) => new ValueList(args.ToList()))},
            {"len", new Function("len", 1, (broccoli, args) => {
                if (args[0] is ValueList l)
                    return new Integer(l.Count);
                if (args[0] is String s)
                    return new Integer(s.Value.Length);

                throw new Exception($"Received {TypeName(args[0])} instead of list or string in argument 1 for 'len'");
            })},
            {"first", new Function("first", 1, (broccoli, args) => {
                if (!(args[0] is ValueList list))
                    throw new Exception($"Received {TypeName(args[0])} instead of list in argument 1 for 'first'");

                return list.Count == 0 ? Atom.Nil : list[0];
            })},
            {"rest", new Function("rest", 1, (broccoli, args) => {
                if (!(args[0] is ValueList list))
                    throw new Exception($"Received {TypeName(args[0])} instead of list in argument 1 for 'rest'");

                return new ValueList(list.Skip(1).ToList());
            })},
            {"slice", new Function("slice", 3, (broccoli, args) => {
                if (!(args[0] is ValueList list))
                    throw new Exception($"Received {TypeName(args[0])} instead of list in argument 1 for 'slice'");
                if (!(args[1] is Integer i1))
                    throw new Exception($"Received {TypeName(args[0])} instead of integer in argument 2 for 'slice'");
                if (!(args[2] is Integer i2))
                    throw new Exception($"Received {TypeName(args[0])} instead of integer in argument 3 for 'slice'");

                var start = (int) i1.Value;
                var end = (int) i2.Value;
                return new ValueList(list.Skip(start).Take(end - Math.Max(0, start)));
            })},
            {"range", new Function("range", 2, (broccoli, args) => {
                if (!(args[0] is Integer i1))
                    throw new Exception($"Received {TypeName(args[0])} instead of integer in argument 1 for 'range'");
                if (!(args[1] is Integer i2))
                    throw new Exception($"Received {TypeName(args[0])} instead of integer in argument 2 for 'range'");

                var start = (int) i1.Value;
                var end = (int) i2.Value;
                return new ValueList(Enumerable.Range(start, end - start + 1).Select(value => (IValue) new Integer(value)));
            })},
            {"cat", new Function("cat", -3, (broccoli, args) => {
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
                {"", new ShortCircuitFunction("", 1, (broccoli, args) => {
                    var e = (ValueExpression) args[0];
                    if (e.IsValue)
                        return broccoli.Run(e.Values.First());
                    var first = e.Values.First();
                    IFunction fn = null;
                    if (first is Atom fnAtom) {
                        var fnName = fnAtom.Value;
                        fn = broccoli.Scope[fnName] ?? broccoli.Builtins.GetValueOrDefault(fnName, null);

                        if (fn == null)
                            throw new Exception($"Function {fnName} does not exist");
                    } else if (broccoli.Run(first) is IFunction lambda)
                        fn = lambda;
                    else
                        throw new Exception($"Function name {first} must be an identifier or lambda");

                    return fn.Invoke(broccoli, e.Values.Skip(1).ToArray());
                })},
                {"fn", new ShortCircuitFunction("fn", -3, (broccoli, args) => {
                    if (!(broccoli.Run(args[0]) is Atom name))
                        throw new Exception($"Received {TypeName(args[0])} instead of atom in argument 1 for 'fn'");
                    if (!(args[1] is ValueExpression argExpressions)) {
                        if (args[1] is ScalarVar s)
                            argExpressions = new ValueExpression(new IValueExpressible[] { s });
                        if (args[1] is ListVar l)
                            argExpressions = new ValueExpression(new IValueExpressible[] { l });
                        throw new Exception($"Received {TypeName(args[1])} instead of expression in argument 1 for 'fn'");
                    }
                    var argNames = argExpressions.Values.ToList();
                    foreach (var argExpression in argNames.Take(argNames.Count - 1))
                        if (argExpression is ValueExpression)
                            throw new Exception($"Received expression instead of variable name in argument list for 'fn'");
                    int length = argNames.Count;
                    IValue varargs = null;
                    if (argNames.Count != 0) {
                        switch (argNames.Last()) {
                            case ListVar list:
                                break;
                            case ScalarVar scalar:
                                break;
                            case ValueExpression expr when expr.Values.Length == 1 && expr.Values[0] is ListVar l:
                                length = -length - 1;
                                varargs = l;
                                break;
                            default:
                                throw new Exception($"Received expression instead of variable name in argument list for 'fn'");
                        }
                    }
                    if (varargs != null)
                        argNames.RemoveAt(argNames.Count - 1);
                    var statements = args.Skip(2);
                    broccoli.Scope.Functions[name.Value] = new Function(name.Value, length, (_, innerArgs) => {
                        broccoli.Scope = new BroccoliScope(broccoli.Scope);
                        for (int i = 0; i < argNames.Count; i++) {
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
                        if (varargs != null)
                            broccoli.Scope[(ListVar) varargs] = new ValueList(innerArgs.Skip(argNames.Count));
                        IValue result = null;
                        foreach (var statement in statements)
                            result = broccoli.Run(statement);
                        broccoli.Scope = broccoli.Scope.Parent;
                        return result;
                    });
                    return null;
                })},

                {"help", new ShortCircuitFunction("help", 0, (broccoli, args) => {
                    return new ValueList(broccoli.Builtins.Keys.Skip(1).Select(key => (IValue) new String(key)));
                })},

                {"input", new Function("input", 0, (broccoli, args) => new String(Console.ReadLine()))},

                {"string", new Function("string", 1, (broccoli, args) => new String(args[0].ToString()))},
                {"bool", new Function("bool", 1, (broccoli, args) => Boolean(CauliflowerInline.Truthy(args[0])))},
                {"int", new Function("int", 1, (broccoli, args) => {
                    switch (args[0]) {
                        case Integer i:
                            return i;
                        case Float f:
                            return new Integer((int) f.Value);
                        case String s:
                            return new Integer(int.Parse(s.Value));
                        default:
                            throw new Exception($"Received {TypeName(args[0])} instead of integer, float or string in argument 1 for 'int'");
                    }
                })},
                {"float", new Function("float", 1, (broccoli, args) => {
                    switch (args[0]) {
                        case Integer i:
                            return new Float(i.Value);
                        case Float f:
                            return f;
                        case String s:
                            return new Float(float.Parse(s.Value));
                        default:
                            throw new Exception($"Received {TypeName(args[0])} instead of integer, float or string in argument 1 for 'float'");
                    }
                })},

                {"not", new Function("not", 1, (broccoli, args) => Boolean(CauliflowerInline.Truthy(args[0]).Equals(Atom.Nil)))},
                {"and", new ShortCircuitFunction("and", -1, (broccoli, args) =>
                    Boolean(!args.Any(arg => CauliflowerInline.Truthy(broccoli.Run(arg))))
                )},
                {"or", new ShortCircuitFunction("or", -1, (broccoli, args) =>
                    Boolean(args.Any(arg => !CauliflowerInline.Truthy(broccoli.Run(arg))))
                )},

                {"\\", new ShortCircuitFunction("\\", -2, (broccoli, args) => {
                    if (!(args[0] is ValueExpression argExpressions)) {
                        if (args[0] is ScalarVar s)
                            argExpressions = new ValueExpression(new IValueExpressible[] { s });
                        if (args[0] is ListVar l)
                            argExpressions = new ValueExpression(new IValueExpressible[] { l });
                        throw new Exception($"Received {TypeName(args[0])} instead of expression in argument 1 for '\\'");
                    }
                    var argNames = argExpressions.Values.ToList();
                    foreach (var argExpression in argNames.Take(argNames.Count - 1))
                        if (argExpression is ValueExpression)
                            throw new Exception($"Received expression instead of variable name in argument list for '\\'");
                    int length = argNames.Count;
                    IValue varargs = null;
                    if (argNames.Count != 0) {
                        switch (argNames.Last()) {
                            case ListVar list:
                                break;
                            case ScalarVar scalar:
                                break;
                            case ValueExpression expr when expr.Values.Length == 1 && expr.Values[0] is ListVar l:
                                length = -length - 1;
                                varargs = l;
                                break;
                            default:
                                throw new Exception($"Received expression instead of variable name in argument list for '\\'");
                        }
                    }
                    if (varargs != null)
                        argNames.RemoveAt(argNames.Count - 1);
                    var statements = args.Skip(1);
                    return new AnonymousFunction(length, innerArgs => {
                        broccoli.Scope = new BroccoliScope(broccoli.Scope);
                        for (int i = 0; i < argNames.Count; i++) {
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
                        if (varargs != null)
                            broccoli.Scope[(ListVar) varargs] = new ValueList(innerArgs.Skip(argNames.Count));
                        IValue result = null;
                        foreach (var statement in statements)
                            result = broccoli.Run(statement);
                        broccoli.Scope = broccoli.Scope.Parent;
                        return result;
                    });
                })},

                {"if", new ShortCircuitFunction("if", -3, (broccoli, args) => {
                    var condition = broccoli.Builtins["bool"].Invoke(broccoli, new[] {broccoli.Run(args[0]) });
                    var elseIndex = Array.IndexOf(args.ToArray(), new Atom("else"), 1);
                    IEnumerable<IValueExpressible> statements = elseIndex != -1 ?
                        condition.Equals(Atom.True) ?
                            args.Skip(1).Take(elseIndex - 1) :
                            args.Skip(elseIndex + 1) :
                        condition.Equals(Atom.True) ?
                            args.Skip(1) :
                            args.Skip(args.Length);
                    IValue result = null;
                    foreach (var statement in statements)
                        result = broccoli.Run(statement);
                    return result;
                })},

                {"slice", new Function("slice", -2, (broccoli, args) => {
                    if (!(args[0] is ValueList list))
                        throw new Exception($"Received {TypeName(args[0])} instead of list in argument 1 for 'slice'");
                    foreach (var (arg, index) in args.Skip(1).WithIndex())
                        if (!(arg is Integer i))
                            throw new Exception($"Received {TypeName(arg)} instead of integer in argument {index + 2} for 'slice'");
                    var ints = args.Skip(1).Select(arg => (int) ((Integer) arg).Value).ToArray();

                    switch (ints.Length) {
                        case 0:
                            return list;
                        case 1:
                            return new ValueList(list.Skip(ints[0]));
                        case 2:
                            return new ValueList(list.Skip(ints[0]).Take(ints[1] - Math.Max(0, ints[0])));
                        case 3:
                            var result = new ValueList();
                            for (int i = ints[0]; i < ints[1]; i += ints[2])
                                result.Add(list[i]);
                            return result;
                        default:
                            throw new Exception($"Function slice requires 1 to 4 arguments, {args.Length} provided");
                    }
                })},
                {"range", new Function("range", -2, (broccoli, args) => {
                    foreach (var (arg, index) in args.WithIndex())
                        if (!(arg is Integer i))
                            throw new Exception($"Received {TypeName(arg)} instead of integer in argument {index + 1} for 'range'");
                    var ints = args.Select(arg => (int) ((Integer) arg).Value).ToArray();

                    switch (ints.Length) {
                        case 1:
                            return new ValueList(Enumerable.Range(0, ints[0] + 1).Select(i => (IValue) new Integer(i)));
                        case 2:
                            return new ValueList(Enumerable.Range(ints[0], ints[1] - ints[0] + 1).Select(i => (IValue) new Integer(i)));
                        case 3:
                            var result = new ValueList();
                            for (int i = ints[0]; i <= ints[1]; i += ints[2])
                                result.Add(new Integer(i));
                            return result;
                        default:
                            throw new Exception($"Function range requires 1 to 3 arguments, {args.Length} provided");
                    }
                })},
                {"map", new Function("map", 2, (broccoli, args) => {
                    var func = CauliflowerInline.FindFunction("map", broccoli, args[0]);

                    switch (args[1]) {
                        case ValueList l:
                            return new ValueList(l.Select(x => func.Invoke(broccoli, new IValueExpressible[] {x})));
                        default:
                            throw new Exception($"Received {TypeName(args[1])} instead of list in argument 1 for 'map'");
                    }
                })},
                {"filter", new Function("filter", 2, (broccoli, args) => {
                    var func = CauliflowerInline.FindFunction("filter", broccoli, args[0]);

                    switch (args[1]) {
                        case ValueList l:
                            return new ValueList(l.Where(x => CauliflowerInline.Truthy(func.Invoke(broccoli, new IValueExpressible[] {x}))));

                        default:
                            throw new Exception($"Received {TypeName(args[1])} instead of list in argument 1 for 'map'");
                    }
                })},
                {"all", new Function("all", 2, (broccoli, args) => {
                    var func = CauliflowerInline.FindFunction("all", broccoli, args[0]);

                    switch (args[1]) {
                        case ValueList l:
                            return Boolean(l.All(CauliflowerInline.Truthy));
                        default:
                            throw new Exception($"Received {TypeName(args[1])} instead of list in argument 1 for 'all'");
                    }
                })},
                {"any", new Function("any", 2, (broccoli, args) => {
                    var func = CauliflowerInline.FindFunction("any", broccoli, args[0]);

                    switch (args[1]) {
                        case ValueList l:
                            return Boolean(l.Any(CauliflowerInline.Truthy));
                        default:
                            throw new Exception($"Received {TypeName(args[1])} instead of list in argument 1 for 'any'");
                    }
                })},
            }.Extend(DefaultBuiltins).FluentRemove("call")}
        };

        static class CauliflowerInline {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static IFunction FindFunction(string callerName, Broccoli broccoli, IValue func) {
                switch (func) {
                    case Atom a:
                        var fn = broccoli.Scope[a.Value] ?? broccoli.Builtins.GetValueOrDefault(a.Value, null);
                        if (fn == null)
                            throw new Exception($"Function {a.Value} does not exist");
                        return fn;
                    case AnonymousFunction f:
                        return f;
                    default:
                        throw new Exception($"Received {TypeName(func)} instead of atom or lambda in argument 0 for '{callerName}'");
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static bool Truthy(IValue value) {
                switch (value) {
                    case Integer i:
                        return i.Value != 0;
                    case Float f:
                        return f.Value != 0;
                    case String s:
                        return s.Value.Length != 0;
                    case Atom a:
                        return !a.Equals(Atom.Nil);
                    case ValueList v:
                        return v.Value.Count != 0;
                    case IFunction f:
                        return true;
                    default:
                        return false;
                }
            }
        }
    }

    static class DictionaryExtensions {
        public static Dictionary<K, V> Extend<K, V>(this Dictionary<K, V> self, Dictionary<K, V> other) {
            foreach (var (key, value) in other)
                if (!self.ContainsKey(key))
                    self[key] = value;
            return self;
        }
        public static Dictionary<K, V> FluentRemove<K, V>(this Dictionary<K, V> self, params K[] keys) {
            foreach (var key in keys)
                self.Remove(key);
            return self;
        }
    }
}