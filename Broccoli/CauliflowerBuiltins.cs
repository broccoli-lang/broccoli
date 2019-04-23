using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Text;
using Lokad.ILPack;

// ReSharper disable InconsistentNaming
// ReSharper disable PossibleMultipleEnumeration
// ReSharper disable CoVariantArrayConversion

// TODO: clisp's boole, and various other

namespace Broccoli {
    public partial class CauliflowerInterpreter : Interpreter {
        /// <summary>
        /// Array of all assembles used by this project.
        /// </summary>
        private static Assembly[] assemblies;

        /// <summary>
        /// Dictionary of assembly full name to Assembly object.
        /// </summary>
        private static Dictionary<string, Assembly> assemblyLookup;

        /// <summary>
        /// Base path of Cauliflower module storage.
        /// </summary>
        private static string basePath;

        /// <summary>
        /// Path separator of OS.
        /// </summary>
        private static string pathSeparator;

        /// <summary>
        /// Cache of methods grouped by type.
        /// </summary>
        private static readonly Dictionary<Type, Dictionary<string, MethodBase>> methods = new Dictionary<Type, Dictionary<string, MethodBase>>();

        /// <summary>
        /// Cache of fields grouped by type.
        /// </summary>
        private static readonly Dictionary<Type, Dictionary<string, FieldInfo>> fields = new Dictionary<Type, Dictionary<string, FieldInfo>>();

        /// <summary>
        /// Cache of properties grouped by type.
        /// </summary>
        private static readonly Dictionary<Type, Dictionary<string, PropertyInfo>> properties = new Dictionary<Type, Dictionary<string, PropertyInfo>>();

        /// <summary>
        /// Valid access control modifiers ranked from least to most restrictive.
        /// </summary>
        private static readonly string[] accessControlModifiers = { "public", "protected", "private" };


        /// <summary>
        /// All valid class item modifiers.
        /// </summary>
        private static readonly string[] modifiers = accessControlModifiers.Concat(new [] { "readonly", "static", "operator" }).ToArray();

        private static readonly Function Noop = new Function("", ~0, (interpreter, args) => null);

        /// <summary>
        /// Creates an IValue from an object.
        /// </summary>
        /// <param name="value">The value to convert.</param>
        /// <returns>An IValue with an equivalent value.</returns>
        public static IValue CreateValue(object value) {
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

        /// <summary>
        /// IScalar wrapper for C# objects.
        /// </summary>
        public class BCSharpValue : IScalar {
            // ReSharper disable once MemberCanBePrivate.Global
            public object Value { get; }

            public BCSharpValue(object value) => Value = value;

            public override string ToString() => Value.ToString();

            public string Inspect() => $"C#Value<{Value.GetType().FullName}>({Value})";

            public object ToCSharp() => Value;

            public Type Type() => Value.GetType();

            public IScalar ScalarContext() => this;

            public IList ListContext() => throw new NoListContextException(this);

            public IDictionary DictionaryContext() => throw new NoDictionaryContextException(this);
        }

        /// <summary>
        /// IScalar wrapper for C# types.
        /// </summary>
        public class BCSharpType : IScalar {
            public Type Value { get; }

            // ReSharper disable once MemberCanBeProtected.Global
            public BCSharpType(Type value) => Value = value;

            public static implicit operator BCSharpType(Type type) => new BCSharpType(type);

            public static implicit operator Type(BCSharpType type) => type.Value;

            public override string ToString() => Value.FullName;

            public virtual string Inspect() => $"C#Type({Value.FullName})";

            public object ToCSharp() => Value;

            public Type Type() => typeof(Type);

            public IScalar ScalarContext() => this;

            public IList ListContext() => throw new NoListContextException(this);

            public IDictionary DictionaryContext() => throw new NoDictionaryContextException(this);
        }

        /// <inheritdoc />
        /// <summary>
        /// IScalar wrapper for Cauliflower native module types.
        /// </summary>
        public class BCauliflowerType : BCSharpType {
            // ReSharper disable once MemberCanBePrivate.Global
            public BCauliflowerType(Type value) : base(value) { }

            public static implicit operator BCauliflowerType(Type type) => new BCauliflowerType(type);

            public static implicit operator Type(BCauliflowerType type) => type.Value;

            public override string Inspect() => $"CauliflowerType({Value.FullName.Replace('+', '.')})";
        }


        /// <summary>
        /// IScalar wrapper for C# methods.
        /// </summary>
        private class BCSharpMethod : IScalar {
            public (Type type, string name) Value { get; }

            // ReSharper disable once MemberCanBeProtected.Local
            public BCSharpMethod(Type type, string name) => Value = (type, name);

            public override string ToString() => $"{Value.type.FullName}.{Value.name}";

            public virtual string Inspect() => $"C#Method({Value.type.FullName}.{Value.name})";

            public object ToCSharp() => Value;

            public Type Type() => typeof(Tuple<Type, string>);

            public IScalar ScalarContext() => this;

            public IList ListContext() => throw new NoListContextException(this);

            public IDictionary DictionaryContext() => throw new NoDictionaryContextException(this);
        }


        /// <inheritdoc />
        /// <summary>
        /// IScalar wrapper for Cauliflower native method types.
        /// </summary>
        // ReSharper disable once ClassNeverInstantiated.Local
        private class BCauliflowerMethod : BCSharpMethod {
            public BCauliflowerMethod(Type type, string name) : base(type, name) { }

            public override string Inspect() => $"CauliflowerMethod({Value.type.FullName.Replace('+', '.')}.{Value.name})";
        }

        /// <summary>
        /// Adds C# type to current scope.
        /// </summary>
        /// <param name="interpreter">Interpreter to add types to.</param>
        /// <param name="type">Type to add.</param>
        private static void CSharpAddType(Interpreter interpreter, BCSharpType type) {
            interpreter.Scope.Types[type.Value.Name] = type;
//            foreach (var method in new HashSet<string>(type.Value.GetMethods(BindingFlags.Public | BindingFlags.Static).Select(method => method.Name)))
//                interpreter.Scope.Scalars[type.Value.Name + '.' + method] = new BCSharpMethod(type, method);
        }

        /// <summary>
        /// Imports a C# type or assembly given its name.
        /// </summary>
        /// <param name="interpreter">Interpreter to import to.</param>
        /// <param name="name">Name of type or assembly.</param>
        private static void CSharpImport(Interpreter interpreter, BAtom name) {
            // Basically lazy load assemblies
            if (assemblies == null) {
                assemblies = AppDomain.CurrentDomain.GetAssemblies();
                assemblyLookup = new Dictionary<string, Assembly>(assemblies.ToDictionary(a => new AssemblyName(a.FullName).Name, a => a));
            }
            var path = name.Value;


            Type type;
            if ((type = Type.GetType($"{path}")) != null) {
                CSharpAddType(interpreter, type);
                return;
            }

            var directSubtypes = assemblyLookup.Values.SelectMany(v => v.ExportedTypes).Where(a => a.Namespace == path);
            foreach (var subtype in directSubtypes)
                CSharpAddType(interpreter, subtype);

            if (assemblies.Any(assembly => (type = Type.GetType($"{path}, {assembly.FullName}")) != null)) {
                CSharpAddType(interpreter, type);
                return;
            }

            if (!directSubtypes.Any())
                throw new Exception($"C# type {path} not found");
        }

        /// <summary>
        /// Create a C# value.
        /// </summary>
        /// <param name="type">Type of value to create.</param>
        /// <param name="parameters">Parameters to feed to the constructor</param>
        /// <returns>The created value.</returns>
        private static BCSharpValue CSharpCreate(BCSharpType type, IEnumerable<IValue> parameters) => new BCSharpValue(
            type.Value.GetConstructor(parameters.Select(p => p.Type()).ToArray())
                ?.Invoke(parameters.Select(p => p.ToCSharp()).ToArray())
            ?? throw new Exception($"C# type '{type.Value.FullName}' has no constructor accepting arguments {string.Join(", ", parameters.Select(p => p.Type().FullName))}")
        );

        /// <summary>
        /// Create a Cauliflower value.
        /// </summary>
        /// <param name="type">Type of value to create.</param>
        /// <param name="parameters">Parameters to feed to the constructor.</param>
        /// <returns>The created value.</returns>
        private static IValue CauliflowerCreate(BCauliflowerType type, IEnumerable<IValue> parameters) {
            return (IValue) (
                type.Value.GetConstructor(parameters.Select(p => p.GetType()).ToArray())
                    ?.Invoke(parameters.ToArray())
                ?? throw new Exception(
                    $"Cauliflower type '{type.Value.FullName}' has no constructor accepting arguments {string.Join(", ", parameters.Select(p => p.Type().Name))}")
            );
        }

        /// <summary>
        /// Call a C# method.
        /// </summary>
        /// <param name="type">Type method is on.</param>
        /// <param name="name">Name of method.</param>
        /// <param name="instance">Object to call method on, or null if method is static.</param>
        /// <param name="args">Arguments to pass to method.</param>
        /// <returns>Return value of method.</returns>
        private static IValue CSharpMethod(BCSharpType type, BAtom name, IValue instance, IEnumerable<IValue> args) => CreateValue(
            type.Value.GetMethod(name.Value, args.Select(arg => arg.Type()).ToArray())
                .Invoke(instance?.ToCSharp(), args.Select(arg => arg.ToCSharp()).ToArray())
        );

        /// <summary>
        /// Call a Cauliflower native method.
        /// </summary>
        /// <param name="type">Type method is on.</param>
        /// <param name="name">Name of method.</param>
        /// <param name="instance">Object to call method on, or null if method is static.</param>
        /// <param name="args">Arguments to pass to method.</param>
        /// <returns>Return value of method.</returns>
        private static IValue CauliflowerMethod(BCSharpType type, BAtom name, IValue instance, IEnumerable<IValue> args) => (IValue) (
            type.Value.GetMethod(name.Value, args.Select(arg => arg.GetType()).ToArray())
                .Invoke(instance?.ToCSharp(), args.ToArray())
        );

        /// <summary>
        /// Get the value of a C# property.
        /// </summary>
        /// <param name="type">Type property is on.</param>
        /// <param name="name">Name of property.</param>
        /// <param name="instance">Object to get property from.</param>
        /// <returns>Value of property.</returns>
        private static IValue CSharpProperty(BCSharpType type, BAtom name, IValue instance) => CreateValue(
            type.Value.TryGetProperty(name.Value).GetValue(instance?.ToCSharp())
        );

        /// <summary>
        /// Sets the value of a C# property.
        /// </summary>
        /// <param name="type">Type property is on.</param>
        /// <param name="name">Name of property.</param>
        /// <param name="instance">Object to set property on.</param>
        /// <param name="value">New value of property.</param>
        private static void AssignCSharpProperty(BCSharpType type, BAtom name, IValue instance, IValue value) => type.Value.TryGetProperty(name.Value).SetValue(instance?.ToCSharp(), value.ToCSharp());

        /// <summary>
        /// Get the value of a C# field.
        /// </summary>
        /// <param name="type">Type field is on.</param>
        /// <param name="name">Name of field.</param>
        /// <param name="instance">Object to get field from.</param>
        /// <returns>Value of field.</returns>
        private static IValue CSharpField(BCSharpType type, BAtom name, IValue instance) => CreateValue(
            type.Value.TryGetField(name.Value).GetValue(instance?.ToCSharp())
        );

        /// <summary>
        /// Sets the value of a C# field.
        /// </summary>
        /// <param name="type">Type field is on.</param>
        /// <param name="name">Name of field.</param>
        /// <param name="instance">Object to set field on.</param>
        /// <param name="value">New value of field.</param>
        private static void AssignCSharpField(BCSharpType type, BAtom name, IValue instance, IValue value) => type.Value.TryGetField(name.Value).SetValue(instance?.ToCSharp(), value.ToCSharp());

        /// <summary>
        /// Add Cauliflower native types to interpreter.
        /// </summary>
        /// <param name="cauliflower">The Cauliflower interpreter to use.</param>
        /// <param name="type">Type containing wanted types.</param>
        /// <param name="space">Namespace to set value on.</param>
        private static void AddTypes(Interpreter cauliflower, Type type, Scope.Tree<string, Scope> space) {
            var name = type.Name.Replace('_', '-');
            if (!(space.ContainsKey(name))) {
                space[name] = new Scope.Tree<string, Scope> { Value = new CauliflowerScope() };
            }
            foreach (var nested in type.GetNestedTypes(BindingFlags.Public).Where(t => !t.Name.Contains("<"))) {
                AddTypes(cauliflower, nested, space[name]);
                if (nested.GetInterface("IValue") != null) {
                    var property = nested.GetProperty("interpreter", BindingFlags.Static);
                    if (property?.PropertyType == typeof(Interpreter))
                        property.SetValue(null, cauliflower);
                    space.Value.Types[nested.Name.Replace('_', '-')] = (BCauliflowerType)nested;
                }
            }
            foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Static))
                space[name].Value.Functions[method.Name.Replace('_', '-')] = new Function(
                    name, ~0, (_, innerArgs) => CauliflowerMethod(type, method.Name, null, innerArgs)
                );
        }

        /// <summary>
        /// A dictionary containing all the builtin functions of Cauliflower.
        /// </summary>
        private new static readonly Dictionary<string, IFunction> StaticBuiltins = new Dictionary<string, IFunction> {
            {"", new ShortCircuitFunction("", 1, (cauliflower, args) => {
                var e = (ValueExpression) args[0];
                if (e.IsValue)
                    return cauliflower.Run(e.Values.First());
                if (e.Values.Length == 0)
                    throw new Exception("Expected function name");
                var first = cauliflower.Run(e.Values[0]);
                if (first is BCauliflowerMethod cauliflowerMethod)
                    try {
                        return CauliflowerMethod(cauliflowerMethod.Value.type, cauliflowerMethod.Value.name, null, e.Values.Skip(1).Select(cauliflower.Run));
                    } catch {
                        throw new Exception($"Cauliflower method '{cauliflowerMethod.Value.name}' not found for specified arguments. Are you missing an 'import'?");
                    }
                if (first is BCSharpMethod method)
                    try {
                        return CSharpMethod(method.Value.type, method.Value.name, null, e.Values.Skip(1).Select(cauliflower.Run));
                    } catch {
                        throw new Exception($"C# method '{method.Value.name}' not found for specified arguments. Are you missing a 'c#-import'?");
                    }
                IFunction fn;
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
            {".", new ShortCircuitFunction(".", -2, (cauliflower, args) => {
                var scope = cauliflower.Scope.Namespaces;
                foreach (var (item, index) in args.SkipLast(1).WithIndex())
                    if (item is BAtom a) {
                        if (scope.ContainsKey(a.Value))
                            scope = scope[a.Value];
                        else
                            throw new Exception($"Object '{a.Value}' not found in namespace");
                    } else
                        throw new ArgumentTypeException(item, "atom", index + 1, ".");
                if (scope.Value == null)
                    throw new Exception($"Scope '{string.Join('.', args.Select(arg => arg.Inspect()))}' does not exist");
                switch (args.Last()) {
                    case ScalarVar s:
                        return scope.Value[s];
                    case ListVar l:
                        return scope.Value[l];
                    case DictVar d:
                        return scope.Value[d];
                    case BAtom a:
                        return scope.Value[a.Value] ?? (IValue) scope[a.Value].Value;
                    default:
                        throw new ArgumentTypeException(args.Last(), "variable", args.Length, ".");
                }
            })},
            {"->", new ShortCircuitFunction("->", -2, (cauliflower, args) => {
                var result = cauliflower.Run(args[0]);
                var sharpType = result as BCSharpType;
                var type = sharpType != null ? sharpType.Value : result.Type();
                foreach (var (item, index) in args.Skip(1).WithIndex())
                    if (type == null)
                        throw new Exception("Cannot get property of method");
                    else if (item is BAtom a) {
                        var name = a.Value;
                        var members = type.GetMember(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
                        if (members.Length != 0) {
                            if (members.Length > 1) {
                                var _type = type;
                                var _name = name;
                                var _result = result;
                                // ReSharper disable once ConvertIfStatementToConditionalTernaryExpression
                                if (!type.IsSubclassOf(typeof(IValue)))
                                    result = new Function(name, ~0, (_, innerArgs) => CSharpMethod(_type, _name, _result, innerArgs));
                                else
                                    result = new Function(name, ~0, (_, innerArgs) => CauliflowerMethod(_type, _name, _result, innerArgs));
                                break;
                            }
                            switch (members[0].MemberType) {
                                case MemberTypes.Method:
                                    if (!methods.ContainsKey(type))
                                        methods[type] = new Dictionary<string, MethodBase>();
                                    if (!methods[type].ContainsKey(name))
                                        methods[type][name] = type.GetMethod(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
                                    var method = methods[type][name];
                                    var obj = result;
                                    result = new Function(name, -1, (_, innerArgs) => CreateValue(method.Invoke(obj, innerArgs)));
                                    type = null;
                                    break;
                                case MemberTypes.Field:
                                    if (!fields.ContainsKey(type))
                                        fields[type] = new Dictionary<string, FieldInfo>();
                                    if (!fields[type].ContainsKey(name))
                                        fields[type][name] = type.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
                                    var field = fields[type][name];
                                    type = (result = CreateValue(field.GetValue(result is BCSharpValue ? result.ToCSharp() : result))).GetType();
                                    break;
                                case MemberTypes.Property:
                                    if (!properties.ContainsKey(type))
                                        properties[type] = new Dictionary<string, PropertyInfo>();
                                    if (!properties[type].ContainsKey(name))
                                        properties[type][name] = type.GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
                                    var property = properties[type][name];
                                    type = (result = CreateValue(property.GetValue(result is BCSharpValue ? result.ToCSharp() : result))).GetType();
                                    break;
                            }
                        } else
                            throw new Exception($"Member '{a.Value}' not found in item");
                    } else
                        throw new ArgumentTypeException(item, "atom", index + 1, "");
                return result;
            })},
            {"$", new Function("$", 1, (cauliflower, args) => args[0] is IScalar ? args[0] : args[0].ScalarContext())},
            {"@", new Function("@", 1, (cauliflower, args) => {
                if (args[0] is IList)
                    return args[0];
                if (!(args[0] is BCSharpValue))
                    return args[0].ListContext();
                var value = args[0].ToCSharp();
                if (value.GetType().GetInterface("IEnumerable") != null) {
                    var result = new BList();
                    foreach (var item in (IEnumerable) value)
                        result.Add(CreateValue(item));
                    return result;
                }
                return args[0].ListContext();
            })},
            {"%", new Function("%", 1, (cauliflower, args) => {
                if (args[0] is IDictionary)
                    return args[0];
                if (!(args[0] is BCSharpValue))
                    return args[0].DictionaryContext();
                var value = args[0].ToCSharp();
                if (value.GetType().GetInterface("IDictionary") != null)
                    return new BDictionary(((IDictionary) value).ToDictionary(kvp => CreateValue(kvp.Key), kvp => CreateValue(kvp.Value)));
                throw new ArgumentTypeException(args[0], "list", 1, "%");
            })},
            {"fn", new ShortCircuitFunction("fn", -3, (cauliflower, args) => {
                if (!(cauliflower.Run(args[0]) is BAtom name))
                    throw new ArgumentTypeException(args[0], "atom", 1, "fn");
                if (!(args[1] is ValueExpression argExpressions)) {
                    switch (args[1]) {
                        case ScalarVar s:
                            argExpressions = new ValueExpression(s);
                            break;
                        case ListVar l:
                            argExpressions = new ValueExpression(l);
                            break;
                        default:
                            throw new ArgumentTypeException(args[1], "expression", 2, "fn");
                    }
                }
                var argNames = argExpressions.Values.ToList();
                if (argNames.Take(argNames.Count - 1).OfType<ValueExpression>().Any()) {
                    throw new ArgumentTypeException("Received expression instead of variable name in argument list for 'fn'");
                }
                var length = argNames.Count;
                IValue varargs = null;
                if (argNames.Count != 0) {
                    switch (argNames.Last()) {
                        case ListVar _:
                        case ScalarVar _:
                        case DictVar _:
                        case BAtom _:
                            break;
                        case ValueExpression expr when expr.Values.Length == 1 && expr.Values[0] is ListVar l:
                            length = -length;
                            varargs = l;
                            break;
                        default:
                            throw new ArgumentTypeException("Received expression instead of variable name in argument list for 'fn'");
                    }
                }
                if (varargs != null)
                    argNames.RemoveAt(argNames.Count - 1);
                var statements = args.Skip(2);
                cauliflower.Scope.Functions[name.Value] = new Function(name.Value, length, (_, innerArgs) => {
                    cauliflower.Scope = new CauliflowerScope(cauliflower.Scope);
                    for (var i = 0; i < argNames.Count; i++) {
                        var toAssign = innerArgs[i];
                        switch (argNames[i]) {
                            case ScalarVar s:
                                if (!(toAssign is IScalar scalar))
                                    throw new Exception("Only scalars can be assigned to scalar ($) variables");
                                cauliflower.Scope[s] = scalar;
                                break;
                            case ListVar l:
                                if (!(toAssign is IList list))
                                    throw new Exception("Only lists can be assigned to list (@) variables");
                                cauliflower.Scope[l] = list;
                                break;
                            case DictVar d:
                                if (!(toAssign is IDictionary dict))
                                    throw new Exception("Only dictionaries can be assigned to dictionary (%) variables");
                                cauliflower.Scope[d] = dict;
                                break;
                            case BAtom a:
                                if (!(toAssign is IFunction fn))
                                    throw new Exception("Only Functions can be assigned to atom variables");
                                cauliflower.Scope[a] = fn;
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
                }, argExpressions.Values);
                return null;
            })},
            {"call", new Function("call", 2, (cauliflower, args) => {
                if (!(args[0] is BAtom))
                    throw new ArgumentTypeException(args[0], "atom", 1, "call");
                if (!(args[1] is IList l))
                    throw new ArgumentTypeException(args[0], "list", 2, "call");
                return StaticBuiltins[""].Invoke(cauliflower, new ValueExpression(new List<IValueExpressible>{ args[0] }.Concat(l)));
            })},

            // Meta-commands
            {"new", new Function("new", -2, (cauliflower, args) => {
                var type = cauliflower.Run(args[0]);
                if (type is BCauliflowerType ctype)
                    return CauliflowerCreate(ctype, args.Skip(1));
                if (type is BCSharpType cstype)
                    return CSharpCreate(cstype, args.Skip(1));
                throw new ArgumentTypeException(args[0], "Cauliflower or C# type", 1, "new");
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
            {"import", new Function("import", ~1, (cauliflower, args) => {
                if (args[0] is BAtom a) {
                    if (args.Length == 1) {
                        CSharpImport(cauliflower, a);
                        return null;
                    }

                    CauliflowerInline.Import(cauliflower, args);
                    return null;
                }
                if (args.Length != 1)
                    throw new Exception("Function 'import' requires exactly 1 argument, 3 provided");
                if (!(args[0] is BString s))
                    throw new ArgumentTypeException(args[0], "string", 1, "import");

                var tempCauliflower = new CauliflowerInterpreter();
                tempCauliflower.Run(File.ReadAllText(s.Value));
                foreach (var (key, value) in tempCauliflower.Scope.Functions)
                    cauliflower.Scope[key] = value;
                return null;
            })},
            {"import-static", new Function("import-static", ~1, (cauliflower, args) => {
                if (!(args[0] is BAtom))
                    throw new ArgumentTypeException(args[0], "atom", 1, "import-static");
                CauliflowerInline.Import(cauliflower, args, true);
                return null;
            })},

            {":=", new ShortCircuitFunction(":=", ~1, (cauliflower, args) => {
                IValue result = null;
                for (var i = 0; i < args.Length; i += 2) {
                    result = i + 1 < args.Length ? cauliflower.Run(args[i + 1]) : null;
                    switch (args[i]) {
                        case ScalarVar _:
                            if (result == null)
                                throw new Exception("Scalar has no default value to be assigned");
                            if (!(result is IScalar scalar))
                                throw new Exception("Only scalars can be assigned to scalar ($) variables");
                            args[i + 1] = scalar;
                            break;
                        case ListVar _:
                            if (result == null) {
                                args = args.Concat(new[] { result = new BList()}).ToArray();
                                break;
                            }
                            if (!(result is IList list))
                                throw new Exception("Only lists can be assigned to list (@) variables");
                            args[i + 1] = list;
                            break;
                        case DictVar _:
                            if (result == null) {
                                args = args.Concat(new[] { result = new BDictionary()}).ToArray();
                                break;
                            }
                            if (!(result is IDictionary dict))
                                throw new Exception("Only dicts can be assigned to dict (%) variables");
                            args[i + 1] = dict;
                            break;
                        case BAtom _:
                            if (result == null) {
                                args = args.Concat(new[] { result = Noop }).ToArray();
                                break;
                            }
                            if (!(result is IFunction fn))
                                throw new Exception("Only functions can be assigned to atom variables");
                            args[i + 1] = fn;
                            break;
                        case ValueExpression v:
                            if (!(v.Values[0] is BAtom arrow && arrow.Value == "->"))
                                throw new Exception("Assignments to an expression must be assignments to fields or properties");

                            var target = cauliflower.Run(v.Values[1]);
                            if (!(v.Values[2] is BAtom memberName))
                                throw new ArgumentTypeException(v.Values[2], "atom", 3, "-> inside :=");

                            var members = target.GetType().GetMember(memberName.Value, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
                            if (members.Length == 0)
                                throw new Exception($"Unable to find member '{memberName.Value}'");

                            switch (members[0]) {
                                case FieldInfo f:
                                    f.SetValue(target, result is BCSharpValue ? result.ToCSharp() : result);
                                    break;
                                case PropertyInfo p:
                                    p.SetValue(target, result is BCSharpValue ? result.ToCSharp() : result);
                                    break;
                                default:
                                    throw new Exception("Values can only be assigned to fields or properties");
                            }
                            break;
                        default:
                            throw new Exception("Values can only be assigned to scalar ($), list (@), dictionary (%) or atom variables");
                    }
                }
                for (var i = 0; i < args.Length; i += 2) {
                    switch (args[i]) {
                        case ScalarVar s:
                            cauliflower.Scope[s] = (IScalar) args[i + 1];
                            break;
                        case ListVar l:
                            cauliflower.Scope[l] = (IList) args[i + 1];
                            break;
                        case DictVar d:
                            cauliflower.Scope[d] = (IDictionary) args[i + 1];
                            break;
                        case BAtom a:
                            cauliflower.Scope[a] = (IFunction) args[i + 1];
                            break;
                    }
                }
                return result;
            })},

            // OOP
            {"namespace", new ShortCircuitFunction("namespace", ~0, (cauliflower, args) => {
                if (!(cauliflower.Run(args[0]) is BAtom a))
                    throw new ArgumentTypeException(args[0], "atom", 1, "namespace");
                var tempCauliflower = new CauliflowerInterpreter();
                foreach (var arg in args.Skip(1))
                    tempCauliflower.Run(arg);
                if (cauliflower.Scope.Namespaces.ContainsKey(a.Value))
                    cauliflower.Scope.Namespaces[a.Value].Add(tempCauliflower.Scope.Namespaces);
                else
                    cauliflower.Scope.Namespaces[a.Value] = tempCauliflower.Scope.Namespaces;
                var scope = cauliflower.Scope.Namespaces[a.Value].Value = (cauliflower.Scope.Namespaces[a.Value].Value ?? new CauliflowerScope());
                foreach (var (key, value) in tempCauliflower.Scope.Functions)
                    scope[key] = value;
                return null;
            })},
            {"class", new ShortCircuitFunction("class", ~0, (cauliflower, args) => {
                if (!(args[0] is TypeName name))
                    throw new ArgumentTypeException(args[0], "type name", 1, "class");

                bool hasCustomScalarContext = false, hasCustomListContext = false, hasCustomDictionaryContext = false, isScalar = true, isList = false, isDictionary = false;

                var supertypes = new List<Type>();

                if (!(args[1] is ValueExpression _)) {
                    if (!(args[1] is BAtom isAtom && isAtom.Value == "is"))
                        throw new ArgumentTypeException(args[1], "statement or `is`", 2, "class");
                    if (!(args[2] is ValueExpression superVexp))
                        throw new ArgumentTypeException(args[2], "expression of supertypes", 3, "class");

                    supertypes = superVexp.Values.Select(v => {
                        if (!(v is TypeName t))
                            throw new ArgumentTypeException(v, "supertype name", 4, "class");

                        return (Type) (BCSharpType) cauliflower.Run((IValueExpressible) t);
                    }).ToList();

                    args = args.Skip(2).ToArray();
                }

                if (supertypes.Contains(typeof(IList))) {
                    isList = true;
                    isScalar = false;
                } else if (supertypes.Contains(typeof(IDictionary))) {
                    isDictionary = true;
                    isScalar = false;
                } else if (!supertypes.Contains(typeof(IScalar))) {
                    supertypes.Add(typeof(IScalar));
                }

                // Generate initial class
                var asmName = new AssemblyName("CauliflowerGenerated-" + name.Value);
                var asmBuilder = AssemblyBuilder.DefineDynamicAssembly(asmName, AssemblyBuilderAccess.Run);
                var modBuilder = asmBuilder.DefineDynamicModule(asmName.Name);
                var typeBuilder = modBuilder.DefineType(
                    name.Value,
                    TypeAttributes.Public | TypeAttributes.Class,
                    null,
                    supertypes.ToArray()
                );

                // Validate + collate statements
                var statements = CauliflowerInline.GetStatements(args.Skip(1));

                // Generate constructor params
                var ctorParamTuple = statements.FirstOrDefault(s => s.Item1 == "fn" && s.Item3.Values.ElementAtOrDefault(0) is BAtom fnName && fnName.Value == "init");
                var isCustomCtor =
                    !ctorParamTuple.Equals(default((string, List<string>, ValueExpression)));
                var ctorParams = Type.EmptyTypes;
                var ctorParamDecl = new ValueExpression();
                if (isCustomCtor) {
                    if (!(ctorParamTuple.Item3.Values.ElementAtOrDefault(1) is ValueExpression vexp))
                        throw new ArgumentTypeException(ctorParamTuple.Item3.Values.ElementAtOrDefault(1), "expression", 2, "fn");
                    ctorParams = CauliflowerInline.GetParameterTypes(vexp);
                    ctorParamDecl = vexp;
                }

                // Interface implementations
                var toCSharp = typeBuilder.DefineMethod(
                    "ToCSharp",
                    MethodAttributes.Public | MethodAttributes.Virtual,
                    typeof(object),
                    Type.EmptyTypes
                );
                var toCSharpIL = toCSharp.GetILGenerator();
                toCSharpIL.Emit(OpCodes.Ldarg_0);
                toCSharpIL.Emit(OpCodes.Ret);
                typeBuilder.DefineMethodOverride(toCSharp, typeof(IValue).GetMethod("ToCSharp"));

                var type = typeBuilder.DefineMethod(
                    "Type",
                    MethodAttributes.Public | MethodAttributes.Virtual,
                    typeof(Type),
                    Type.EmptyTypes
                );
                var typeIL = type.GetILGenerator();
                typeIL.Emit(OpCodes.Ldarg_0);
                typeIL.Emit(OpCodes.Call, typeof(object).GetMethod("GetType"));
                typeIL.Emit(OpCodes.Ret);
                typeBuilder.DefineMethodOverride(type, typeof(IValue).GetMethod("Type"));

                var inspect = typeBuilder.DefineMethod(
                    "Inspect",
                    MethodAttributes.Virtual,
                    typeof(string),
                    Type.EmptyTypes
                );
                var inspectIL = inspect.GetILGenerator();
                inspectIL.Emit(OpCodes.Ldstr, $"<{name.Value}>");
                inspectIL.Emit(OpCodes.Ret);
                typeBuilder.DefineMethodOverride(inspect, typeof(IValueExpressible).GetMethod("Inspect"));

                MethodAttributes MethodAttrFromMod(string modifier) {
                    switch (modifier) {
                        case "public":
                            return MethodAttributes.Public;
                        case "private":
                            return MethodAttributes.Private;
                        case "protected":
                            return MethodAttributes.Family;
                        case "static":
                            return MethodAttributes.Static;
                        default:
                            throw new Exception($"Unrecognized access modifier '{modifier}'");
                    }
                }

                MethodAttributes MethodAttrsFromAllMods(IEnumerable<string> modifiers, MethodAttributes defaultAttr = MethodAttributes.Public, bool isConstructor = false) {
                    modifiers = modifiers.Where(m => m != "operator");
                    if (!modifiers.Any()) return isConstructor ? defaultAttr : defaultAttr | MethodAttributes.Virtual;
                    var attrs = modifiers.Select(MethodAttrFromMod).Aggregate((a, m) => a | m);

                    var attrsWithDefault = (modifiers.Any(accessControlModifiers.Contains)
                        ? attrs
                        : defaultAttr | attrs);
                    return isConstructor ? attrsWithDefault : attrsWithDefault | MethodAttributes.Virtual; // All methods are virtual in Cauliflower
                }

                FieldAttributes FieldAttrFromMod(string modifier) {
                    switch (modifier) {
                        case "public":
                            return FieldAttributes.Public;
                        case "private":
                            return FieldAttributes.Private;
                        case "protected":
                            return FieldAttributes.Family;
                        case "static":
                            return FieldAttributes.Static;
                        default:
                            throw new Exception($"Unrecognized access modifier '{modifier}'");
                    }
                }

                FieldAttributes FieldAttrsFromAllMods(IEnumerable<string> modifiers) {
                    if (!modifiers.Any()) return FieldAttributes.Private;
                    var attrs = modifiers.Select(FieldAttrFromMod).Aggregate((a, m) => a | m);
                    return modifiers.Any(accessControlModifiers.Contains)
                        ? attrs
                        : FieldAttributes.Private | attrs;
                }

                if (ctorParamTuple.Item2 != null && ctorParamTuple.Item2.Contains("static"))
                    throw new Exception($"Custom static constructors currently not allowed (in class '{name.Value}')");

                // Generate constructor
                var ctor = typeBuilder.DefineConstructor(
                    MethodAttrsFromAllMods(ctorParamTuple.Item2 ?? new List<string>(), isConstructor: true),
                    CallingConventions.HasThis,
                    ctorParams
                );
                var ctorIL = ctor.GetILGenerator();
                ctorIL.Emit(OpCodes.Ldarg_0);
                ctorIL.Emit(OpCodes.Call, typeof(object).GetConstructor(Type.EmptyTypes));

                // Add (interpreter) field
                var interpreterField = typeBuilder.DefineField(
                    "(interpreter)",
                    typeof(CauliflowerInterpreter),
                    FieldAttributes.Private | FieldAttributes.Static
                );

                // Generate static initializer
                var staticInit = typeBuilder.DefineMethod(
                    "(init)",
                    MethodAttributes.Private | MethodAttributes.Static,
                    typeof(void),
                    new [] {typeof(CauliflowerInterpreter)}
                );
                var staticInitIL = staticInit.GetILGenerator();
                staticInitIL.Emit(OpCodes.Ldarg_0);
                staticInitIL.Emit(OpCodes.Stsfld, interpreterField);

                foreach (var (sName, sModifiers, sArgs) in statements) {
                    // Console.WriteLine(string.Join(", ", sName, $"[{string.Join(", ", sModifiers)}]", sArgs));

                    var isStatic = sModifiers.Contains("static");

                    switch (sName) {
                        case "field":
                            if (!(sArgs.Values.ElementAtOrDefault(0) is BAtom fieldName))
                                throw new ArgumentTypeException(sArgs.Values.ElementAtOrDefault(0), "atom", 1, "field");

                            var newField = typeBuilder.DefineField(
                                fieldName.Value,
                                typeof(IValue),
                                FieldAttrsFromAllMods(sModifiers)
                            );

                            var initFieldVal = sArgs.Values.ElementAtOrDefault(1);
                            if (initFieldVal != null) {
                                // Init value inside constructor
                                var initCtorIL = isStatic ? staticInitIL : ctorIL;

                                if (!isStatic) initCtorIL.Emit(OpCodes.Ldarg_0);
                                CauliflowerInline.LoadInterpreterInvocation(initCtorIL, interpreterField, initFieldVal);
                                initCtorIL.Emit(isStatic ? OpCodes.Stsfld : OpCodes.Stfld, newField);
                            }

                            // Console.WriteLine($"field {fieldName.Value} attrs: {newField.Attributes}");

                            break;
                        case "prop":
                        case "property":
                            if (!(sArgs.Values.ElementAtOrDefault(0) is BAtom propName))
                                throw new ArgumentTypeException(sArgs.Values.ElementAtOrDefault(0), "atom", 1, "prop");

                            var newProp = typeBuilder.DefineProperty(
                                propName.Value,
                                PropertyAttributes.None,
                                typeof(IValue),
                                Type.EmptyTypes
                            );

                            if (!(sArgs.Values.ElementAtOrDefault(1) is ValueExpression exp))
                                throw new ArgumentTypeException(sArgs.Values.ElementAtOrDefault(1), "expression", 2, "prop");

                            var propStatements = CauliflowerInline.GetStatements(exp.Values);

                            var propModifier =
                                MethodAttrFromMod(sModifiers.FirstOrDefault(accessControlModifiers.Contains) ?? "public");
                            bool? hasBody = null;

                            var backingField = typeBuilder.DefineField(
                                propName.Value + "(backing)",
                                typeof(IValue),
                                FieldAttrsFromAllMods(sModifiers)
                            );

                            if (propStatements.All(a => a.Item2?.FirstOrDefault(accessControlModifiers.Contains) != null))
                                throw new Exception($"Cannot specify access modifiers for all accessors of property '{propName.Value}'");

                            foreach (var (aType, aModifiers, aArgs) in propStatements) {
                                // Make sure nobody's trying to combine auto-properties and bodies
                                var argsEmpty = aArgs.Values.Length <= 0;
                                if (hasBody.HasValue) {
                                    if (argsEmpty == hasBody.Value)
                                        throw new Exception("Property definitions cannot have both auto-accessors and accessor bodies");
                                } else {
                                    hasBody = !argsEmpty;
                                }

                                var accessModifier =
                                    aModifiers.FirstOrDefault(accessControlModifiers.Contains);

                                // Make sure that the accessor modifiers are more restrictive than the property modifier
                                if (accessModifier != null &&
                                    Array.IndexOf(accessControlModifiers, accessModifier) <=
                                    Array.IndexOf(accessControlModifiers,
                                        sModifiers.FirstOrDefault(accessControlModifiers
                                            .Contains) ?? "public"))
                                    throw new Exception(
                                        $"Access modifier for {aType} accessor of '{propName.Value}' must be more restrictive than modifier for '{propName.Value}'");

                                switch (aType) {
                                    case "get":
                                        var getter = typeBuilder.DefineMethod(
                                            "get_" + propName.Value,
                                            MethodAttrsFromAllMods(aModifiers, propModifier | MethodAttributes.SpecialName | MethodAttributes.HideBySig),
                                            typeof(IValue),
                                            Type.EmptyTypes
                                        );

                                        var getterIL = getter.GetILGenerator();
                                        if (argsEmpty) {
                                            if (!isStatic) getterIL.Emit(OpCodes.Ldarg_0);
                                            getterIL.Emit(isStatic ? OpCodes.Ldsfld : OpCodes.Ldfld, backingField);
                                        } else {
                                            CauliflowerInline.CreateNewScope(getterIL, interpreterField);
                                            if (!isStatic) CauliflowerInline.AddThisToScope(getterIL, interpreterField, isScalar, isList, isDictionary);
                                            CauliflowerInline.LoadInterpreterInvocation(getterIL, interpreterField, aArgs.Values);
                                            CauliflowerInline.ReturnToParentScope(getterIL, interpreterField);
                                        }
                                        getterIL.Emit(OpCodes.Ret);

                                        newProp.SetGetMethod(getter);

                                        // Console.WriteLine($"{propName.Value} getter attrs: {getter.Attributes}");
                                        break;
                                    case "set":
                                        var setter = typeBuilder.DefineMethod(
                                            "set_" + propName.Value,
                                            MethodAttrsFromAllMods(aModifiers, propModifier | MethodAttributes.SpecialName | MethodAttributes.HideBySig),
                                            null,
                                            new [] { typeof(IValue) }
                                        );

                                        var setterIL = setter.GetILGenerator();
                                        if (argsEmpty) {
                                            setterIL.Emit(OpCodes.Ldarg_0); // Arg 0 is this for instance, value for static
                                            if (!isStatic) setterIL.Emit(OpCodes.Ldarg_1); // Arg 1 is value for instance, undefined for static
                                            setterIL.Emit(isStatic ? OpCodes.Stsfld : OpCodes.Stfld, backingField);
                                        } else {
                                            CauliflowerInline.CreateNewScope(setterIL, interpreterField);
                                            if (!isStatic) CauliflowerInline.AddThisToScope(setterIL, interpreterField, isScalar, isList, isDictionary);
                                            CauliflowerInline.LoadInterpreterInvocation(setterIL, interpreterField, aArgs.Values);
                                            setterIL.Emit(OpCodes.Pop);
                                            CauliflowerInline.ReturnToParentScope(setterIL, interpreterField);
                                        }
                                        setterIL.Emit(OpCodes.Ret);

                                        newProp.SetSetMethod(setter);

                                        // Console.WriteLine($"{propName.Value} setter attrs: {setter.Attributes}");
                                        break;
                                    default:
                                        throw new Exception($"Unrecognized property accessor '{aType}'");
                                }
                            }

                            var initPropVal = sArgs.Values.ElementAtOrDefault(2);

                            // Init value inside constructor
                            if (initPropVal != null) {
                                if (propStatements.Any(a => a.Item3.Values.Length > 0))
                                    throw new Exception($"Only auto-properties can have initial values ('{propName.Value}')");

                                var initCtorIL = isStatic ? staticInitIL : ctorIL;

                                if (!isStatic) initCtorIL.Emit(OpCodes.Ldarg_0);
                                CauliflowerInline.CreateNewScope(initCtorIL, interpreterField);
                                CauliflowerInline.LoadInterpreterInvocation(initCtorIL, interpreterField, initPropVal);
                                CauliflowerInline.ReturnToParentScope(initCtorIL, interpreterField);
                                initCtorIL.Emit(isStatic ? OpCodes.Stsfld : OpCodes.Stfld, backingField);
                            }
                            break;
                        case "fn":
                        case "fun":
                        case "func":
                        case "function":
                            if (!(sArgs.Values.ElementAtOrDefault(0) is BAtom fnName))
                                throw new ArgumentTypeException(sArgs.Values.ElementAtOrDefault(0), "atom", 1, "fn");
                            if (!(sArgs.Values.ElementAtOrDefault(1) is ValueExpression vexp2))
                                throw new ArgumentTypeException(sArgs.Values.ElementAtOrDefault(1), "expression", 2, "fn");

                            MethodBuilder newMethod;

                            if (sModifiers.Contains("operator")) {
                                switch (fnName.Value) {
                                    case "scalar":
                                        if (isScalar)
                                            throw new Exception("A scalar cannot have a custom scalar context");
                                        if (vexp2.Values.Length != 0)
                                            throw new Exception("Custom scalar contexts cannot have arguments");
                                        newMethod = typeBuilder.DefineMethod(
                                            "ScalarContext",
                                            MethodAttrsFromAllMods(sModifiers),
                                            typeof(IScalar),
                                            Type.EmptyTypes
                                        );
                                        typeBuilder.DefineMethodOverride(newMethod, typeof(IValue).GetMethod("ScalarContext"));
                                        hasCustomScalarContext = true;
                                        break;
                                    case "list":
                                        if (isList)
                                            throw new Exception("A list cannot have a custom list context");
                                        if (vexp2.Values.Length != 0)
                                            throw new Exception("Custom list contexts cannot have arguments");
                                        newMethod = typeBuilder.DefineMethod(
                                            "ListContext",
                                            MethodAttrsFromAllMods(sModifiers),
                                            typeof(IList),
                                            Type.EmptyTypes
                                        );
                                        typeBuilder.DefineMethodOverride(newMethod, typeof(IValue).GetMethod("ListContext"));
                                        hasCustomListContext = true;
                                        break;
                                    case "dictionary":
                                        if (isDictionary)
                                            throw new Exception("A dictionary cannot have a custom dictionary context");
                                        if (vexp2.Values.Length != 0)
                                            throw new Exception("Custom dictionary contexts cannot have arguments");
                                        newMethod = typeBuilder.DefineMethod(
                                            "DictionaryContext",
                                            MethodAttrsFromAllMods(sModifiers),
                                            typeof(IDictionary),
                                            Type.EmptyTypes
                                        );
                                        typeBuilder.DefineMethodOverride(newMethod, typeof(IValue).GetMethod("DictionaryContext"));
                                        hasCustomDictionaryContext = true;
                                        break;
                                    default:
                                        throw new Exception($"Operator '{fnName.Value}' not supported");
                                }
                            } else {
                                if (fnName.Value == "init") continue;
                                newMethod = typeBuilder.DefineMethod(
                                    fnName.Value,
                                    MethodAttrsFromAllMods(sModifiers),
                                    typeof(IValue),
                                    CauliflowerInline.GetParameterTypes(vexp2)
                                );
                            }
                            var methodIL = newMethod.GetILGenerator();

                            // Add new function scope + populate
                            CauliflowerInline.CreateNewScope(methodIL, interpreterField);
                            if (!isStatic) CauliflowerInline.AddThisToScope(methodIL, interpreterField, isScalar, isList, isDictionary);
                            CauliflowerInline.AddParametersToScope(methodIL, interpreterField, vexp2, isStatic);

                            // Invoke interpreter, back out of scope, return
                            CauliflowerInline.LoadInterpreterInvocation(methodIL, interpreterField, sArgs.Values.Skip(2));
                            CauliflowerInline.ReturnToParentScope(methodIL, interpreterField);
                            methodIL.Emit(OpCodes.Ret);

                            // Console.WriteLine($"method {fnName.Value} attrs: {newMethod.Attributes}");
                            break;
                        default:
                            throw new Exception($"Unrecognized class definition statement '{sName}'");
                    }
                }

                if (isScalar) {
                    var defaultContext = typeBuilder.DefineMethod(
                        "ScalarContext",
                        MethodAttributes.Public | MethodAttributes.Virtual,
                        typeof(IScalar),
                        Type.EmptyTypes
                    );
                    var defaultContextIL = defaultContext.GetILGenerator();
                    defaultContextIL.Emit(OpCodes.Ldarg_0);
                    defaultContextIL.Emit(OpCodes.Ret);
                    typeBuilder.DefineMethodOverride(defaultContext, typeof(IValue).GetMethod("ScalarContext"));

                    if (!hasCustomListContext) {
                        var defaultListContext = typeBuilder.DefineMethod(
                            "ListContext",
                            MethodAttributes.Public | MethodAttributes.Virtual,
                            typeof(IList),
                            Type.EmptyTypes
                        );
                        var defaultListContextIL = defaultListContext.GetILGenerator();
                        defaultListContextIL.Emit(OpCodes.Ldarg_0);
                        defaultListContextIL.Emit(OpCodes.Newobj, typeof(NoListContextException).GetConstructor(new[] {typeof(object)}));
                        defaultListContextIL.Emit(OpCodes.Throw);
                        typeBuilder.DefineMethodOverride(defaultListContext, typeof(IValue).GetMethod("ListContext"));
                    }

                    if (!hasCustomDictionaryContext) {
                        var defaultDictionaryContext = typeBuilder.DefineMethod(
                            "DictionaryContext",
                            MethodAttributes.Public | MethodAttributes.Virtual,
                            typeof(IDictionary),
                            Type.EmptyTypes
                        );
                        var defaultDictionaryContextIL = defaultDictionaryContext.GetILGenerator();
                        defaultDictionaryContextIL.Emit(OpCodes.Ldarg_0);
                        defaultDictionaryContextIL.Emit(OpCodes.Newobj, typeof(NoDictionaryContextException).GetConstructor(new[] {typeof(object)}));
                        defaultDictionaryContextIL.Emit(OpCodes.Throw);
                        typeBuilder.DefineMethodOverride(defaultDictionaryContext, typeof(IValue).GetMethod("DictionaryContext"));
                    }
                }

                if (isList) {
                    var defaultContext = typeBuilder.DefineMethod(
                        "ListContext",
                        MethodAttributes.Public | MethodAttributes.Virtual,
                        typeof(IList),
                        Type.EmptyTypes
                    );
                    var defaultContextIL = defaultContext.GetILGenerator();
                    defaultContextIL.Emit(OpCodes.Ldarg_0);
                    defaultContextIL.Emit(OpCodes.Ret);
                    typeBuilder.DefineMethodOverride(defaultContext, typeof(IValue).GetMethod("ListContext"));

                    if (!hasCustomScalarContext) {
                        var defaultScalarContext = typeBuilder.DefineMethod(
                            "ScalarContext",
                            MethodAttributes.Public | MethodAttributes.Virtual,
                            typeof(IScalar),
                            Type.EmptyTypes
                        );
                        var defaultScalarContextIL = defaultScalarContext.GetILGenerator();
                        defaultScalarContextIL.Emit(OpCodes.Ldfld, typeBuilder.UnderlyingSystemType.GetField("Count"));
                        defaultScalarContextIL.Emit(OpCodes.Newobj, typeof(BInteger).GetConstructor(new[] { typeof(int) }));
                        defaultScalarContextIL.Emit(OpCodes.Ret);
                        typeBuilder.DefineMethodOverride(defaultScalarContext, typeof(IValue).GetMethod("ScalarContext"));
                    }

                    if (!hasCustomDictionaryContext) {
                        var defaultDictionaryContext = typeBuilder.DefineMethod(
                            "DictionaryContext",
                            MethodAttributes.Public | MethodAttributes.Virtual,
                            typeof(IDictionary),
                            Type.EmptyTypes
                        );
                        var defaultDictionaryContextIL = defaultDictionaryContext.GetILGenerator();
                        var loopStart = defaultDictionaryContextIL.DefineLabel();
                        var loopEnd = defaultDictionaryContextIL.DefineLabel();
                        defaultDictionaryContextIL.Emit(OpCodes.Ldc_I4_0);
                        defaultDictionaryContextIL.Emit(OpCodes.Stloc_0);
                        defaultDictionaryContextIL.Emit(OpCodes.Newobj, typeof(BDictionary).GetConstructor(Type.EmptyTypes));
                        defaultDictionaryContextIL.Emit(OpCodes.Stloc_1);
                        defaultDictionaryContextIL.MarkLabel(loopStart);
                        defaultDictionaryContextIL.Emit(OpCodes.Ldloc_0);
                        defaultDictionaryContextIL.Emit(OpCodes.Ldfld, typeBuilder.UnderlyingSystemType.GetField("Count"));
                        defaultDictionaryContextIL.Emit(OpCodes.Ceq);
                        defaultDictionaryContextIL.Emit(OpCodes.Brtrue, loopEnd);
                        defaultDictionaryContextIL.Emit(OpCodes.Ldloc_0);
                        defaultDictionaryContextIL.Emit(OpCodes.Ldloc_0);
                        defaultDictionaryContextIL.Emit(OpCodes.Ldarg_0);
                        defaultDictionaryContextIL.Emit(OpCodes.Callvirt, typeBuilder.UnderlyingSystemType.GetMethod("get_Item"));
                        defaultDictionaryContextIL.Emit(OpCodes.Ldloc_1);
                        defaultDictionaryContextIL.Emit(OpCodes.Callvirt, typeof(BDictionary).GetMethod("set_Item"));
                        defaultDictionaryContextIL.Emit(OpCodes.Ldloc_0);
                        defaultDictionaryContextIL.Emit(OpCodes.Ldc_I4_1);
                        defaultDictionaryContextIL.Emit(OpCodes.Add);
                        defaultDictionaryContextIL.Emit(OpCodes.Stloc_0);
                        defaultDictionaryContextIL.Emit(OpCodes.Jmp, loopStart);
                        defaultDictionaryContextIL.MarkLabel(loopEnd);
                        defaultDictionaryContextIL.Emit(OpCodes.Ldloc_1);
                        defaultDictionaryContextIL.Emit(OpCodes.Ret);
                        typeBuilder.DefineMethodOverride(defaultDictionaryContext, typeof(IValue).GetMethod("DictionaryContext"));
                    }
                }

                if (isDictionary) {
                    var defaultContext = typeBuilder.DefineMethod(
                        "DictionaryContext",
                        MethodAttributes.Public | MethodAttributes.Virtual,
                        typeof(IDictionary),
                        Type.EmptyTypes
                    );
                    var defaultContextIL = defaultContext.GetILGenerator();
                    defaultContextIL.Emit(OpCodes.Ldarg_0);
                    defaultContextIL.Emit(OpCodes.Ret);
                    typeBuilder.DefineMethodOverride(defaultContext, typeof(IValue).GetMethod("DictionaryContext"));

                    if (!hasCustomScalarContext) {
                        var defaultScalarContext = typeBuilder.DefineMethod(
                            "ScalarContext",
                            MethodAttributes.Public | MethodAttributes.Virtual,
                            typeof(IScalar),
                            Type.EmptyTypes
                        );
                        var defaultScalarContextIL = defaultScalarContext.GetILGenerator();
                        defaultScalarContextIL.Emit(OpCodes.Ldfld, typeBuilder.UnderlyingSystemType.GetField("Count"));
                        defaultScalarContextIL.Emit(OpCodes.Newobj, typeof(BInteger).GetConstructor(new[] { typeof(int) }));
                        defaultScalarContextIL.Emit(OpCodes.Ret);
                        typeBuilder.DefineMethodOverride(defaultScalarContext, typeof(IValue).GetMethod("ScalarContext"));
                    }

                    if (!hasCustomListContext) {
                        var defaultListContext = typeBuilder.DefineMethod(
                            "ListContext",
                            MethodAttributes.Public | MethodAttributes.Virtual,
                            typeof(IList),
                            Type.EmptyTypes
                        );
                        var defaultListContextIL = defaultListContext.GetILGenerator();
                        var loopStart = defaultListContextIL.DefineLabel();
                        var loopEnd = defaultListContextIL.DefineLabel();
                        defaultListContextIL.Emit(OpCodes.Ldc_I4_0);
                        defaultListContextIL.Emit(OpCodes.Stloc_0);
                        defaultListContextIL.Emit(OpCodes.Newobj, typeof(BList).GetConstructor(Type.EmptyTypes));
                        defaultListContextIL.Emit(OpCodes.Stloc_1);
                        defaultListContextIL.MarkLabel(loopStart);
                        defaultListContextIL.Emit(OpCodes.Ldloc_0);
                        defaultListContextIL.Emit(OpCodes.Ldfld, typeBuilder.UnderlyingSystemType.GetField("Count"));
                        defaultListContextIL.Emit(OpCodes.Ceq);
                        defaultListContextIL.Emit(OpCodes.Brtrue, loopEnd);
                        defaultListContextIL.Emit(OpCodes.Ldloc_0);
                        defaultListContextIL.Emit(OpCodes.Ldloc_0);
                        defaultListContextIL.Emit(OpCodes.Ldfld, typeBuilder.UnderlyingSystemType.GetField("Values"));
                        defaultListContextIL.Emit(OpCodes.Ldarg_0);
                        defaultListContextIL.Emit(OpCodes.Callvirt, typeof(Dictionary<IValue, IValue>.ValueCollection).GetMethod("get_Item"));
                        defaultListContextIL.Emit(OpCodes.Ldloc_0);
                        defaultListContextIL.Emit(OpCodes.Ldfld, typeBuilder.UnderlyingSystemType.GetField("Keys"));
                        defaultListContextIL.Emit(OpCodes.Ldarg_0);
                        defaultListContextIL.Emit(OpCodes.Callvirt, typeof(Dictionary<IValue, IValue>.KeyCollection).GetMethod("get_Item"));
                        defaultListContextIL.Emit(OpCodes.Newobj, typeof(BList).GetConstructor(new[] { typeof(IValue), typeof(IValue) }));
                        defaultListContextIL.Emit(OpCodes.Ldloc_1);
                        defaultListContextIL.Emit(OpCodes.Callvirt, typeof(BList).GetMethod("Add"));
                        defaultListContextIL.Emit(OpCodes.Ldloc_0);
                        defaultListContextIL.Emit(OpCodes.Ldc_I4_1);
                        defaultListContextIL.Emit(OpCodes.Add);
                        defaultListContextIL.Emit(OpCodes.Stloc_0);
                        defaultListContextIL.Emit(OpCodes.Jmp, loopStart);
                        defaultListContextIL.MarkLabel(loopEnd);
                        defaultListContextIL.Emit(OpCodes.Ldloc_1);
                        defaultListContextIL.Emit(OpCodes.Ret);
                        typeBuilder.DefineMethodOverride(defaultListContext, typeof(IValue).GetMethod("ListContext"));
                    }
                }

                // Custom constructor implementation (after initial values)
                if (isCustomCtor) {
                    CauliflowerInline.AddThisToScope(ctorIL, interpreterField, isScalar, isList, isDictionary);
                    CauliflowerInline.AddParametersToScope(ctorIL, interpreterField, ctorParamDecl);
                    CauliflowerInline.CreateNewScope(ctorIL, interpreterField);
                    CauliflowerInline.LoadInterpreterInvocation(ctorIL, interpreterField, ctorParamTuple.Item3.Values.Skip(2));
                    ctorIL.Emit(OpCodes.Pop);
                    CauliflowerInline.ReturnToParentScope(ctorIL, interpreterField);
                }

                ctorIL.Emit(OpCodes.Ret);
                staticInitIL.Emit(OpCodes.Ret);

                var classType = typeBuilder.CreateType();

                new AssemblyGenerator().GenerateAssembly(asmBuilder, "cauliflower.dll");

                classType.GetMethod("(init)", BindingFlags.NonPublic | BindingFlags.Static)
                    .Invoke(null, new [] {cauliflower});

                cauliflower.Scope.Types[name.Value] = (BCauliflowerType) classType;

                return null;
            })},

            // I/O commands
            {"input", new Function("input", 0, (cauliflower, args) => new BString(Console.ReadLine()))},
            {"read", new Function("read", 1, (cauliflower, args) => {
                if (!(args[0] is BString path))
                    throw new ArgumentTypeException(args[0], "string", 1, "read");
                return new BString(File.ReadAllText(path.Value));
            })},
            {"write", new Function("write", 2, (cauliflower, args) => {
                if (!(args[0] is BString path))
                    throw new ArgumentTypeException(args[0], "string", 1, "write");
                if (!(args[1] is BString toWrite))
                    throw new ArgumentTypeException(args[0], "string", 2, "write");
                File.WriteAllText(path.Value, toWrite.Value);
                return null;
            })},
            {"say", new Function("say", ~0, (broccoli, args) => {
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
                Console.Write('\n');
                return null;
            })},

            // Basic math
            {"mod", new Function("mod", 2, (broccoli, args) => {
                if (!(args[0] is BInteger i))
                    throw new ArgumentTypeException(args[0], "integer", 1, "mod");
                if (!(args[1] is BInteger j))
                    throw new ArgumentTypeException(args[1], "integer", 2, "mod");

                return new BInteger(i.Value % j.Value);
            })},
            {"^", new Function("^", ~1, (broccoli, args) => {
                foreach (var (value, index) in args.WithIndex())
                    if (!(value is BInteger))
                        throw new ArgumentTypeException(value, "integer", index + 1, "^");

                return args.Aggregate(new BInteger(0L), (m, v) => new BInteger(m.Value ^ ((BInteger) v).Value));
            })},
            {"~^", new Function("~^", ~1, (broccoli, args) => {
                foreach (var (value, index) in args.WithIndex())
                    if (!(value is BInteger))
                        throw new ArgumentTypeException(value, "integer", index + 1, "~^");

                return args.Aggregate(new BInteger(~0L), (m, v) => new BInteger(~m.Value ^ ((BInteger) v).Value));
            })},
            {"|", new Function("|", ~0, (broccoli, args) => {
                foreach (var (value, index) in args.WithIndex())
                    if (!(value is BInteger))
                        throw new ArgumentTypeException(value, "integer", index + 1, "|");

                return args.Aggregate(new BInteger(0L), (m, v) => new BInteger(m.Value | ((BInteger) v).Value));
            })},
            {"&", new Function("&", ~0, (broccoli, args) => {
                foreach (var (value, index) in args.WithIndex())
                    if (!(value is BInteger))
                        throw new ArgumentTypeException(value, "integer", index + 1, "&");

                return args.Aggregate(new BInteger(~0L), (m, v) => new BInteger(m.Value & ((BInteger) v).Value));
            })},
            {"~", new Function("~", 1, (broccoli, args) => {
                if (!(args[0] is BInteger i))
                    throw new ArgumentTypeException(args[0], "integer", 1, "~");

                return new BInteger(~i.Value);
            })},
            {"~&", new Function("~&", 2, (broccoli, args) => {
                if (!(args[0] is BInteger i))
                    throw new ArgumentTypeException(args[0], "integer", 1, "~&");
                if (!(args[1] is BInteger j))
                    throw new ArgumentTypeException(args[1], "integer", 2, "~&");

                return new BInteger(~(i.Value & j.Value));
            })},
            {"~|", new Function("~|", 2, (broccoli, args) => {
                if (!(args[0] is BInteger i))
                    throw new ArgumentTypeException(args[0], "integer", 1, "~|");
                if (!(args[1] is BInteger j))
                    throw new ArgumentTypeException(args[1], "integer", 2, "~|");

                return new BInteger(~(i.Value | j.Value));
            })},
            {"&~1", new Function("&~1", 2, (broccoli, args) => {
                if (!(args[0] is BInteger i))
                    throw new ArgumentTypeException(args[0], "integer", 1, "&~1");
                if (!(args[1] is BInteger j))
                    throw new ArgumentTypeException(args[1], "integer", 2, "&~1");

                return new BInteger(~i.Value & j.Value);
            })},
            {"&~2", new Function("&~2", 2, (broccoli, args) => {
                if (!(args[0] is BInteger i))
                    throw new ArgumentTypeException(args[0], "integer", 1, "&~2");
                if (!(args[1] is BInteger j))
                    throw new ArgumentTypeException(args[1], "integer", 2, "&~2");

                return new BInteger(i.Value & ~j.Value);
            })},
            {"|~1", new Function("|~1", 2, (broccoli, args) => {
                if (!(args[0] is BInteger i))
                    throw new ArgumentTypeException(args[0], "integer", 1, "|~1");
                if (!(args[1] is BInteger j))
                    throw new ArgumentTypeException(args[1], "integer", 2, "|~1");

                return new BInteger(~i.Value | j.Value);
            })},
            {"|~2", new Function("|~2", 2, (broccoli, args) => {
                if (!(args[0] is BInteger i))
                    throw new ArgumentTypeException(args[0], "integer", 1, "|~2");
                if (!(args[1] is BInteger j))
                    throw new ArgumentTypeException(args[1], "integer", 2, "|~2");

                return new BInteger(i.Value | ~j.Value);
            })},
            {"logtest", new Function("logtest", 2, (broccoli, args) => {
                if (!(args[0] is BInteger i))
                    throw new ArgumentTypeException(args[0], "integer", 1, "logtest");
                if (!(args[1] is BInteger j))
                    throw new ArgumentTypeException(args[1], "integer", 2, "logtest");

                return Boolean((i.Value & j.Value) != 0);
            })},
            {"logbitp", new Function("logtest", 2, (broccoli, args) => {
                if (!(args[0] is BInteger i))
                    throw new ArgumentTypeException(args[0], "integer", 1, "logtest");
                if (!(args[1] is BInteger j))
                    throw new ArgumentTypeException(args[1], "integer", 2, "logtest");

                return Boolean((j.Value >> (int) i.Value) % 2 == 1);
            })},
            {"integer-length", new Function("integer-length", 1, (broccoli, args) => {
                if (!(args[0] is BInteger i))
                    throw new ArgumentTypeException(args[0], "integer", 1, "integer-length");

                return new BInteger(Math.Ceiling(Math.Log(i.Value < 0 ? -i.Value : i.Value + 1, 2)));
            })},
            {"**", new Function("**", ~0, (broccoli, args) => {
                foreach (var (value, index) in args.WithIndex())
                    if (!(value is BInteger || value is BFloat))
                        throw new ArgumentTypeException(value, "integer or float", index + 1, "*");

                return args.Reverse().Aggregate((IValue) new BInteger(1), (m, v) => {
                    if (m is BInteger im && v is BInteger iv) {
                        var result = Math.Pow(iv.Value, im.Value);
                        if (result < long.MaxValue)
                            return new BInteger(result);
                        return new BFloat(result);
                    }
                    double fm, fv;
                    if (m is BInteger mValue)
                        fm = mValue.Value;
                    else
                        fm = ((BFloat) m).Value;
                    if (v is BInteger vValue)
                        fv = vValue.Value;
                    else
                        fv = ((BFloat) v).Value;
                    return new BFloat(Math.Pow(fv, fm));
                });
            })},
            {"evenp", new Function("evenp", 1, (broccoli, args) => {
                if (!(args[0] is BInteger i))
                    throw new ArgumentTypeException(args[0], "integer", 1, "evenp");

                return Boolean(i.Value % 2 == 0);
            })},
            {"oddp", new Function("oddp", 1, (broccoli, args) => {
                if (!(args[0] is BInteger i))
                    throw new ArgumentTypeException(args[0], "integer", 1, "oddp");

                return Boolean(i.Value % 2 == 1);
            })},
            {"zerop", new Function("zerop", 1, (broccoli, args) => {
                if (args[0] is BInteger i)
                    return Boolean(i.Value == 0);
                if (args[0] is BFloat f)
                    // ReSharper disable once CompareOfFloatsByEqualityOperator
                    return Boolean(f.Value == 0);
                throw new ArgumentTypeException(args[0], "number", 1, "zerop");
            })},

            {"popcnt", new Function("popcnt", 1, (broccoli, args) => {
                if (!(args[0] is BInteger i))
                    throw new ArgumentTypeException(args[0], "integer", 1, "popcnt");

                var n = i.Value;
                n = n - ((n >> 1) & 0x5555555555555555);
                n = (n & 0x3333333333333333) + ((n >> 2) & 0x3333333333333333);
                return new BInteger((((n + (n >> 4)) & 0xF0F0F0F0F0F0F0F) * 0x101010101010101) >> 56);
            })},

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

            // Basic math
            {"+", new Function("+", ~0, (cauliflower, args) => {
                if (args.Length == 0 || args[0] is BInteger || args[0] is BFloat)
                    foreach (var (value, index) in args.WithIndex()) {
                        if (!(value is BInteger || value is BFloat))
                            throw new ArgumentTypeException(value, "integer or float", index + 1, "+");
                    }
                else {
                    var type = args[0].GetType();
                    var typeString = new Dictionary<Type, string>{
                        {typeof(BString), "string"},
                        {typeof(BAtom), "atom"},
                        {typeof(BList), "list"},
                        {typeof(BDictionary), "dictionary"}
                    }.GetValueOrDefault(type, "C# value");
                    foreach (var (value, index) in args.WithIndex())
                        if (value.GetType() != type)
                            throw new ArgumentTypeException(value, typeString, index + 1, "+");
                    if (type == typeof(BString)) {
                        var b = new StringBuilder();
                        foreach (var arg in args)
                            b.Append(((BString) arg).Value);
                        return new BString(b.ToString());
                    }
                    if (type == typeof(BAtom))
                        throw new ArgumentTypeException(args[0], "number, string, list or dictionary", 1, "+");
                    if (type == typeof(BList))
                        return new BList(args.Cast<BList>().Aggregate<IEnumerable<IValue>>((m, v) => m.Concat(v)));
                    if (type == typeof(BDictionary)) {
                        var result = new BDictionary();
                        foreach (var arg in args)
                            foreach (var (key, value) in (BDictionary) arg)
                                result[key] = value;
                        return result;
                    }
                }

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

            {"not", new Function("not", 1, (cauliflower, args) => Boolean(!CauliflowerInline.Truthy(args[0])))},
            {"and", new ShortCircuitFunction("and", ~0, (cauliflower, args) =>
                Boolean(args.All(arg => CauliflowerInline.Truthy(cauliflower.Run(arg))))
            )},
            {"or", new ShortCircuitFunction("or", ~0, (cauliflower, args) =>
                Boolean(args.Any(arg => CauliflowerInline.Truthy(cauliflower.Run(arg))))
            )},

            {"\\", new ShortCircuitFunction("\\", ~1, (cauliflower, args) => {
                if (!(args[0] is ValueExpression argExpressions)) {
                    switch (args[0]) {
                        case ScalarVar s:
                            argExpressions = new ValueExpression(s);
                            break;
                        case ListVar l:
                            argExpressions = new ValueExpression(l);
                            break;
                        default:
                            throw new ArgumentTypeException(args[0], "expression", 1, "\\");
                    }
                }
                var argNames = argExpressions.Values.ToList();
                if (argNames.Take(argNames.Count - 1).OfType<ValueExpression>().Any()) {
                    throw new ArgumentTypeException("Received expression instead of variable name in argument list for '\\'");
                }
                var length = argNames.Count;
                IValue varargs = null;
                if (argNames.Count != 0) {
                    switch (argNames.Last()) {
                        case ListVar _:
                        case ScalarVar _:
                        case DictVar _:
                        case BAtom _:
                            break;
                        case ValueExpression expr when expr.Values.Length == 1 && expr.Values[0] is ListVar l:
                            length = -length;
                            varargs = l;
                            break;
                        default:
                            throw new ArgumentTypeException("Received expression instead of variable name in argument list for '\\'");
                    }
                }
                if (varargs != null)
                    argNames.RemoveAt(argNames.Count - 1);
                var statements = args.Skip(1);
                var scope = new CauliflowerScope(cauliflower.Scope);
                return new AnonymousFunction(length, innerArgs => {
                    var oldScope = cauliflower.Scope;
                    cauliflower.Scope = scope;
                    for (var i = 0; i < argNames.Count; i++) {
                        var toAssign = innerArgs[i];
                        switch (argNames[i]) {
                            case ScalarVar s:
                                if (!(toAssign is IScalar scalar))
                                    throw new Exception("Only scalars can be assigned to scalar ($) variables");
                                cauliflower.Scope[s] = scalar;
                                break;
                            case ListVar l:
                                if (!(toAssign is IList list))
                                    throw new Exception("Only lists can be assigned to list (@) variables");
                                cauliflower.Scope[l] = list;
                                break;
                            case DictVar d:
                                if (!(toAssign is IDictionary dict))
                                    throw new Exception("Only dictionaries can be assigned to dictionary (%) variables");
                                cauliflower.Scope[d] = dict;
                                break;
                            case BAtom a:
                                if (!(toAssign is IFunction fn))
                                    throw new Exception("Only Functions can be assigned to atom variables");
                                cauliflower.Scope[a] = fn;
                                break;
                            default:
                                throw new Exception("Values can only be assigned to scalar ($), list (@), dictionary (%) or atom variables");
                        }
                    }
                    if (varargs != null)
                        cauliflower.Scope[(ListVar) varargs] = new BList(innerArgs.Skip(argNames.Count));
                    IValue result = null;
                    foreach (var statement in statements)
                        result = cauliflower.Run(statement);
                    cauliflower.Scope = oldScope;
                    return result;
                });
            })},

            {"if", new ShortCircuitFunction("if", ~2, (cauliflower, args) => {
                var condition = CauliflowerInline.Truthy(cauliflower.Run(args[0]));
                var elseIndex = Array.IndexOf(args.ToArray(), new BAtom("else"), 1);
                var statements = elseIndex != -1 ?
                    condition ? args.Skip(1).Take(elseIndex - 1) : args.Skip(elseIndex + 1) :
                    condition ? args.Skip(1) : args.Skip(args.Length);
                IValue result = null;
                foreach (var statement in statements)
                    result = cauliflower.Run(statement);
                return result;
            })},
            {"for", new ShortCircuitFunction("for", ~2, (cauliflower, args) => {
                var inValue = cauliflower.Run(args[1]);
                if (!(inValue is BAtom inAtom))
                    throw new ArgumentTypeException(inValue, "atom", 2, "for");
                if (inAtom.Value != "in")
                    throw new ArgumentTypeException($"Received atom '{inAtom.Value}' instead of atom 'in' in argument 2 for 'for'");

                var iterable = cauliflower.Run(args[2]);
                if (!(iterable is BList valueList))
                    throw new ArgumentTypeException(iterable, "list", 3, "for");

                cauliflower.Scope = new CauliflowerScope(cauliflower.Scope);
                var statements = args.Skip(3).ToList();
                foreach (var value in valueList) {
                    switch (args[0]) {
                        case ScalarVar s:
                            if (!(value is IScalar scalar))
                                throw new Exception("Only Scalars can be assigned to scalar ($) variables");
                            cauliflower.Scope[s] = scalar;
                            break;
                        case ListVar l:
                            if (!(value is IList list))
                                throw new Exception("Only Lists can be assigned to list (@) variables");
                            cauliflower.Scope[l] = list;
                            break;
                        case DictVar d:
                            if (!(value is IDictionary dict))
                                throw new Exception("Only Dictionaries can be assigned to dictionary (%) variables");
                            cauliflower.Scope[d] = dict;
                            break;
                        case BAtom a:
                            if (!(value is IFunction fn))
                                throw new Exception("Only Functions can be assigned to atom variables");
                            cauliflower.Scope[a] = fn;
                            break;
                        default:
                            throw new Exception($"Cannot assign to unknown type '{args[0].GetType()}'");
                    }
                    foreach (var statement in statements)
                        cauliflower.Run(statement);
                }

                cauliflower.Scope = cauliflower.Scope.Parent;
                return null;
            })},
            {"while", new ShortCircuitFunction("while", ~1, (cauliflower, args) => {
                var condition = args[0];
                var statements = args.Skip(1).ToList();
                IValue result = null;
                while (CauliflowerInline.Truthy(cauliflower.Run(condition)))
                    foreach (var statement in statements)
                        result = cauliflower.Run(statement);
                return result;
            })},
            {"do", new ShortCircuitFunction("do", ~2, (cauliflower, args) => {
                cauliflower.Scope = new CauliflowerScope(cauliflower.Scope);
                var assignments = args[0] as ValueExpression;
                if (assignments == null)
                    throw new Exception("Do loop has no assignments");
                var returnSpec = args[1] as ValueExpression ?? new ValueExpression(args[1]);
                var condition = returnSpec.Values.Length == 0 ? BAtom.Nil : returnSpec.Values[0];
                var resultForm = returnSpec.Values.Skip(1).ToArray();
                var assignmentBase = new IValueExpressible[] { new BAtom(":=") };
                var body = args.Skip(2);
                var values = new List<IValueExpressible>();
                // ReSharper disable once PossibleInvalidCastExceptionInForeachLoop
                foreach (ValueExpression assignment in assignments.Values) {
                    if (assignment.Values.Length != 3)
                        throw new Exception("Do binding must have variable name, initial value, and next value");
                    values.Add(assignment.Values[0]);
                    values.Add(assignment.Values[1]);
                }
                cauliflower.Run(new ValueExpression(assignmentBase.Concat(values)));
                while (true) {
                    foreach (var expression in body)
                        cauliflower.Run(expression);
                    if (CauliflowerInline.Truthy(cauliflower.Run(condition)))
                        break;
                    values.Clear();
                    // ReSharper disable once PossibleInvalidCastExceptionInForeachLoop
                    foreach (ValueExpression assignment in assignments.Values) {
                        values.Add(assignment.Values[0]);
                        values.Add(assignment.Values[2]);
                    }
                    cauliflower.Run(new ValueExpression(assignmentBase.Concat(values)));
                }
                IValue result = BAtom.Nil;
                foreach (var item in resultForm)
                    result = cauliflower.Run(item);
                cauliflower.Scope = cauliflower.Scope.Parent;
                return result;
            })},
            {"dotimes", new ShortCircuitFunction("dotimes", ~2, (cauliflower, args) => {
                cauliflower.Scope = new CauliflowerScope(cauliflower.Scope);
                var range = args[0] as ValueExpression;
                if (range == null)
                    throw new Exception("Dotimes loop has no range");
                if (range.Values.Length < 2)
                    throw new Exception($"Dotimes loop recieved {range.Values.Length} arguments in initializer, expected 2 or more");
                long start = 0, end = 0, step = 1;
                if (range.Values.Length > 1) {
                    //(dox($i 1 101 t)(dox($j 1(1+ i)t)(if(=(mod$i$j)0)(p$j" ")))(s))
                    var value = cauliflower.Run(range.Values[1]);
                    if (!(value is BInteger i))
                        throw new ArgumentTypeException(value, "integer", 2, "dotimes");

                    end = i.Value;
                }
                if (range.Values.Length > 3) {
                    var value = cauliflower.Run(range.Values[2]);
                    if (!(value is BInteger i))
                        throw new ArgumentTypeException(value, "integer", 3, "dotimes");

                    start = end;
                    end = i.Value;
                }
                if (range.Values.Length > 4) {
                    var value = cauliflower.Run(range.Values[3]);
                    if (!(value is BInteger i))
                        throw new ArgumentTypeException(value, "integer", 4, "dotimes");

                    step = i.Value;
                }
                var resultVar = range.Values.Length > 2 ? range.Values.Last() : null;
                var assignment = new[] { new BAtom(":="), range.Values[0], null };
                var body = args.Skip(1);
                for (var i = start; end > 0 ? i < end : i > end; i += step) {
                    assignment[2] = new BInteger(i);
                    cauliflower.Run(new ValueExpression(assignment));
                    foreach (var expression in body)
                        cauliflower.Run(expression);
                }
                IValue result = BAtom.Nil;
                if (resultVar != null)
                    result = cauliflower.Run(resultVar);
                cauliflower.Scope = cauliflower.Scope.Parent;
                return result;
            })},
            {"dolist", new ShortCircuitFunction("dolist", ~2, (cauliflower, args) => {
                cauliflower.Scope = new CauliflowerScope(cauliflower.Scope);
                var initList = args[0] as ValueExpression;
                if (initList == null)
                    throw new Exception("Dolist loop has no list");
                if (initList.Values.Length < 2)
                    throw new Exception($"Dolist loop recieved {initList.Values.Length} arguments in initializer, expected 2 or more");
                var iterable = cauliflower.Run(initList.Values[1]);
                if (!(iterable is IList listArg))
                    throw new ArgumentTypeException(iterable, "list", 2, "initializer of dolist");
                var resultVar = initList.Values.Length > 2 ? initList.Values.Last() : null;
                var assignment = new[] { new BAtom(":="), initList.Values[0], null };
                var body = args.Skip(1);
                foreach (var item in listArg) {
                    assignment[2] = item;
                    cauliflower.Run(new ValueExpression(assignment));
                    foreach (var expression in body)
                        cauliflower.Run(expression);
                }
                IValue result = BAtom.Nil;
                if (resultVar != null)
                    result = cauliflower.Run(resultVar);
                cauliflower.Scope = cauliflower.Scope.Parent;
                return result;
            })},

            {"len", new Function("len", 1, (cauliflower, args) => {
                switch (args[0]) {
                    case BString s:
                        return new BInteger(s.Value.Length);
                    case IList l:
                        return new BInteger(l.Count());
                    case IDictionary d:
                        return new BInteger(d.Count);
                    default:
                        throw new ArgumentTypeException(args[0], "list, dictionary or string", 1, "len");
                }
            })},
            {"slice", new Function("slice", ~2, (cauliflower, args) => {
                if (!(args[0] is BList list))
                    throw new ArgumentTypeException(args[0], "list", 1, "slice");
                foreach (var (arg, index) in args.Skip(1).WithIndex())
                    if (!(arg is BInteger))
                        throw new ArgumentTypeException(arg, "integer", index + 2, "slice");
                var ints = args.Skip(1).Select(arg => (int) ((BInteger) arg).Value).ToArray();
                if (ints.Length > 0) {
                    if (ints[0] < 0)
                        ints[0] = Math.Max(0, ints[0] + list.Count);
                    else
                        ints[0] = Math.Min(list.Count, ints[0]);
                }
                if (ints.Length > 1) {
                    if (ints[1] < 0)
                        ints[1] = Math.Max(0, ints[1] + list.Count);
                    else
                        ints[1] = Math.Min(list.Count, ints[1]);
                }

                switch (ints.Length) {
                    case 0:
                        return list;
                    case 1:
                        return new BList(list.Skip(ints[0]));
                    case 2:
                        return new BList(list.Skip(ints[0]).Take(ints[1] - Math.Max(0, ints[0])));
                    case 3:
                        return new BList(Enumerable.Range(0, Math.Max(0, (Math.Sign(ints[2]) * (ints[1] - ints[0]) - 1) / Math.Abs(ints[2]) + 1)).Select(i => list[ints[0] + i * ints[2]]));
                    default:
                        throw new Exception($"Function slice requires 1 to 4 arguments, {args.Length} provided");
                }
            })},
            {"range", new Function("range", ~1, (cauliflower, args) => {
                foreach (var (arg, index) in args.WithIndex())
                    if (!(arg is BInteger))
                        throw new ArgumentTypeException(arg, "integer", index + 1, "range");
                var ints = args.Select(arg => (int) ((BInteger) arg).Value).ToArray();

                switch (ints.Length) {
                    case 1:
                        return new BList(Enumerable.Range(0, Math.Max(0, ints[0])).Select(i => (IValue) new BInteger(i)));
                    case 2:
                        return new BList(Enumerable.Range(ints[0], Math.Max(0, ints[1] - ints[0])).Select(i => (IValue) new BInteger(i)));
                    case 3:
                        return new BList(Enumerable.Range(0, Math.Max(0, (Math.Sign(ints[2]) * (ints[1] - ints[0]) - 1) / Math.Abs(ints[2]) + 1)).Select(i => (IValue) new BInteger(ints[0] + i * ints[2])));
                    default:
                        throw new Exception($"Function range requires 1 to 3 arguments, {args.Length} provided");
                }
            })},
            {"cat", new Function("cat", ~0, (broccoli, args) => {
                var result = new BList();
                foreach (var item in args)
                    if (item is IList list)
                        result.AddRange(list);
                    else
                        result.Add(item);
                return result;
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
                // var func = CauliflowerInline.FindFunction("all", cauliflower, args[0]);

                if (args[1] is BList l)
                    return Boolean(l.All(CauliflowerInline.Truthy));
                throw new ArgumentTypeException(args[1], "list", 2, "all");
            })},
            {"any", new Function("any", 2, (cauliflower, args) => {
                // var func = CauliflowerInline.FindFunction("any", cauliflower, args[0]);

                if (args[1] is BList l)
                    return Boolean(l.Any(CauliflowerInline.Truthy));
                throw new ArgumentTypeException(args[1], "list", 2, "any");
            })},
            {"mkdict", new Function("mkdict", 0, (cauliflower, args) => new BDictionary())},
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
            {"keys", new Function("keys", 1, (cauliflower, args) => {
                if (args[0] is BDictionary dict) {
                    return new BList(dict.Keys);
                }
                throw new Exception("First argument to listkeys must be a Dict.");
            })},
            {"values", new Function("values", 1, (cauliflower, args) => {
                if (args[0] is BDictionary dict) {
                    return new BList(dict.Values);
                }
                throw new Exception("First argument to listkeys must be a Dict.");
            })},
        }.Extend(Interpreter.StaticBuiltins)
            .Alias("print", "p")
            .Alias("say", "s")
            .Alias("read", "r")
            .Alias("write", "w")
            .Alias("fn", "f", "fun", "func", "function")
            .Alias("\\", "lambda")
            .Alias("cat", "concat", "concatenate")
            .Alias("len", "length")
            .Alias("first", "car")
            .Alias("rest", "cdr")
            .Alias("^", "logxor")
            .Alias("|", "logior")
            .Alias("&", "logand")
            .Alias("~", "lognot")
            .Alias("~^", "logeqv")
            .Alias("~|", "lognor")
            .Alias("~&", "lognand")
            .Alias("&~1", "logandc1")
            .Alias("&~2", "logandc2")
            .Alias("|~1", "logorc1")
            .Alias("|~2", "logorc2")
            .Alias("popcnt", "popcount", "logcount")
            .Alias("evenp", "ep")
            .Alias("oddp", "op")
            .Alias("zerop", "zp")
            .Alias("dotimes", "dox")
            .Alias("dolist", "dol");

        /// <summary>
        /// Helper functions for Cauliflower that can be inlined.
        /// </summary>
        static class CauliflowerInline {
            /// <summary>
            /// Finds the function to execute given a value.
            /// </summary>
            /// <param name="callerName">The function call name.</param>
            /// <param name="cauliflower">The containing Cauliflower interpreter.</param>
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
            /// Returns whether a Cauliflower value is truthy.
            /// </summary>
            /// <param name="value">The value to check.</param>
            /// <returns>The truthiness of the value.</returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static bool Truthy(IValue value) {
                switch (value) {
                    case BInteger i:
                        return i.Value != 0;
                    case BFloat f:
                        // ReSharper disable once CompareOfFloatsByEqualityOperator
                        return f.Value != 0;
                    case BString s:
                        return s.Value.Length != 0;
                    case BAtom a:
                        return !a.Equals(BAtom.Nil);
                    case BList v:
                        return v.Value.Count != 0;
                    case IFunction _:
                        return true;
                    default:
                        return false;
                }
            }

            /// <summary>
            /// Imports a Cauliflower native module.
            /// </summary>
            /// <param name="cauliflower">Interpreter instance wanting to import module</param>
            /// <param name="path">Path to module</param>
            /// <param name="isStatic">Whether to static import module</param>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static void Import(Interpreter cauliflower, IValue[] path, bool isStatic = false) {
                if (cauliflower.Scope.Namespaces.Value == null)
                    cauliflower.Scope.Namespaces.Value = new CauliflowerScope();
                if (basePath == null) {
                    var linux = System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Linux);
                    pathSeparator = linux ? "/" : "\\";
                    Directory.CreateDirectory(
                        basePath = linux ?
                            "/usr/local/lib/cauliflower" :
                            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + "\\Cauliflower"
                    );
                    AppDomain.CurrentDomain.AssemblyResolve += (sender, assemblyInfo) => {
                        try {
                            return Assembly.Load(assemblyInfo.Name);
                        } catch {
                            return Assembly.LoadFile(basePath + pathSeparator + "dependencies" + pathSeparator + new AssemblyName(assemblyInfo.Name).Name);
                        }
                    };
                }
                var directory = basePath + pathSeparator + "modules";
                Assembly module = null;
                var fullModuleName = new StringBuilder("cauliflower.");
                var fullTypeName = new StringBuilder();
                string typeName = null;
                foreach (var (item, index) in path.WithIndex()) {
                    if (!(item is BAtom a))
                        throw new ArgumentTypeException(item, "atom", index + 1, "import");
                    if (module == null) {
                        fullModuleName.Append(a.Value).Append('.');
                        var next = directory + pathSeparator + a.Value;
                        if (Directory.Exists(next))
                            directory = next;
                        else if (File.Exists(next + ".dll"))
                            module = Assembly.LoadFile(next + ".dll");
                        else
                            throw new Exception($"Directory '{directory + pathSeparator + a.Value}' not found");
                    } else {
                        if (fullTypeName.Length != 0)
                            fullTypeName.Append('.');
                        fullTypeName.Append(typeName = a.Value);
                    }
                }
                if (module != null && typeName == null) {
                    foreach (var nested in module.GetExportedTypes().Where(t => !t.Name.Contains("<"))) {
                        AddTypes(cauliflower, nested, cauliflower.Scope.Namespaces);
                        if (isStatic)
                            foreach (var method in nested.GetMethods(BindingFlags.Public | BindingFlags.Static)) {
                                var name = method.Name.Replace('_', '-');
                                cauliflower.Scope.Functions[method.Name.Replace('_', '-')] = new Function(
                                    name, ~0, (_, innerArgs) => CauliflowerMethod(nested, method.Name, null, innerArgs)
                                );
                            }
                        else if (nested.GetInterface("IValue") != null) {
                            var property = nested.GetProperty("interpreter", BindingFlags.Static);
                            if (property?.PropertyType == typeof(Interpreter))
                                property.SetValue(null, cauliflower);
                            cauliflower.Scope.Namespaces.Value.Types[nested.Name.Replace('_', '-')] = (BCauliflowerType) nested;
                        }
                    }
                }
            }

            /// <summary>
            /// Gets statements from a list of expressions
            /// </summary>
            /// <param name="expressions">Expressions to turn into statements</param>
            /// <returns></returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static List<(string, List<string>, ValueExpression)> GetStatements(IEnumerable<IValueExpressible> expressions) {
                var statements = new List<(string, List<string>, ValueExpression)>();
                foreach (var (arg, index) in expressions.WithIndex()) {
                    if (!(arg is ValueExpression e)) {
                        if (!(arg is BAtom a1))
                            throw new ArgumentTypeException(arg, "expression or atom", index + 1,
                            "class");

                        e = new ValueExpression(a1);
                    }

                    if (e.Values.Length == 0)
                        throw new Exception($"Found empty expression in statement {index + 1} of 'class'");
                    var i = 0;
                    var m = new List<string>();
                    if (e.Values[0] is BAtom mod && modifiers.Contains(mod.Value)) {
                        m.Add(mod.Value);
                        while ((e.Values[++i] is BAtom atom) && modifiers.Contains(atom.Value)) {
                            if (accessControlModifiers.Contains(mod.Value) &&
                                accessControlModifiers.Contains(atom.Value))
                                throw new Exception("Only one access control modifier is allowed per class member");
                            m.Add(atom.Value);
                        }

                        if (!(e.Values[i] is ValueExpression e2)) {
                            if (!(e.Values[i] is BAtom a2))
                                throw new ArgumentTypeException(arg, "expression or atom",
                                    index + 1,
                                    "class");

                            e = new ValueExpression(a2);
                        } else {
                            e = e2;
                        }
                    }
                    if (!(e.Values[0] is BAtom a))
                        throw new Exception($"Expected atom in statement {index + 1} of 'class', found {e.Values[0].GetType()} instead");
                    statements.Add((a.Value, m, new ValueExpression(e.Values.Skip(1))));
                }
                return statements;
            }

            /// <summary>
            /// Returns an array of parameter types according to the given parameter expression.
            /// </summary>
            /// <param name="vexp">The parameter expression.</param>
            /// <returns>The array of parameter types.</returns>
            /// <exception cref="ArgumentTypeException">Throws when parameter types are invalid.</exception>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static Type[] GetParameterTypes(ValueExpression vexp) {
                var results = new Type[vexp.Values.Length];
                foreach (var (param, index) in vexp.Values.WithIndex()) {
                    switch (param) {
                        case ScalarVar _:
                            results[index] = typeof(IScalar);
                            break;
                        case ListVar _:
                            results[index] = typeof(IList);
                            break;
                        case DictVar _:
                            results[index] = typeof(IDictionary);
                            break;
                        case BAtom _:
                            results[index] = typeof(IFunction);
                            break;
                        // Rest args
                        case ValueExpression v:
                            if (!(v.Values.ElementAtOrDefault(0) is ListVar _))
                                throw new ArgumentTypeException(v.Values.ElementAtOrDefault(0), "rest arguments list variable", index + 1, "fn parameters");

                            results[index] = typeof(IValue[]);
                            break;
                        default:
                            throw new ArgumentTypeException(param, "variable name", index + 1, "fn parameters");
                    }
                }

                return results;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static void LoadInterpreterReference(ILGenerator gen, FieldInfo interpreterField) => gen.Emit(OpCodes.Ldsfld, interpreterField);

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static void LoadScopeReference(ILGenerator gen, FieldInfo interpreterField) {
                LoadInterpreterReference(gen, interpreterField);
                gen.Emit(OpCodes.Ldfld, typeof(Interpreter).GetField("Scope"));
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static void CreateNewScope(ILGenerator gen, FieldInfo interpreterField) {
                LoadInterpreterReference(gen, interpreterField); // Load to store in scope
                LoadScopeReference(gen, interpreterField); // Load to get scope value
                gen.Emit(OpCodes.Newobj, typeof(Scope).GetConstructor(new [] { typeof(Scope) })); // Create new scope
                gen.Emit(OpCodes.Stfld, typeof(Interpreter).GetField("Scope"));
                LoadScopeReference(gen, interpreterField);
                gen.Emit(OpCodes.Call, typeof(MethodBase).GetMethod("GetCurrentMethod", BindingFlags.Public | BindingFlags.Static));
                gen.Emit(OpCodes.Callvirt, typeof(MemberInfo).GetMethod("get_DeclaringType"));
                gen.Emit(OpCodes.Callvirt, typeof(Scope).GetMethod("set_SurroundingClass"));
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static void ReturnToParentScope(ILGenerator gen, FieldInfo interpreterField) {
                LoadInterpreterReference(gen, interpreterField);
                LoadScopeReference(gen, interpreterField);
                gen.Emit(OpCodes.Ldfld, typeof(Scope).GetField("Parent"));
                gen.Emit(OpCodes.Stfld, typeof(Interpreter).GetField("Scope"));
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static void AddThisToScope(ILGenerator gen, FieldInfo interpreterField, bool isScalar = true, bool isList = false, bool isDictionary = false) {
                FieldInfo scopeField;
                MethodInfo dictSetter;

                if (isScalar) {
                    scopeField = typeof(Scope).GetField("Scalars");
                    dictSetter = typeof(Dictionary<string, IScalar>).GetMethod("set_Item");
                } else if (isList) {
                    scopeField = typeof(Scope).GetField("Lists");
                    dictSetter = typeof(Dictionary<string, IList>).GetMethod("set_Item");
                } else if (isDictionary) {
                    scopeField = typeof(Scope).GetField("Dictionaries");
                    dictSetter = typeof(Dictionary<string, IDictionary>).GetMethod("set_Item");
                } else {
                    throw new NotImplementedException();
                }

                LoadScopeReference(gen, interpreterField);
                gen.Emit(OpCodes.Ldfld, scopeField);
                gen.Emit(OpCodes.Ldstr, "this");
                gen.Emit(OpCodes.Ldarg_0);
                gen.Emit(OpCodes.Callvirt, dictSetter);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static void LoadInterpreterInvocation(ILGenerator gen, FieldInfo interpreterField, IValueExpressible expr) {
                LoadInterpreterReference(gen, interpreterField);
                gen.Emit(OpCodes.Ldstr, expr.Inspect());
                gen.Emit(OpCodes.Callvirt, typeof(CauliflowerInterpreter).GetMethod("Run", new [] {typeof(string)}));
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static void LoadInterpreterInvocation(ILGenerator gen, FieldInfo interpreterField, IEnumerable<IValueExpressible> exprs) {
                LoadInterpreterReference(gen, interpreterField);
                gen.Emit(OpCodes.Ldstr, string.Join(' ', exprs.Select(e => e.Inspect())));
                gen.Emit(OpCodes.Callvirt, typeof(CauliflowerInterpreter).GetMethod("Run", new [] {typeof(string)}));
            }

            /// <summary>
            /// Populates a function's scope with values from the given parameter expression.
            /// </summary>
            /// <param name="methILGen">The <see cref="ILGenerator"/> for the method.</param>
            /// <param name="interpreterField">The <see cref="FieldInfo"/> that represents the interpreter field in the class.</param>
            /// <param name="paramExp">The parameter expression.</param>
            /// <param name="isStatic">Whether the parameters are for a Cauliflower static class function.</param>
            /// <exception cref="ArgumentTypeException">Throws when parameter types are invalid.</exception>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static void AddParametersToScope(ILGenerator methILGen, FieldInfo interpreterField, ValueExpression paramExp, bool isStatic = false) {
                foreach (var (param, _index) in paramExp.Values.WithIndex()) {
                    var index = isStatic ? _index : _index + 1;
                    LoadScopeReference(methILGen, interpreterField);
                    switch (param) {
                        case ScalarVar s:
                            methILGen.Emit(OpCodes.Ldfld, typeof(Scope).GetField("Scalars"));
                            methILGen.Emit(OpCodes.Ldstr, s.Value);
                            methILGen.Emit(OpCodes.Ldarg_S, index);
                            methILGen.Emit(OpCodes.Callvirt, typeof(Dictionary<string, IScalar>).GetMethod("set_Item"));
                            break;
                        case ListVar l:
                            methILGen.Emit(OpCodes.Ldfld, typeof(Scope).GetField("Lists"));
                            methILGen.Emit(OpCodes.Ldstr, l.Value);
                            methILGen.Emit(OpCodes.Ldarg_S, index);
                            methILGen.Emit(OpCodes.Callvirt, typeof(Dictionary<string, IList>).GetMethod("set_Item"));
                            break;
                        case DictVar d:
                            methILGen.Emit(OpCodes.Ldfld, typeof(Scope).GetField("Dictionaries"));
                            methILGen.Emit(OpCodes.Ldstr, d.Value);
                            methILGen.Emit(OpCodes.Ldarg_S, index);
                            methILGen.Emit(OpCodes.Callvirt, typeof(Dictionary<string, IDictionary>).GetMethod("set_Item"));
                            break;
                        case BAtom a:
                            methILGen.Emit(OpCodes.Ldfld, typeof(Scope).GetField("Functions"));
                            methILGen.Emit(OpCodes.Ldstr, a.Value);
                            methILGen.Emit(OpCodes.Ldarg_S, index);
                            methILGen.Emit(OpCodes.Callvirt, typeof(Dictionary<string, IFunction>).GetMethod("set_Item"));
                            break;
                        // Rest args
                        case ValueExpression v:
                            if (!(v.Values.ElementAtOrDefault(0) is ListVar rest))
                                throw new ArgumentTypeException(v.Values.ElementAtOrDefault(0), "rest arguments list variable", index + 1, "fn parameters");

                            methILGen.Emit(OpCodes.Ldfld, typeof(Scope).GetField("Lists"));
                            methILGen.Emit(OpCodes.Ldstr, rest.Value);
                            methILGen.Emit(OpCodes.Ldarg_S, index);
                            methILGen.Emit(OpCodes.Callvirt, typeof(Dictionary<string, IList>).GetMethod("set_Item"));
                            break;
                        default:
                            throw new ArgumentTypeException(param, "variable name", index + 1, "fn parameters");
                    }
                }
            }
        }
    }
}
