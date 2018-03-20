using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;

// TODO: test "->"
// TODO: "namespace" function & maybe various OOP things
// TODO: try catch

namespace Broccoli {
    static class TypeExtensions {
        public static PropertyInfo TryGetProperty(this Type type, string name) {
            var result = type.GetProperty(name);
            if (result == null)
                throw new Exception($"Type '{type.FullName}' has no field '{name}'");
            return result;
        }

        public static FieldInfo TryGetField(this Type type, string name) {
            var result = type.GetField(name);
            if (result == null)
                throw new Exception($"Type '{type.FullName}' has no field '{name}'");
            return result;
        }
    }

    public partial class CauliflowerInterpreter : Interpreter {
        private static Assembly[] assemblies = null;
        private static Dictionary<string, Assembly> assemblyLookup = null;

        private static IValue CreateValue(object value) {
            if (value == null)
                return null;
            switch (value) {
                case char c:
                    return new BInteger(c);
                case short s:
                    return new BInteger(s);
                case int i:
                    return new BInteger(i);
                case long l:
                    return new BInteger(l);
                case float f:
                    return new BFloat(f);
                case double d:
                    return new BFloat(d);
                case bool b:
                    return Boolean(b);
                case string s:
                    return new BString(s);
                case IValue v:
                    return v;
                default:
                    return new BCSharpValue(value);
            }
        }

        private class BCSharpValue : IScalar {
            public object Value { get; }

            public BCSharpValue(object value) {
                Value = value;
            }

            public override string ToString() => Value.ToString();

            public string Inspect() => $"C#Value<{Value.GetType().FullName}>({Value.ToString()})";

            public object ToCSharp() => Value;

            public Type Type() => Value.GetType();
        }

        private class BCSharpType : IScalar {
            public Type Value { get; }

            public BCSharpType(Type value) {
                Value = value;
            }

            public static implicit operator BCSharpType(Type type) => new BCSharpType(type);

            public static implicit operator Type(BCSharpType type) => type.Value;

            public override string ToString() => Value.FullName;

            public string Inspect() => $"C#Type({Value.FullName})";

            public object ToCSharp() => Value;

            public Type Type() => typeof(Type);
        }

        private class BCSharpMethod : IScalar {
            public (Type type, string name) Value { get; }

            public BCSharpMethod(Type type, string name) {
                Value = (type, name);
            }

            public override string ToString() => $"{Value.type.FullName}.{Value.name}";

            public string Inspect() => $"C#Method({Value.type.FullName}.{Value.name})";

            public object ToCSharp() => Value;

            public Type Type() => typeof(Tuple<Type, string>);
        }

        private static void CSharpAddType(Interpreter interpreter, BCSharpType type) {
            interpreter.Scope.Scalars[type.Value.Name] = type;
            foreach (var method in new HashSet<string>(type.Value.GetMethods(BindingFlags.Public | BindingFlags.Static).Select(method => method.Name)))
                interpreter.Scope.Scalars[type.Value.Name + '.' + method] = new BCSharpMethod(type, method);
        }

        private static void CSharpImport(Interpreter interpreter, BAtom name) {
            // Basically lazy load assemblies
            if (assemblies == null) {
                assemblies = AppDomain.CurrentDomain.GetAssemblies();
                assemblyLookup = new Dictionary<string, Assembly>(assemblies.ToDictionary(a => new AssemblyName(a.FullName).Name, a => a));
            }
            var path = name.Value;
            if (assemblyLookup.ContainsKey(path)) {
                foreach (var subType in assemblyLookup[path].GetExportedTypes())
                    CSharpAddType(interpreter, subType);
                return;
            }
            Type type;
            if ((type = Type.GetType($"{path}")) != null) {
                CSharpAddType(interpreter, type);
                return;
            }
            foreach (var assembly in assemblies)
                if ((type = Type.GetType($"{path}, {assembly.FullName}")) != null) {
                    CSharpAddType(interpreter, type);
                    return;
                }
            throw new Exception($"C# type {path} not found");
        }

        private static BCSharpValue CSharpCreate(BCSharpType type, IEnumerable<IValue> parameters) => new BCSharpValue(
            type.Value.GetConstructor(parameters.Select(p => p.Type()).ToArray())
                .Invoke(parameters.Select(p => p.ToCSharp()).ToArray())
        );

        private static IValue CSharpMethod(BCSharpType type, BAtom name, IValue instance, IEnumerable<IValue> args) => CreateValue(
            type.Value.GetMethod(name.Value, args.Select(arg => arg.Type()).ToArray())
                .Invoke(instance?.ToCSharp(), args.Select(arg => arg.ToCSharp()).ToArray())
        );

        private static IValue CSharpProperty(BCSharpType type, BAtom name, IValue instance) => CreateValue(
            type.Value.TryGetProperty(name.Value).GetValue(instance?.ToCSharp())
        );

        private static void AssignCSharpProperty(BCSharpType type, BAtom name, IValue instance, IValue value) {
            type.Value.TryGetProperty(name.Value).SetValue(instance?.ToCSharp(), value.ToCSharp());
        }

        private static IValue CSharpField(BCSharpType type, BAtom name, IValue instance) => CreateValue(
            type.Value.TryGetField(name.Value).GetValue(instance?.ToCSharp())
        );

        private static void AssignCSharpField(BCSharpType type, BAtom name, IValue instance, IValue value) {
            type.Value.TryGetField(name.Value).SetValue(instance?.ToCSharp(), value.ToCSharp());
        }

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
                if (e.Values.Length == 0)
                    throw new Exception("Expected function name");
                if (cauliflower.Run(e.Values[0]) is BCSharpMethod method)
                    try {
                        return CSharpMethod(method.Value.type, method.Value.name, null, e.Values.Skip(1).Select(cauliflower.Run));
                    } catch {
                        throw new Exception($"C# method '{method.Value.name}' not found for specified arguments. Are you missing a 'c#-import'?");
                    }
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
            {"->", new ShortCircuitFunction("->", -2, (cauliflower, args) => {
                var scope = cauliflower.Scope.Namespace;
                foreach (var (item, index) in args.SkipLast(1).WithIndex())
                    if (item is BAtom a) {
                        if (scope.ContainsKey(a.Value))
                            scope = scope[a.Value];
                        else
                            throw new Exception($"Object '{a.Value}' not found in namespace");
                    } else
                        throw new ArgumentTypeException(item, "atom", index + 1, "->");
                switch (args.Last()) {
                    case ScalarVar s:
                        return scope.Value[s];
                    case ListVar l:
                        return scope.Value[l];
                    case DictVar d:
                        return scope.Value[d];
                    case BString s:
                        return scope.Value[s.Value];
                    default:
                        throw new ArgumentTypeException(args.Last(), "variable", args.Length, "->");
                }
            })},
            {"$", new Function("$", 1, (cauliflower, args) => args[0] is IScalar ? args[0] : new BCSharpValue(args[0]))},
            {"@", new Function("@", 1, (cauliflower, args) => {
                if (args[0] is BList)
                    return args[0];
                var value = args[0].ToCSharp();
                if (value.GetType().GetInterface("IEnumerable") != null) {
                    var result = new BList();
                    foreach (var item in (IEnumerable) value)
                        result.Add(CreateValue(item));
                    return result;
                }
                throw new ArgumentTypeException(args[0], "list", 1, "@");
            })},
            {"%", new Function("%", 1, (cauliflower, args) => {
                if (args[0] is BDictionary)
                    return args[0];
                var value = args[0].ToCSharp();
                if (value.GetType().GetInterface("IDictionary") != null) {
                    var result = new BDictionary();
                    var source = ((IDictionary) value);
                    foreach (var key in source.Keys)
                        result[CreateValue(key)] = CreateValue(source[key]);
                    return result;
                }
                throw new ArgumentTypeException(args[0], "list", 1, "%");
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
                                if (!(toAssign is BList list))
                                    throw new Exception("Only lists can be assigned to list (@) variables");
                                cauliflower.Scope[l] = list;
                                break;
                            case DictVar d:
                                if (!(toAssign is BDictionary dict))
                                    throw new Exception("Only dictionaries can be assigned to dictionary (%) variables");
                                cauliflower.Scope[d] = dict;
                                break;
                            default:
                                throw new Exception("Values can only be assigned to scalar ($), list (@), or dictionary (%) variables");

                        }
                    }
                    if (varargs != null)
                        cauliflower.Scope[(ListVar) varargs] = new BList(innerArgs.Skip(argNames.Count));
                    IValue result = null;
                    foreach (var statement in statements)
                        result = cauliflower.Run(statement);
                    cauliflower.Scope = cauliflower.Scope.Parent;
                    return result;
                });
                return null;
            })},

            // Meta-commands
            {"c#-import", new Function("c#-import", -2, (cauliflower, args) => {
                if (!(args[0] is BAtom a))
                    throw new ArgumentTypeException(args[0], "atom", 1, "c#-import");
                CSharpImport(cauliflower, a);
                return null;
            })},
            {"c#-create", new Function("c#-create", -2, (cauliflower, args) => {
                if (!(args[0] is BCSharpType type))
                    throw new ArgumentTypeException(args[0], "C# type", 1, "c#-create");
                return CSharpCreate(type, args.Skip(1));
            })},
            {"c#-static", new Function("c#-static", -2, (cauliflower, args) => {
                if (args[0] is BCSharpMethod method)
                    try {
                        return CSharpMethod(method.Value.type, method.Value.name, null, args.Skip(1));
                    } catch {
                        throw new Exception($"C# method '{method.Value.name}' not found for specified arguments. Are you missing a 'c#-import'?");
                    }
                if (!(args[0] is BCSharpType type))
                    throw new ArgumentTypeException(args[0], "C# type or method", 1, "c#-static");
                if (!(args[1] is BAtom name))
                    throw new ArgumentTypeException(args[1], "atom", 2, "c#-static");
                try {
                    return CSharpMethod(type, name, null, args.Skip(2));
                } catch {
                    throw new Exception($"C# method '{name}' not found for specified arguments. Are you missing a 'c#-import'?");
                }
            })},
            {"c#-method", new Function("c#-method", -3, (cauliflower, args) => {
                if (!(args[0] is BCSharpValue value))
                    throw new ArgumentTypeException(args[0], "C# value", 1, "c#-method");
                if (!(args[1] is BAtom name))
                    throw new ArgumentTypeException(args[1], "atom", 2, "c#-method");
                try {
                    return CSharpMethod(value.Type(), name, value, args.Skip(2));
                } catch {
                    throw new Exception($"C# method '{name}' not found for specified arguments. Are you missing a 'c#-import'?");
                }
            })},
            {"c#-property", new Function("c#-property", -2, (cauliflower, args) => {
                if (args[0] is BCSharpType type && args[1] is BAtom a) {
                    if (args.Length > 2) {
                        AssignCSharpProperty(type.Value, a, null, args[2]);
                        return null;
                    }
                    return CSharpProperty(type.Value, a, null);
                }
                if (!(args[0] is BCSharpValue value))
                    throw new ArgumentTypeException(args[0], "C# value or type", 1, "c#-property");
                if (!(args[1] is BAtom name))
                    throw new ArgumentTypeException(args[1], "atom", 2, "c#-property");
                try {
                    if (args.Length > 2) {
                        AssignCSharpProperty(value.Type(), name, value, args[2]);
                        return null;
                    }
                    return CSharpProperty(value.Type(), name, value);
                } catch {
                    throw new Exception($"C# property '{name}' not found for specified arguments. Are you missing a 'c#-import'?");
                }
            })},
            {"c#-field", new Function("c#-field", -2, (cauliflower, args) => {
                if (args[0] is BCSharpType type && args[1] is BAtom a) {
                    if (args.Length > 2) {
                        AssignCSharpField(type.Value, a, null, args[2]);
                        return null;
                    }
                    return CSharpField(type.Value, a, null);
                }
                if (!(args[0] is BCSharpValue value))
                    throw new ArgumentTypeException(args[0], "C# value or type", 1, "c#-field");
                if (!(args[1] is BAtom name))
                    throw new ArgumentTypeException(args[1], "atom", 2, "c#-field");
                try {
                    if (args.Length > 2) {
                        AssignCSharpField(value.Type(), name, value, args[2]);
                        return null;
                    }
                    return CSharpField(value.Type(), name, value);
                } catch {
                    throw new Exception($"C# property '{name}' not found for specified arguments. Are you missing a 'c#-import'?");
                }
            })},
            {"c#-char", new Function("c#-char", 1, (cauliflower, args) => {
                switch (args[0]) {
                    case BInteger i:
                        return new BCSharpValue((char) i);
                    case BFloat f:
                        return new BCSharpValue((char) f);
                    default:
                        return args[0];
                }
            })},
            {"c#-short", new Function("c#-short", 1, (cauliflower, args) => {
                switch (args[0]) {
                    case BInteger i:
                        return new BCSharpValue((short) i);
                    case BFloat f:
                        return new BCSharpValue((short) f);
                    default:
                        return args[0];
                }
            })},
            {"c#-int", new Function("c#-int", 1, (cauliflower, args) => {
                switch (args[0]) {
                    case BInteger i:
                        return new BCSharpValue((int) i);
                    case BFloat f:
                        return new BCSharpValue((int) f);
                    default:
                        return args[0];
                }
            })},
            {"c#-float", new Function("c#-float", 1, (cauliflower, args) => {
                switch (args[0]) {
                    case BInteger i:
                        return new BCSharpValue((float) i);
                    case BFloat f:
                        return new BCSharpValue((float) f);
                    default:
                        return args[0];
                }
            })},
            {"help", new ShortCircuitFunction("help", 0, (cauliflower, args) => {
                return new BList(cauliflower.Builtins.Keys.Skip(1).Select(key => (IValue) new BString(key)));
            })},

            {":=", new ShortCircuitFunction(":=", 2, (broccoli, args) => {
                var toAssign = broccoli.Run(args[1]);
                switch (args[0]) {
                    case ScalarVar s:
                        if (toAssign is BList || toAssign is BDictionary)
                            throw new Exception("Only scalars can be assigned to scalar ($) variables");
                        broccoli.Scope[s] = toAssign;
                        break;
                    case ListVar l:
                        if (!(toAssign is BList list))
                            throw new Exception("Only lists can be assigned to list (@) variables");
                        broccoli.Scope[l] = list;
                        break;
                    case DictVar d:
                        if (!(toAssign is BDictionary dict))
                            throw new Exception("Only dicts can be assigned to dict (%) variables");
                        broccoli.Scope[d] = dict;
                        break;
                    default:
                        throw new Exception("Values can only be assigned to scalar ($), list (@) or dictionary (%) variables");
                }
                return toAssign;
            })},

            // I/O commands
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
                                if (!(toAssign is BList list))
                                    throw new Exception("Only lists can be assigned to list (@) variables");
                                cauliflower.Scope[l] = list;
                                break;
                            case DictVar d:
                                if (!(toAssign is BDictionary dict))
                                    throw new Exception("Only dictionaries can be assigned to dictionary (%) variables");
                                cauliflower.Scope[d] = dict;
                                break;
                            default:
                                throw new Exception("Values can only be assigned to scalar ($), list (@), or dictionary (%) variables");
                        }
                    }
                    if (varargs != null)
                        cauliflower.Scope[(ListVar) varargs] = new BList(innerArgs.Skip(argNames.Count));
                    IValue result = null;
                    foreach (var statement in statements)
                        result = cauliflower.Run(statement);
                    cauliflower.Scope = cauliflower.Scope.Parent;
                    return result;
                });
            })},
            
            {"if", new ShortCircuitFunction("if", -3, (cauliflower, args) => {
                var condition = CauliflowerInline.Truthy(cauliflower.Run(args[0]));
                var elseIndex = Array.IndexOf(args.ToArray(), new BAtom("else"), 1);
                IEnumerable<IValueExpressible> statements = elseIndex != -1 ?
                    condition ? args.Skip(1).Take(elseIndex - 1) : args.Skip(elseIndex + 1) :
                    condition ? args.Skip(1) : args.Skip(args.Length);
                IValue result = null;
                foreach (var statement in statements)
                    result = cauliflower.Run(statement);
                return result;
            })},

            {"len", new Function("len", 1, (broccoli, args) => {
                switch (args[0]) {
                    case BList l:
                        return new BInteger(l.Count);
                    case BString s:
                        return new BInteger(s.Value.Length);
                    case BDictionary d:
                        return new BInteger(d.Count);
                    default:
                        throw new ArgumentTypeException(args[0], "list, dictionary or string", 1, "len");
                }
            })},
            {"slice", new Function("slice", -2, (cauliflower, args) => {
                if (!(args[0] is BList list))
                    throw new ArgumentTypeException(args[0], "list", 1, "slice");
                foreach (var (arg, index) in args.Skip(1).WithIndex())
                    if (!(arg is BInteger i))
                        throw new ArgumentTypeException(arg, "integer", index + 2, "slice");
                var ints = args.Skip(1).Select(arg => (int) ((BInteger) arg).Value).ToArray();

                switch (ints.Length) {
                    case 0:
                        return list;
                    case 1:
                        return new BList(list.Skip(ints[0]));
                    case 2:
                        return new BList(list.Skip(ints[0]).Take(ints[1] - Math.Max(0, ints[0])));
                    case 3:
                        return new BList(Enumerable.Range(ints[0], ints[1] == ints[0] ? 0 : (ints[1] - ints[0] - 1) / ints[2] + 1).Select(i => list[ints[0] + i * ints[2]]));
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
                        return new BList(Enumerable.Range(0, ints[0] + 1).Select(i => (IValue) new BInteger(i)));
                    case 2:
                        return new BList(Enumerable.Range(ints[0], ints[1] - ints[0]).Select(i => (IValue) new BInteger(i)));
                    case 3:
                        return new BList(Enumerable.Range(ints[0], ints[1] == ints[0] ? 0 : (ints[1] - ints[0] - 1) / ints[2] + 1).Select(i => (IValue) new BInteger(ints[0] + i * ints[2])));
                    default:
                        throw new Exception($"Function range requires 1 to 3 arguments, {args.Length} provided");
                }
            })},
            {"map", new Function("map", 2, (cauliflower, args) => {
                var func = CauliflowerInline.FindFunction("map", cauliflower, args[0]);

                if (args[1] is BList l)
                    return new BList(l.Select(x => func.Invoke(cauliflower, x)));
                throw new ArgumentTypeException(args[1], "list", 2, "map");
            })},
            {"filter", new Function("filter", 2, (cauliflower, args) => {
                var func = CauliflowerInline.FindFunction("filter", cauliflower, args[0]);

                if (args[1] is BList l)
                    return new BList(l.Where(x => CauliflowerInline.Truthy(func.Invoke(cauliflower, x))));
                throw new ArgumentTypeException(args[1], "list", 2, "filter");
            })},
            {"reduce", new Function("reduce", 2, (cauliflower, args) => {
                var func = CauliflowerInline.FindFunction("reduce", cauliflower, args[0]);

                if (args[1] is BList l)
                    return l.Aggregate((res, elem) => func.Invoke(cauliflower, res, elem));
                throw new ArgumentTypeException(args[1], "list", 2, "reduce");
            })},
            {"all", new Function("all", 2, (cauliflower, args) => {
                var func = CauliflowerInline.FindFunction("all", cauliflower, args[0]);

                if (args[1] is BList l)
                    return Boolean(l.All(CauliflowerInline.Truthy));
                throw new ArgumentTypeException(args[1], "list", 2, "all");
            })},
            {"any", new Function("any", 2, (cauliflower, args) => {
                var func = CauliflowerInline.FindFunction("any", cauliflower, args[0]);

                if (args[1] is BList l)
                    return Boolean(l.Any(CauliflowerInline.Truthy));
                throw new ArgumentTypeException(args[1], "list", 2, "any");
            })},
            {"mkdict", new Function("mkdict", 0, (cauliflower, args) => {
                return new BDictionary();
            })},
            {"setkey", new Function("setkey", 3, (cauliflower, args) => {
                if (args[0] is BDictionary dict)
                    return new BDictionary(new Dictionary<IValue, IValue>(dict){
                        [args[1]] = args[2]
                    });
                throw new Exception("First argument to rmkey must be a Dict.");
            })},
            {"getkey", new Function("getkey", 2, (cauliflower, args) => {
                if (args[0] is BDictionary dict)
                    return dict[args[1]];
                throw new Exception("First argument to rmkey must be a Dict.");
            })},
            {"rmkey", new Function("rmkey", 2, (cauliflower, args) => {
                if (args[0] is BDictionary dict) {
                    var d = new Dictionary<IValue, IValue>(dict);
                    d.Remove(args[1]);
                    return new BDictionary(d);
                }
                throw new Exception("First argument to rmkey must be a Dict.");
            })},
            {"haskey", new Function("haskey", 2, (cauliflower, args) => {
                if (args[0] is BDictionary dict) {
                    return Boolean(dict.ContainsKey(args[1]));
                }
                throw new Exception("First argument to rmkey must be a Dict.");
            })},
            {"keys", new Function("keys", 1, (broccoli, args) => {
                if (args[0] is BDictionary dict) {
                    return new BList(dict.Keys);
                }
                throw new Exception("First argument to listkeys must be a Dict.");
            })},
            {"values", new Function("values", 1, (broccoli, args) => {
                if (args[0] is BDictionary dict) {
                    return new BList(dict.Values);
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
                    case BList v:
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