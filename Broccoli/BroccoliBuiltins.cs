using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;

namespace Broccoli {
    public partial class Interpreter {
        /// <summary>
        /// Converts a .NET boolean into the Broccoli equivalent.
        /// </summary>
        /// <param name="b">The boolean to convert.</param>
        /// <returns>The Broccoli equivalent of the boolean.</returns>
        private static BAtom Boolean(bool b) => b ? BAtom.True : BAtom.Nil;

        /// <summary>
        /// A dictionary containing all the default builtin functions of Broccoli.
        /// </summary>
        public static readonly Dictionary<string, IFunction> StaticBuiltins = new Dictionary<string, IFunction> {
            // Core Language Features
            {"", new ShortCircuitFunction("", 1, (broccoli, args) => {
                var e = (ValueExpression) args[0];
                if (e.IsValue)
                    return broccoli.Run(e.Values.First());
                if (e.Values.Length == 0)
                    throw new Exception("Expected function name");
                var first = e.Values.First();
                IFunction fn = null;
                if (!(first is BAtom a))
                    throw new Exception($"Function name {first} must be an identifier");

                var fnName = a.Value;
                fn = broccoli.Scope[fnName] ?? broccoli.Builtins.GetValueOrDefault(fnName, null);
                if (fn == null)
                    throw new Exception($"Function {fnName} does not exist");

                return fn.Invoke(broccoli, e.Values.Skip(1).ToArray());
            })},
            {"fn", new ShortCircuitFunction("fn", -3, (broccoli, args) => {
                if (!(broccoli.Run(args[0]) is BAtom name))
                    throw new ArgumentTypeException(args[0], "atom", 1, "fn");
                if (!(args[1] is ValueExpression argExpressions))
                    throw new ArgumentTypeException(args[1], "expression", 2, "fn");
                var argNames = argExpressions.Values.ToArray();
                var statements = args.Skip(2);
                broccoli.Scope.Functions[name.Value] = new Function(name.Value, argNames.Length, (_, innerArgs) => {
                    broccoli.Scope = new Scope(broccoli.Scope);
                    for (int i = 0; i < innerArgs.Length; i++) {
                        var toAssign = innerArgs[i];
                        switch (argNames[i]) {
                            case ScalarVar s:
                                if (toAssign is BList)
                                    throw new Exception("Only scalars can be assigned to scalar ($) variables");
                                broccoli.Scope[s] = toAssign;
                                break;
                            case ListVar l:
                                if (!(toAssign is BList list))
                                    throw new Exception("Only lists can be assigned to list (@) variables");
                                broccoli.Scope[l] = list;
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
            
            // Meta-commands
            // Note: Many of these technically short-circuit because they have no args
            {"quit", new ShortCircuitFunction("quit", 0, (broccoli, args) => {
                Environment.Exit(0);
                return null;
            })},
            {"help", new ShortCircuitFunction("help", 0, (broccoli, args) => {
                return new BList(broccoli.Builtins.Keys.Where(key => !new []{ "", "fn", "env" }.Contains(key)).Select(key => (IValue) new BString(key)));
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
                var start = DateTime.Now;
                foreach (var expression in args)
                    broccoli.Run(expression);
                return new BFloat((DateTime.Now - start).TotalSeconds);
            })},
            {"eval", new Function("eval", 1, (broccoli, args) => {
                if (!(args[0] is BString s))
                    throw new ArgumentTypeException(args[0], "string", 1, "eval");
                return broccoli.Run(s.Value);
            })},
            {"call", new ShortCircuitFunction("call", -2, (broccoli, args) => {
                IFunction fn = null;
                var value = broccoli.Run(args[0]);
                if (!(broccoli.Run(value) is BAtom a))
                    throw new ArgumentTypeException(args[0], "atom", 1, "call");

                var fnName = a.Value;
                fn = broccoli.Scope[fnName] ?? broccoli.Builtins.GetValueOrDefault(fnName, null);
                if (fn == null)
                    throw new Exception($"Function {fnName} does not exist");

                return fn.Invoke(broccoli, args.Skip(1).ToArray());
            })},
            {"run", new Function("run", 1, (broccoli, args) => {
                if (!(args[0] is BString s))
                    throw new ArgumentTypeException(args[0], "string", 1, "run");
                return broccoli.Run(File.ReadAllText(s.Value));
            })},
            {"import", new Function("import", 1, (broccoli, args) => {
                if (!(args[0] is BString s))
                    throw new ArgumentTypeException(args[0], "string", 1, "import");
                var tempBroccoli = new Interpreter();
                tempBroccoli.Run(File.ReadAllText(s.Value));
                foreach (var (key, value) in tempBroccoli.Scope.Functions)
                    broccoli.Scope[key] = value;
                return null;
            })},

            // I/O Commands
            {"print", new Function("print", -2, (broccoli, args) => {
                foreach (var value in args) {
                    string print = null;
                    if (value is BAtom atom)
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
                    if (!(value is BInteger || value is BFloat))
                        throw new ArgumentTypeException(value, "integer or float", index + 1, "+");

                return args.Aggregate((IValue) new BInteger(0), (m, v) => {
                    if (m is BInteger im && v is BInteger iv)
                        return new BInteger(im.Value + iv.Value);
                    double fm, fv;
                    if (m is BInteger mValue)
                        fm = mValue.Value;
                    else
                        fm = ((BFloat) m).Value;
                    if (v is BInteger vValue)
                        fv = vValue.Value;
                    else
                        fv = ((BFloat) v).Value;
                    return new BFloat(fm + fv);
                });
            })},
            {"*", new Function("*", -1, (broccoli, args) => {
                foreach (var (value, index) in args.WithIndex())
                    if (!(value is BInteger || value is BFloat))
                        throw new ArgumentTypeException(value, "integer or float", index + 1, "*");

                return args.Aggregate((IValue) new BInteger(1), (m, v) => {
                    if (m is BInteger im && v is BInteger iv)
                        return new BInteger(im.Value * iv.Value);
                    double fm, fv;
                    if (m is BInteger mValue)
                        fm = mValue.Value;
                    else
                        fm = ((BFloat) m).Value;
                    if (v is BInteger vValue)
                        fv = vValue.Value;
                    else
                        fv = ((BFloat) v).Value;
                    return new BFloat(fm * fv);
                });
            })},
            {"-", new Function("-", -2, (broccoli, args) => {
                foreach (var (value, index) in args.WithIndex())
                    if (!(value is BInteger || value is BFloat))
                        throw new ArgumentTypeException(value, "integer or float", index + 1, "-");

                return args.Skip(1).Aggregate(args[0], (m, v) => {
                    if (m is BInteger im && v is BInteger iv)
                        return new BInteger(im.Value - iv.Value);
                    double fm, fv;
                    if (m is BInteger mValue)
                        fm = mValue.Value;
                    else
                        fm = ((BFloat) m).Value;
                    if (v is BInteger vValue)
                        fv = vValue.Value;
                    else
                        fv = ((BFloat) v).Value;
                    return new BFloat(fm - fv);
                });
            })},
            {"/", new Function("/", -2, (broccoli, args) => {
                foreach (var (value, index) in args.WithIndex())
                    if (!(value is BInteger || value is BFloat))
                        throw new ArgumentTypeException(value, "integer or float", index + 1, "/");

                return args.Skip(1).Aggregate(args[0], (m, v) => {
                    double fm, fv;
                    if (m is BInteger mValue)
                        fm = mValue.Value;
                    else
                        fm = ((BFloat) m).Value;
                    if (v is BInteger vValue)
                        fv = vValue.Value;
                    else
                        fv = ((BFloat) v).Value;
                    return new BFloat(fm / fv);
                });
            })},
            {":=", new ShortCircuitFunction(":=", 2, (broccoli, args) => {
                var toAssign = broccoli.Run(args[1]);
                switch (args[0]) {
                    case ScalarVar s:
                        if (!(toAssign is IScalar scalar))
                            throw new Exception("Only scalars can be assigned to scalar ($) variables");
                        broccoli.Scope[s] = toAssign;
                        break;
                    case ListVar l:
                        if (!(toAssign is BList list))
                            throw new Exception("Only lists can be assigned to list (@) variables");
                        broccoli.Scope[l] = list;
                        break;
                    default:
                        throw new Exception("Values can only be assigned to scalar ($) or list (@) variables");
                }
                return toAssign;
            })},
            {"int", new Function("int", 1, (broccoli, args) => {
                switch (args[0]) {
                    case BInteger i:
                        return i;
                    case BFloat f:
                        return new BInteger((int) f.Value);
                    default:
                        throw new ArgumentTypeException(args[0], "integer or float", 1, "int");
                }
            })},
            {"float", new Function("float", 1, (broccoli, args) => {
                switch (args[0]) {
                    case BInteger i:
                        return new BFloat(i.Value);
                    case BFloat f:
                        return f;
                    default:
                        throw new ArgumentTypeException(args[0], "integer or float", 1, "float");
                }
            })},

            // Comparison
            {"=", new Function("=", -2, (broccoli, args) => {
                return Boolean(args.Skip(1).All(element => args[0].Equals(element)));
            })},
            {"/=", new Function("/=", -2, (broccoli, args) => {
                if (args.Length == 1)
                    return BAtom.Nil;
                return Boolean(args.Skip(1).All(element => !args[0].Equals(element)));
            })},
            // TODO: make this shorter
            {"<", new Function("<", -3, (broccoli, args) => {
                foreach (var (value, index) in args.WithIndex())
                    if (!(value is BInteger || value is BFloat))
                        throw new ArgumentTypeException(args[0], "integer or float", index + 11, "<");
                var current = args[0];
                var rest = args.Skip(1);
                foreach (var next in rest) {
                    switch (current) {
                        case BInteger i:
                            switch (next) {
                                case BInteger i2:
                                    if (i.Value < i2.Value)
                                        current = next;
                                    else
                                        return BAtom.Nil;
                                    break;
                                case BFloat f:
                                    if (i.Value < f.Value)
                                        current = next;
                                    else
                                        return BAtom.Nil;
                                    break;
                            }
                            break;
                        case BFloat f:
                            switch (next) {
                                case BInteger i:
                                    if (f.Value < i.Value)
                                        current = next;
                                    else
                                        return BAtom.Nil;
                                    break;
                                case BFloat f2:
                                    if (f.Value < f2.Value)
                                        current = next;
                                    else
                                        return BAtom.Nil;
                                    break;
                            }
                            break;
                    }
                }
                return BAtom.True;
            })},
            {">", new Function(">", -3, (broccoli, args) => {
                foreach (var (value, index) in args.WithIndex())
                    if (!(value is BInteger || value is BFloat))
                        throw new ArgumentTypeException(args[0], "integer or float", index + 11, ">");
                var current = args[0];
                var rest = args.Skip(1);
                foreach (var next in rest) {
                    switch (current) {
                        case BInteger i:
                            switch (next) {
                                case BInteger i2:
                                    if (i.Value > i2.Value)
                                        current = next;
                                    else
                                        return BAtom.Nil;
                                    break;
                                case BFloat f:
                                    if (i.Value > f.Value)
                                        current = next;
                                    else
                                        return BAtom.Nil;
                                    break;
                            }
                            break;
                        case BFloat f:
                            switch (next) {
                                case BInteger i:
                                    if (f.Value > i.Value)
                                        current = next;
                                    else
                                        return BAtom.Nil;
                                    break;
                                case BFloat f2:
                                    if (f.Value > f2.Value)
                                        current = next;
                                    else
                                        return BAtom.Nil;
                                    break;
                            }
                            break;
                    }
                }
                return BAtom.True;
            })},
            {"<=", new Function("<=", -3, (broccoli, args) => {
                foreach (var (value, index) in args.WithIndex())
                    if (!(value is BInteger || value is BFloat))
                        throw new ArgumentTypeException(args[0], "integer or float", index + 11, "<=");
                var current = args[0];
                var rest = args.Skip(1);
                foreach (var next in rest) {
                    switch (current) {
                        case BInteger i:
                            switch (next) {
                                case BInteger i2:
                                    if (i.Value <= i2.Value)
                                        current = next;
                                    else
                                        return BAtom.Nil;
                                    break;
                                case BFloat f:
                                    if (i.Value <= f.Value)
                                        current = next;
                                    else
                                        return BAtom.Nil;
                                    break;
                            }
                            break;
                        case BFloat f:
                            switch (next) {
                                case BInteger i:
                                    if (f.Value <= i.Value)
                                        current = next;
                                    else
                                        return BAtom.Nil;
                                    break;
                                case BFloat f2:
                                    if (f.Value <= f2.Value)
                                        current = next;
                                    else
                                        return BAtom.Nil;
                                    break;
                            }
                            break;
                    }
                }
                return BAtom.True;
            })},
            {">=", new Function(">=", -3, (broccoli, args) => {
                foreach (var (value, index) in args.WithIndex())
                    if (!(value is BInteger || value is BFloat))
                        throw new ArgumentTypeException(args[0], "integer or float", index + 11, ">=");
                if (args.Length == 1)
                    return BAtom.Nil;
                var current = args[0];
                var rest = args.Skip(1);
                foreach (var next in rest) {
                    switch (current) {
                        case BInteger i:
                            switch (next) {
                                case BInteger i2:
                                    if (i.Value >= i2.Value)
                                        current = next;
                                    else
                                        return BAtom.Nil;
                                    break;
                                case BFloat f:
                                    if (i.Value >= f.Value)
                                        current = next;
                                    else
                                        return BAtom.Nil;
                                    break;
                            }
                            break;
                        case BFloat f:
                            switch (next) {
                                case BInteger i:
                                    if (f.Value >= i.Value)
                                        current = next;
                                    else
                                        return BAtom.Nil;
                                    break;
                                case BFloat f2:
                                    if (f.Value >= f2.Value)
                                        current = next;
                                    else
                                        return BAtom.Nil;
                                    break;
                            }
                            break;
                    }
                }
                return BAtom.True;
            })},

            // Logic
            {"not", new Function("not", 1, (broccoli, args) => Boolean(args[0].Equals(BAtom.Nil)))},
            {"and", new Function("and", -1, (broccoli, args) => Boolean(args.All(arg => !arg.Equals(BAtom.Nil))))},
            {"or", new Function("or", -1, (broccoli, args) => Boolean(args.Any(arg => !arg.Equals(BAtom.Nil))))},

            // Flow control
            {"if", new ShortCircuitFunction("if", -3, (broccoli, args) => {
                var condition = broccoli.Run(args[0]);
                if (!condition.Equals(BAtom.True) && !condition.Equals(BAtom.Nil))
                    throw new ArgumentTypeException(args[0], "boolean", 1, "if");
                var elseIndex = Array.IndexOf(args.ToArray(), new BAtom("else"), 1);
                IEnumerable<IValueExpressible> statements = elseIndex != -1 ?
                    condition.Equals(BAtom.True) ?
                        args.Skip(1).Take(elseIndex - 1) :
                        args.Skip(elseIndex + 1) :
                    condition.Equals(BAtom.True) ?
                        args.Skip(1) :
                        args.Skip(args.Length);
                IValue result = null;
                foreach (var statement in statements)
                    result = broccoli.Run(statement);
                return result;
            })},
            {"for", new ShortCircuitFunction("for", -3, (broccoli, args) => {
                var inValue = broccoli.Run(args[1]);
                if (!(inValue is BAtom inAtom))
                    throw new ArgumentTypeException(inValue, "atom", 2, "for");
                if (inAtom.Value != "in")
                    throw new ArgumentTypeException($"Received atom '{inAtom.Value}' instead of atom 'in' in argument 2 for 'for'");

                var iterable = broccoli.Run(args[2]);
                if (!(iterable is BList valueList))
                    throw new ArgumentTypeException(iterable, "list", 3, "for");

                broccoli.Scope = new Scope(broccoli.Scope);
                var statements = args.Skip(3).ToList();
                foreach (var value in valueList) {
                    switch (args[0]) {
                        case ScalarVar s:
                                if (value.GetType().In(typeof(BList), typeof(BDictionary)))
                                    throw new Exception("Only Scalars can be assigned to scalar ($) variables");
                                broccoli.Scope[s] = value;
                                break;
                        case ListVar l:
                            if (!(value is BList vList))
                                throw new Exception("Only Lists can be assigned to list (@) variables");
                            broccoli.Scope[l] = vList;
                            break;
                        case DictVar d when Program.IsCauliflower:
                            if (!(value is BDictionary vDict))
                                throw new Exception("Only Dicts can be assigned to dict (%) variables");
                            broccoli.Scope[d] = vDict;
                            break;
                    }
                    foreach (var statement in statements)
                        broccoli.Run(statement);
                }

                broccoli.Scope = broccoli.Scope.Parent;
                return null;
            })},

            // List Functions
            {"list", new Function("list", -1, (broccoli, args) => new BList(args.ToList()))},
            {"len", new Function("len", 1, (broccoli, args) => {
                switch (args[0]) {
                    case BList l:
                        return new BInteger(l.Count);
                    case BString s:
                        return new BInteger(s.Value.Length);
                    default:
                        throw new ArgumentTypeException(args[0], "list or string", 1, "len");
                }
            })},
            {"first", new Function("first", 1, (broccoli, args) => {
                if (!(args[0] is BList list))
                    throw new ArgumentTypeException(args[0], "list", 1, "first");

                return list.Count == 0 ? BAtom.Nil : list[0];
            })},
            {"rest", new Function("rest", 1, (broccoli, args) => {
                if (!(args[0] is BList list))
                    throw new ArgumentTypeException(args[0], "list", 1, "rest");

                return new BList(list.Skip(1).ToList());
            })},
            {"slice", new Function("slice", 3, (broccoli, args) => {
                if (!(args[0] is BList list))
                    throw new ArgumentTypeException(args[0], "list", 1, "slice");
                if (!(args[1] is BInteger i1))
                    throw new ArgumentTypeException(args[1], "integer", 2, "slice");
                if (!(args[2] is BInteger i2))
                    throw new ArgumentTypeException(args[1], "integer", 3, "slice");

                var start = (int) i1.Value;
                var end = (int) i2.Value;
                return new BList(list.Skip(start).Take(end - Math.Max(0, start)));
            })},
            {"range", new Function("range", 2, (broccoli, args) => {
                if (!(args[0] is BInteger i1))
                    throw new ArgumentTypeException(args[1], "integer", 1, "range");
                if (!(args[1] is BInteger i2))
                    throw new ArgumentTypeException(args[1], "integer", 2, "range");

                var start = (int) i1.Value;
                var end = (int) i2.Value;
                return new BList(Enumerable.Range(start, end - start + 1).Select(value => (IValue) new BInteger(value)));
            })},
            {"cat", new Function("cat", -3, (broccoli, args) => {
                foreach (var (value, index) in args.WithIndex())
                    if (!(value is BList))
                        throw new ArgumentTypeException(value, "list", index + 1, "cat");
                var result = new BList();
                foreach (BList list in args)
                    result.AddRange(list);
                return result;
            })},
        };
    }
}