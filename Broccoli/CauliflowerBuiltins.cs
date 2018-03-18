using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

namespace Broccoli {
    public partial class CauliflowerInterpreter : Interpreter {
        /// <summary>
        /// Converts a .NET boolean into the Broccoli equivalent.
        /// </summary>
        /// <param name="b">The boolean to convert.</param>
        /// <returns>The Broccoli equivalent of the boolean.</returns>
        private static BAtom Boolean(bool b) => b ? BAtom.True : BAtom.Nil;

        /// <summary>
        /// A dictionary containing all the default builtin functions of Cauliflower.
        /// </summary>
        public new static readonly Dictionary<string, IFunction> StaticBuiltins = new Dictionary<string, IFunction> {
            {"", new ShortCircuitFunction("", 1, (cauliflower, args) => {
                var e = (ValueExpression) args[0];
                if (e.IsValue)
                    return cauliflower.Run(e.Values.First());
                var first = e.Values.First();
                IFunction fn = null;
                if (first is BAtom fnAtom) {
                    var fnName = fnAtom.Value;
                    fn = cauliflower.Scope[fnName] ?? cauliflower.Builtins.GetValueOrDefault(fnName, null);

                    if (fn == null)
                        throw new Exception($"Function {fnName} does not exist");
                } else if (cauliflower.Run(first) is IFunction lambda)
                    fn = lambda;
                else
                    throw new Exception($"Function name {first} must be an identifier or lambda");

                return fn.Invoke(cauliflower, e.Values.Skip(1).ToArray());
            })},
            {"fn", new ShortCircuitFunction("fn", -3, (cauliflower, args) => {
                if (!(cauliflower.Run(args[0]) is BAtom name))
                    throw new ArgumentTypeException(args[0], "atom", 1, "fn");
                if (!(args[1] is ValueExpression argExpressions)) {
                    if (args[1] is ScalarVar s)
                        argExpressions = new ValueExpression(s);
                    if (args[1] is ListVar l)
                        argExpressions = new ValueExpression(l);
                    throw new ArgumentTypeException(args[1], "expression", 2, "fn");
                }
                var argNames = argExpressions.Values.ToList();
                foreach (var argExpression in argNames.Take(argNames.Count - 1))
                    if (argExpression is ValueExpression)
                        throw new ArgumentTypeException($"Received expression instead of variable name in argument list for 'fn'");
                int length = argNames.Count;
                IValue varargs = null;
                if (argNames.Count != 0) {
                    switch (argNames.Last()) {
                        case ListVar l:
                        case ScalarVar s:
                        case DictVar d:
                            break;
                        case ValueExpression expr when expr.Values.Length == 1 && expr.Values[0] is ListVar l:
                            length = -length - 1;
                            varargs = l;
                            break;
                        default:
                            throw new ArgumentTypeException($"Received expression instead of variable name in argument list for 'fn'");
                    }
                }
                if (varargs != null)
                    argNames.RemoveAt(argNames.Count - 1);
                var statements = args.Skip(2);
                cauliflower.Scope.Functions[name.Value] = new Function(name.Value, length, (_, innerArgs) => {
                    cauliflower.Scope = new Scope(cauliflower.Scope);
                    for (int i = 0; i < argNames.Count; i++) {
                        var toAssign = innerArgs[i];
                        switch (argNames[i]) {
                            case ScalarVar s:
                                if (!(toAssign is IScalar))
                                    throw new Exception("Only scalars can be assigned to scalar ($) variables");
                                cauliflower.Scope[s] = toAssign;
                                break;
                            case ListVar l:
                                if (!(toAssign is ValueList list))
                                    throw new Exception("Only lists can be assigned to list (@) variables");
                                cauliflower.Scope[l] = list;
                                break;
                            case DictVar d:
                                if (!(toAssign is ValueDict dict))
                                    throw new Exception("Only dictionaries can be assigned to dictionary (%) variables");
                                cauliflower.Scope[d] = dict;
                                break;
                            default:
                                throw new Exception("Values can only be assigned to scalar ($), list (@), or dictionary (%) variables");

                        }
                    }
                    if (varargs != null)
                        cauliflower.Scope[(ListVar) varargs] = new ValueList(innerArgs.Skip(argNames.Count));
                    IValue result = null;
                    foreach (var statement in statements)
                        result = cauliflower.Run(statement);
                    cauliflower.Scope = cauliflower.Scope.Parent;
                    return result;
                });
                return null;
            })},

            {":=", new ShortCircuitFunction(":=", 2, (broccoli, args) => {
                var toAssign = broccoli.Run(args[1]);
                switch (args[0]) {
                    case ScalarVar s:
                        if (toAssign is ValueList || toAssign is ValueDict)
                            throw new Exception("Only scalars can be assigned to scalar ($) variables");
                        broccoli.Scope[s] = toAssign;
                        break;
                    case ListVar l:
                        if (!(toAssign is ValueList list))
                            throw new Exception("Only lists can be assigned to list (@) variables");
                        broccoli.Scope[l] = list;
                        break;
                    case DictVar d:
                        if (!(toAssign is ValueDict dict))
                            throw new Exception("Only dicts can be assigned to dict (%) variables");
                        broccoli.Scope[d] = dict;
                        break;
                    default:
                        throw new Exception("Values can only be assigned to scalar ($), list (@) or dictionary (%) variables");
                }
                return toAssign;
            })},

            {"help", new ShortCircuitFunction("help", 0, (cauliflower, args) => {
                return new ValueList(cauliflower.Builtins.Keys.Skip(1).Select(key => (IValue) new BString(key)));
            })},

            {"input", new Function("input", 0, (cauliflower, args) => new BString(Console.ReadLine()))},

            {"string", new Function("string", 1, (cauliflower, args) => new BString(args[0].ToString()))},
            {"bool", new Function("bool", 1, (cauliflower, args) => Boolean(CauliflowerInline.Truthy(args[0])))},
            {"int", new Function("int", 1, (cauliflower, args) => {
                switch (args[0]) {
                    case BInteger i:
                        return i;
                    case BFloat f:
                        return new BInteger((int) f.Value);
                    case BString s:
                        return new BInteger(int.Parse(s.Value));
                    default:
                        throw new ArgumentTypeException(args[0], "integer, float or string", 1, "int");
                }
            })},
            {"float", new Function("float", 1, (cauliflower, args) => {
                switch (args[0]) {
                    case BInteger i:
                        return new BFloat(i.Value);
                    case BFloat f:
                        return f;
                    case BString s:
                        return new BFloat(float.Parse(s.Value));
                    default:
                        throw new ArgumentTypeException(args[0], "integer, float or string", 1, "float");
                }
            })},

            {"not", new Function("not", 1, (cauliflower, args) => Boolean(CauliflowerInline.Truthy(args[0]).Equals(BAtom.Nil)))},
            {"and", new ShortCircuitFunction("and", -1, (cauliflower, args) =>
                Boolean(!args.Any(arg => CauliflowerInline.Truthy(cauliflower.Run(arg))))
            )},
            {"or", new ShortCircuitFunction("or", -1, (cauliflower, args) =>
                Boolean(args.Any(arg => !CauliflowerInline.Truthy(cauliflower.Run(arg))))
            )},

            {"\\", new ShortCircuitFunction("\\", -2, (cauliflower, args) => {
                if (!(args[0] is ValueExpression argExpressions)) {
                    if (args[0] is ScalarVar s)
                        argExpressions = new ValueExpression(s);
                    if (args[0] is ListVar l)
                        argExpressions = new ValueExpression(l);
                    throw new ArgumentTypeException(args[0], "expression", 1, "\\");
                }
                var argNames = argExpressions.Values.ToList();
                foreach (var argExpression in argNames.Take(argNames.Count - 1))
                    if (argExpression is ValueExpression)
                        throw new ArgumentTypeException($"Received expression instead of variable name in argument list for '\\'");
                int length = argNames.Count;
                IValue varargs = null;
                if (argNames.Count != 0) {
                    switch (argNames.Last()) {
                        case ListVar l:
                        case ScalarVar s:
                        case DictVar d:
                            break;
                        case ValueExpression expr when expr.Values.Length == 1 && expr.Values[0] is ListVar l:
                            length = -length - 1;
                            varargs = l;
                            break;
                        default:
                            throw new ArgumentTypeException($"Received expression instead of variable name in argument list for '\\'");
                    }
                }
                if (varargs != null)
                    argNames.RemoveAt(argNames.Count - 1);
                var statements = args.Skip(1);
                return new AnonymousFunction(length, innerArgs => {
                    cauliflower.Scope = new Scope(cauliflower.Scope);
                    for (int i = 0; i < argNames.Count; i++) {
                        var toAssign = innerArgs[i];
                        switch (argNames[i]) {
                            case ScalarVar s:
                                if (!(toAssign is IScalar))
                                    throw new Exception("Only scalars can be assigned to scalar ($) variables");
                                cauliflower.Scope[s] = toAssign;
                                break;
                            case ListVar l:
                                if (!(toAssign is ValueList list))
                                    throw new Exception("Only lists can be assigned to list (@) variables");
                                cauliflower.Scope[l] = list;
                                break;
                            case DictVar d:
                                if (!(toAssign is ValueDict dict))
                                    throw new Exception("Only dictionaries can be assigned to dictionary (%) variables");
                                cauliflower.Scope[d] = dict;
                                break;
                            default:
                                throw new Exception("Values can only be assigned to scalar ($), list (@), or dictionary (%) variables");
                        }
                    }
                    if (varargs != null)
                        cauliflower.Scope[(ListVar) varargs] = new ValueList(innerArgs.Skip(argNames.Count));
                    IValue result = null;
                    foreach (var statement in statements)
                        result = cauliflower.Run(statement);
                    cauliflower.Scope = cauliflower.Scope.Parent;
                    return result;
                });
            })},

            {"if", new ShortCircuitFunction("if", -3, (cauliflower, args) => {
                var condition = cauliflower.Builtins["bool"].Invoke(cauliflower, cauliflower.Run(args[0]));
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
                    result = cauliflower.Run(statement);
                return result;
            })},

            {"len", new Function("len", 1, (broccoli, args) => {
                switch (args[0]) {
                    case ValueList l:
                        return new BInteger(l.Count);
                    case BString s:
                        return new BInteger(s.Value.Length);
                    case ValueDict d:
                        return new BInteger(d.Count);
                    default:
                        throw new ArgumentTypeException(args[0], "list, dictionary or string", 1, "len");
                }
            })},
            {"slice", new Function("slice", -2, (cauliflower, args) => {
                if (!(args[0] is ValueList list))
                    throw new ArgumentTypeException(args[0], "list", 1, "slice");
                foreach (var (arg, index) in args.Skip(1).WithIndex())
                    if (!(arg is BInteger i))
                        throw new ArgumentTypeException(arg, "integer", index + 2, "slice");
                var ints = args.Skip(1).Select(arg => (int) ((BInteger) arg).Value).ToArray();

                switch (ints.Length) {
                    case 0:
                        return list;
                    case 1:
                        return new ValueList(list.Skip(ints[0]));
                    case 2:
                        return new ValueList(list.Skip(ints[0]).Take(ints[1] - Math.Max(0, ints[0])));
                    case 3:
                        return new ValueList(Enumerable.Range(ints[0], ints[1] == ints[0] ? 0 : (ints[1] - ints[0] - 1) / ints[2] + 1).Select(i => list[ints[0] + i * ints[2]]));
                    default:
                        throw new Exception($"Function slice requires 1 to 4 arguments, {args.Length} provided");
                }
            })},
            {"range", new Function("range", -2, (cauliflower, args) => {
                foreach (var (arg, index) in args.WithIndex())
                    if (!(arg is BInteger i))
                        throw new ArgumentTypeException(arg, "integer", index + 1, "range");
                var ints = args.Select(arg => (int) ((BInteger) arg).Value).ToArray();

                switch (ints.Length) {
                    case 1:
                        return new ValueList(Enumerable.Range(0, ints[0] + 1).Select(i => (IValue) new BInteger(i)));
                    case 2:
                        return new ValueList(Enumerable.Range(ints[0], ints[1] - ints[0] + 1).Select(i => (IValue) new BInteger(i)));
                    case 3:
                        return new ValueList(Enumerable.Range(ints[0], ints[1] == ints[0] ? 0 : (ints[1] - ints[0] - 1) / ints[2] + 1).Select(i => (IValue) new BInteger(ints[0] + i * ints[2])));
                    default:
                        throw new Exception($"Function range requires 1 to 3 arguments, {args.Length} provided");
                }
            })},
            {"map", new Function("map", 2, (cauliflower, args) => {
                var func = CauliflowerInline.FindFunction("map", cauliflower, args[0]);

                if (args[1] is ValueList l)
                    return new ValueList(l.Select(x => func.Invoke(cauliflower, x)));
                throw new ArgumentTypeException(args[1], "list", 2, "map");
            })},
            {"filter", new Function("filter", 2, (cauliflower, args) => {
                var func = CauliflowerInline.FindFunction("filter", cauliflower, args[0]);

                if (args[1] is ValueList l)
                    return new ValueList(l.Where(x => CauliflowerInline.Truthy(func.Invoke(cauliflower, x))));
                throw new ArgumentTypeException(args[1], "list", 2, "filter");
            })},
            {"reduce", new Function("reduce", 2, (cauliflower, args) => {
                var func = CauliflowerInline.FindFunction("reduce", cauliflower, args[0]);

                if (args[1] is ValueList l)
                    return l.Aggregate((res, elem) => func.Invoke(cauliflower, res, elem));
                throw new ArgumentTypeException(args[1], "list", 2, "reduce");
            })},
            {"all", new Function("all", 2, (cauliflower, args) => {
                var func = CauliflowerInline.FindFunction("all", cauliflower, args[0]);

                if (args[1] is ValueList l)
                    return Boolean(l.All(CauliflowerInline.Truthy));
                throw new ArgumentTypeException(args[1], "list", 2, "all");
            })},
            {"any", new Function("any", 2, (cauliflower, args) => {
                var func = CauliflowerInline.FindFunction("any", cauliflower, args[0]);

                if (args[1] is ValueList l)
                    return Boolean(l.Any(CauliflowerInline.Truthy));
                throw new ArgumentTypeException(args[1], "list", 2, "any");
            })},
            {"mkdict", new Function("mkdict", 0, (cauliflower, args) => {
                return new ValueDict();
            })},
            {"setkey", new Function("setkey", 3, (cauliflower, args) => {
                if (args[0] is ValueDict dict)
                    return new ValueDict(new Dictionary<IValue, IValue>(dict){
                        [args[1]] = args[2]
                    });
                throw new Exception("First argument to rmkey must be a Dict.");
            })},
            {"getkey", new Function("getkey", 2, (cauliflower, args) => {
                if (args[0] is ValueDict dict)
                    return dict[args[1]];
                throw new Exception("First argument to rmkey must be a Dict.");
            })},
            {"rmkey", new Function("rmkey", 2, (cauliflower, args) => {
                if (args[0] is ValueDict dict) {
                    var d = new Dictionary<IValue, IValue>(dict);
                    d.Remove(args[1]);
                    return new ValueDict(d);
                }
                throw new Exception("First argument to rmkey must be a Dict.");
            })},
            {"haskey", new Function("haskey", 2, (cauliflower, args) => {
                if (args[0] is ValueDict dict) {
                    return Boolean(dict.ContainsKey(args[1]));
                }
                throw new Exception("First argument to rmkey must be a Dict.");
            })},
            {"keys", new Function("keys", 1, (broccoli, args) => {
                if (args[0] is ValueDict dict) {
                    return new ValueList(dict.Keys);
                }
                throw new Exception("First argument to listkeys must be a Dict.");
            })},
            {"values", new Function("values", 1, (broccoli, args) => {
                if (args[0] is ValueDict dict) {
                    return new ValueList(dict.Values);
                }
                throw new Exception("First argument to listkeys must be a Dict.");
            })},
        }.Extend(Interpreter.StaticBuiltins).FluentRemove("call");

        /// <summary>
        /// Helper functions for Cauliflower that can be inlined.
        /// </summary>
        static class CauliflowerInline {
            /// <summary>
            /// Finds the function to execute given a value.
            /// </summary>
            /// <param name="callerName">The function call name.</param>
            /// <param name="cauliflower">The containing Broccoli interpreter.</param>
            /// <param name="func">The value to try and find a function for.</param>
            /// <returns>The function to execute.</returns>
            /// <exception cref="Exception">Throws when the wrong kind of value is given or when the function cannot be found.</exception>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static IFunction FindFunction(string callerName, Interpreter cauliflower, IValue func) {
                switch (func) {
                    case BAtom a:
                        var fn = cauliflower.Scope[a.Value] ?? cauliflower.Builtins.GetValueOrDefault(a.Value, null);
                        if (fn == null)
                            throw new Exception($"Function {a.Value} does not exist");
                        return fn;
                    case AnonymousFunction f:
                        return f;
                    default:
                        throw new ArgumentTypeException(func, "atom or lambda", 1, callerName);
                }
            }

            /// <summary>
            /// Returns whether a Broccoli value is truthy.
            /// </summary>
            /// <param name="value">The value to check.</param>
            /// <returns>The truthiness of the value.</returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static bool Truthy(IValue value) {
                switch (value) {
                    case BInteger i:
                        return i.Value != 0;
                    case BFloat f:
                        return f.Value != 0;
                    case BString s:
                        return s.Value.Length != 0;
                    case BAtom a:
                        return !a.Equals(BAtom.Nil);
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

    /// <summary>
    /// Dictionary extension methods.
    /// </summary>
    static class DictionaryExtensions {
        /// <summary>
        /// Adds all the keys + values of a dictionary to the current dictionary.
        /// </summary>
        /// <param name="self">The current dictionary.</param>
        /// <param name="other">The other dictionary whose values to add.</param>
        /// <typeparam name="K">The key type.</typeparam>
        /// <typeparam name="V">The value type.</typeparam>
        /// <returns>The current dictionary.</returns>
        public static Dictionary<K, V> Extend<K, V>(this Dictionary<K, V> self, Dictionary<K, V> other) {
            foreach (var (key, value) in other)
                if (!self.ContainsKey(key))
                    self[key] = value;
            return self;
        }

        /// <summary>
        /// Removes all the keys given from the dictionary.
        /// </summary>
        /// <param name="self">The current dictionary.</param>
        /// <param name="keys">The keys to remove.</param>
        /// <typeparam name="K">The key type.</typeparam>
        /// <typeparam name="V">The value type.</typeparam>
        /// <returns>The current dictionary.</returns>
        public static Dictionary<K, V> FluentRemove<K, V>(this Dictionary<K, V> self, params K[] keys) {
            foreach (var key in keys)
                self.Remove(key);
            return self;
        }
    }
}