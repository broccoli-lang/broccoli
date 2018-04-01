using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Text;

// TODO: test "." and "->" and "import"
// TODO: finish "namespace" function & maybe various OOP things
// TODO: try catch

namespace Broccoli {
    /// <summary>
    /// Useful variants of type methods.
    /// </summary>
    static class TypeExtensions {
        /// <summary>
        /// Gets property of object.
        /// </summary>
        /// <param name="type">Type to get property from.</param>
        /// <param name="name">Name of property.</param>
        /// <returns>Property.</returns>
        /// <exception cref="Exception">Throws when property is not found.</exception>
        public static PropertyInfo TryGetProperty(this Type type, string name) {
            var result = type.GetProperty(name);
            if (result == null)
                throw new Exception($"Type '{type.FullName}' has no field '{name}'");
            return result;
        }

        /// <summary>
        /// Gets field of object.
        /// </summary>
        /// <param name="type">Type to get field from.</param>
        /// <param name="name">Name of field.</param>
        /// <returns>Field.</returns>
        /// <exception cref="Exception">Throws when field is not found.</exception>
        public static FieldInfo TryGetField(this Type type, string name) {
            var result = type.GetField(name);
            if (result == null)
                throw new Exception($"Type '{type.FullName}' has no field '{name}'");
            return result;
        }
    }

    public partial class CauliflowerInterpreter : Interpreter {
        /// <summary>
        /// Array of all assembles used by this project.
        /// </summary>
        private static Assembly[] assemblies = null;

        /// <summary>
        /// Dictionary of assembly full name to Assembly object.
        /// </summary>
        private static Dictionary<string, Assembly> assemblyLookup = null;

        /// <summary>
        /// Base path of Cauliflower module storage.
        /// </summary>
        private static string basePath = null;

        /// <summary>
        /// Path separator of OS.
        /// </summary>
        private static string pathSeparator = null;

        /// <summary>
        /// Cache of methods grouped by type.
        /// </summary>
        private static Dictionary<Type, Dictionary<string, MethodBase>> methods = new Dictionary<Type, Dictionary<string, MethodBase>>();

        /// <summary>
        /// Cache of fields grouped by type.
        /// </summary>
        private static Dictionary<Type, Dictionary<string, FieldInfo>> fields = new Dictionary<Type, Dictionary<string, FieldInfo>>();

        /// <summary>
        /// Cache of properties grouped by type.
        /// </summary>
        private static Dictionary<Type, Dictionary<string, PropertyInfo>> properties = new Dictionary<Type, Dictionary<string, PropertyInfo>>();

        /// <summary>
        /// Valid access control modifiers ranked from least to most restrictive.
        /// </summary>
        private static string[] accessControlModifiers = { "public", "protected", "private" };


        /// <summary>
        /// All valid class item modifiers.
        /// </summary>
        private static string[] modifiers = accessControlModifiers.Concat(new [] { "readonly", "static" }).ToArray();

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

        /// <summary>
        /// IScalar wrapper for C# types.
        /// </summary>
        private class BCSharpType : IScalar {
            public Type Value { get; }

            public BCSharpType(Type value) {
                Value = value;
            }

            public static implicit operator BCSharpType(Type type) => new BCSharpType(type);

            public static implicit operator Type(BCSharpType type) => type.Value;

            public override string ToString() => Value.FullName;

            public virtual string Inspect() => $"C#Type({Value.FullName})";

            public object ToCSharp() => Value;

            public Type Type() => typeof(Type);
        }

        /// <summary>
        /// IScalar wrapper for Cauliflower native module types.
        /// </summary>
        private class BCauliflowerType : BCSharpType {
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

            public BCSharpMethod(Type type, string name) {
                Value = (type, name);
            }

            public override string ToString() => $"{Value.type.FullName}.{Value.name}";

            public virtual string Inspect() => $"C#Method({Value.type.FullName}.{Value.name})";

            public object ToCSharp() => Value;

            public Type Type() => typeof(Tuple<Type, string>);
        }


        /// <summary>
        /// IScalar wrapper for Cauliflower native method types.
        /// </summary>
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
            interpreter.Scope.Scalars[type.Value.Name] = type;
            foreach (var method in new HashSet<string>(type.Value.GetMethods(BindingFlags.Public | BindingFlags.Static).Select(method => method.Name)))
                interpreter.Scope.Scalars[type.Value.Name + '.' + method] = new BCSharpMethod(type, method);
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

        /// <summary>
        /// Create a C# value.
        /// </summary>
        /// <param name="type">Type of value to create.</param>
        /// <param name="parameters">Parameters to feed to the constructor</param>
        /// <returns>The created value.</returns>
        private static BCSharpValue CSharpCreate(BCSharpType type, IEnumerable<IValue> parameters) => new BCSharpValue(
            type.Value.GetConstructor(parameters.Select(p => p.Type()).ToArray())
                .Invoke(parameters.Select(p => p.ToCSharp()).ToArray())
        );

        /// <summary>
        /// Create a Cauliflower native value.
        /// </summary>
        /// <param name="type">Type of value to create.</param>
        /// <param name="parameters">Parameters to feed to the constructor.</param>
        /// <returns>The created value.</returns>
        private static IValue CauliflowerCreate(BCauliflowerType type, IEnumerable<IValue> parameters) => (IValue) (
            type.Value.GetConstructor(new[] { typeof(IValue[]) }).Invoke(new[] { parameters.ToArray() })
        );

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
            type.Value.GetMethod(name.Value).Invoke(instance?.ToCSharp(), new[] { args.ToArray() })
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
        private static void AssignCSharpProperty(BCSharpType type, BAtom name, IValue instance, IValue value) {
            type.Value.TryGetProperty(name.Value).SetValue(instance?.ToCSharp(), value.ToCSharp());
        }

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
        private static void AssignCSharpField(BCSharpType type, BAtom name, IValue instance, IValue value) {
            type.Value.TryGetField(name.Value).SetValue(instance?.ToCSharp(), value.ToCSharp());
        }

        /// <summary>
        /// Add Cauliflower native types to interpreter.
        /// </summary>
        /// <param name="type">Type containing wanted types.</param>
        /// <param name="space">Namespace to set value on.</param>
        private static void AddTypes(Type type, Scope.Tree<string, Scope> space) {
            var name = type.Name.Replace('_', '-');
            if (!(space.ContainsKey(name))) {
                space[name] = new Scope.Tree<string, Scope> { Value = new Scope() };
            }
            foreach (var nested in type.GetNestedTypes(BindingFlags.Public).Where(t => !t.Name.Contains("<"))) {
                AddTypes(nested, space[name]);
                if (nested.GetInterface("IValue") != null)
                    space.Value.Scalars[nested.Name.Replace('_', '-')] = (BCauliflowerType) nested;
            }
            foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Static))
                space[name].Value.Functions[method.Name.Replace('_', '-')] = new Function(
                    name, ~0, (_, innerArgs) => CauliflowerMethod(type, method.Name, null, innerArgs)
                );
        }

        /// <summary>
        /// A dictionary containing all the builtin functions of Cauliflower.
        /// </summary>
        public new static readonly Dictionary<string, IFunction> StaticBuiltins = new Dictionary<string, IFunction> {
            {"", new ShortCircuitFunction("", 1, (cauliflower, args) => {
                var e = (ValueExpression) args[0];
                if (e.IsValue)
                    return cauliflower.Run(e.Values.First());
                if (e.Values.Length == 0)
                    throw new Exception("Expected function name");
                var first = cauliflower.Run(e.Values[0]);
                if (first is BCSharpMethod method)
                    try {
                        return CSharpMethod(method.Value.type, method.Value.name, null, e.Values.Skip(1).Select(cauliflower.Run));
                    } catch {
                        throw new Exception($"C# method '{method.Value.name}' not found for specified arguments. Are you missing a 'c#-import'?");
                    }
                if (first is BCauliflowerMethod cauliflowerMethod)
                    try {
                        return CauliflowerMethod(cauliflowerMethod.Value.type, cauliflowerMethod.Value.name, null, e.Values.Skip(1).Select(cauliflower.Run));
                    } catch {
                        throw new Exception($"Cauliflower method '{cauliflowerMethod.Value.name}' not found for specified arguments. Are you missing an 'import'?");
                    }
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
                switch (args.Last()) {
                    case ScalarVar s:
                        return scope.Value[s];
                    case ListVar l:
                        return scope.Value[l];
                    case DictVar d:
                        return scope.Value[d];
                    case BAtom s:
                        return scope.Value[s.Value] ?? (IValue) scope[s.Value].Value;
                    default:
                        throw new ArgumentTypeException(args.Last(), "variable", args.Length, ".");
                }
            })},
            {"->", new ShortCircuitFunction("->", -2, (cauliflower, args) => {
                var result = cauliflower.Run(args[0]);
                var type = result.GetType();
                foreach (var (item, index) in args.Skip(1).WithIndex())
                    if (type == null)
                        throw new Exception("Cannot get property of method");
                    else if (item is BAtom a) {
                        var name = a.Value;
                        var members = result.GetType().GetMember(name);
                        if (members.Length != 0) {
                            switch (members[0].MemberType) {
                                case MemberTypes.Method:
                                    if (!methods.ContainsKey(type))
                                        methods[type] = new Dictionary<string, MethodBase>();
                                    if (!methods[type].ContainsKey(name))
                                        methods[type][name] = type.GetMethod(name);
                                    var method = methods[type][name];
                                    var obj = result;
                                    result = new Function(name, -1, (_, innerArgs) => (IValue) method.Invoke(obj, new object[] { cauliflower, innerArgs }));
                                    type = null;
                                    break;
                                case MemberTypes.Field:
                                    if (!fields.ContainsKey(type))
                                        fields[type] = new Dictionary<string, FieldInfo>();
                                    if (!fields[type].ContainsKey(name))
                                        fields[type][name] = type.GetField(name);
                                    var field = fields[type][name];
                                    type = (result = (IValue) field.GetValue(result)).GetType();
                                    break;
                                case MemberTypes.Property:
                                    if (!properties.ContainsKey(type))
                                        properties[type] = new Dictionary<string, PropertyInfo>();
                                    if (!properties[type].ContainsKey(name))
                                        properties[type][name] = type.GetProperty(name);
                                    var property = properties[type][name];
                                    type = (result = (IValue) property.GetValue(result)).GetType();
                                    break;
                            }
                        } else
                            throw new Exception($"Member '{a.Value}' not found in item");
                    } else
                        throw new ArgumentTypeException(item, "atom", index + 1, ".");
                return result;
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
                }, argExpressions.Values);
                return null;
            })},

            // Meta-commands
            {"new", new Function("new", -2, (cauliflower, args) => {
                if (!(args[0] is BCauliflowerType type))
                    throw new ArgumentTypeException(args[0], "Cauliflower type", 1, "new");
                return CauliflowerCreate(type, args.Skip(1));
            })},
            {"c#-import", new Function("c#-import", -2, (cauliflower, args) => {
                if (!(args[0] is BAtom a))
                    throw new ArgumentTypeException(args[0], "atom", 1, "c#-import");
                CSharpImport(cauliflower, a);
                return null;
            })},
            {"c#-new", new Function("c#-new", -2, (cauliflower, args) => {
                if (!(args[0] is BCSharpType type))
                    throw new ArgumentTypeException(args[0], "C# type", 1, "c#-new");
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
            {"import", new Function("import", -2, (cauliflower, args) => {
                if (args[0] is BAtom) {
                    CauliflowerInline.Import(cauliflower, args);
                    return null;
                }
                if (args.Length != 1)
                    throw new Exception($"Function 'import' requires exactly 1 argument, 3 provided");
                if (!(args[0] is BString s))
                    throw new ArgumentTypeException(args[0], "string", 1, "import");
                var tempCauliflower = new CauliflowerInterpreter();
                tempCauliflower.Run(File.ReadAllText(s.Value));
                foreach (var (key, value) in tempCauliflower.Scope.Functions)
                    cauliflower.Scope[key] = value;
                return null;
            })},
            {"import-static", new Function("import-static", -2, (cauliflower, args) => {
                if (!(args[0] is BAtom))
                    throw new ArgumentTypeException(args[0], "atom", 1, "import-static");
                CauliflowerInline.Import(cauliflower, args, true);
                return null;
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
                var scope = cauliflower.Scope.Namespaces[a.Value].Value = (cauliflower.Scope.Namespaces[a.Value].Value ?? new Scope());
                foreach (var (key, value) in tempCauliflower.Scope.Functions)
                    scope[key] = value;
                return null;
            })},
            {"class", new ShortCircuitFunction("class", ~0, (cauliflower, args) => {
                if (!(args[0] is BAtom name))
                    throw new ArgumentTypeException(args[0], "atom", 1, "class");

                // Generate initial class
                var asmName = new AssemblyName("CauliflowerGenerated-" + name.Value);
                var asmBuilder = AssemblyBuilder.DefineDynamicAssembly(asmName, AssemblyBuilderAccess.Run);
                var modBuilder = asmBuilder.DefineDynamicModule(asmName.Name + "-Module");
                var typeBuilder = modBuilder.DefineType(
                    name.Value,
                    TypeAttributes.Public | TypeAttributes.Class,
                    null,
                    new [] { typeof(IScalar) }
                );

                // Validate + collate statements
                var statements = CauliflowerInline.GetStatements(args.Skip(1));

                // Generate constructor params
                var ctorParamTuple = statements.FirstOrDefault(s => s.Item1 == "fn" && s.Item3.Values.ElementAtOrDefault(0) is BAtom fnName && fnName.Value == "init");
                var isCustomCtor =
                    !ctorParamTuple.Equals(default((string, List<string>, ValueExpression)));
                var ctorParams = new[] {typeof(CauliflowerInterpreter)};
                var ctorParamDecl = new ValueExpression();
                if (isCustomCtor) {
                    if (!(ctorParamTuple.Item3.Values.ElementAtOrDefault(1) is ValueExpression vexp))
                        throw new ArgumentTypeException(ctorParamTuple.Item3.Values.ElementAtOrDefault(1), "expression", 2, "fn");
                    ctorParams =
                        ctorParams.Concat(CauliflowerInline.GetParameterTypes(vexp)).ToArray();
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

                MethodAttributes MethodAttrsFromAllMods(IEnumerable<string> modifiers, MethodAttributes defaultAttr = MethodAttributes.Public) {
                    return modifiers.Aggregate(defaultAttr, (a, m) => a | MethodAttrFromMod(m));
                }

                FieldAttributes FieldAttrsFromMods(IEnumerable<string> modifiers) {
                    return modifiers.Aggregate(FieldAttributes.Private, (attr, modifier) => {
                        FieldAttributes newAttr;

                        switch (modifier) {
                            case "public":
                                newAttr = FieldAttributes.Public;
                                break;
                            case "private":
                                newAttr = FieldAttributes.Private;
                                break;
                            case "protected":
                                newAttr = FieldAttributes.Family;
                                break;
                            case "static":
                                newAttr = FieldAttributes.Static;
                                break;
                            default:
                                throw new Exception($"Unrecognized access modifier '{modifier}'");
                        }

                        return attr | newAttr;
                    });
                }

                // Generate constructor
                var ctor = typeBuilder.DefineConstructor(
                    MethodAttrsFromAllMods(ctorParamTuple.Item2 ?? new List<string>()),
                    CallingConventions.Standard,
                    ctorParams
                );
                var ctorIL = ctor.GetILGenerator();
                ctorIL.Emit(OpCodes.Ldarg_0);
                ctorIL.Emit(OpCodes.Call, typeof(object).GetConstructor(Type.EmptyTypes));

                // Add (interpreter) field
                var interpreterField = typeBuilder.DefineField(
                    "(interpreter)",
                    typeof(CauliflowerInterpreter),
                    FieldAttributes.Private
                );
                ctorIL.Emit(OpCodes.Ldarg_0); // Load to set interpreter
                ctorIL.Emit(OpCodes.Ldarg_1); // Interpreter param
                ctorIL.Emit(OpCodes.Stfld, interpreterField);

                foreach (var (sName, sModifiers, sArgs) in statements) {
                    Console.WriteLine(string.Join(", ", sName, $"[{string.Join(", ", sModifiers)}]", sArgs));

                    switch (sName) {
                        case "field":
                            if (!(sArgs.Values.ElementAtOrDefault(0) is BAtom fieldName))
                                throw new ArgumentTypeException(sArgs.Values.ElementAtOrDefault(0), "atom", 1, "field");

                            var newField = typeBuilder.DefineField(
                                fieldName.Value,
                                typeof(IValue),
                                FieldAttrsFromMods(sModifiers)
                            );

                            var initFieldVal = sArgs.Values.ElementAtOrDefault(1);
                            if (initFieldVal != null) {
                                // Init value inside constructor
                                ctorIL.Emit(OpCodes.Ldarg_0);
                                CauliflowerInline.LoadInterpreterInvocation(ctorIL, interpreterField, initFieldVal);
                                ctorIL.Emit(OpCodes.Stfld, newField);
                            }

                            break;
                        case "prop":
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
                                FieldAttrsFromMods(sModifiers)
                            );

                            if (propStatements.Any(a => a.Item2.FirstOrDefault(accessControlModifiers.Contains) != null) && propStatements.All(a => a.Item2 != null))
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
                                            getterIL.Emit(OpCodes.Ldarg_0);
                                            getterIL.Emit(OpCodes.Ldfld, backingField);
                                        } else {
                                            CauliflowerInline.CreateNewScope(getterIL, interpreterField);
                                            CauliflowerInline.AddThisToScope(getterIL, interpreterField);
                                            CauliflowerInline.LoadInterpreterInvocation(getterIL, interpreterField, aArgs);
                                            CauliflowerInline.ReturnToParentScope(getterIL, interpreterField);
                                        }
                                        getterIL.Emit(OpCodes.Ret);

                                        newProp.SetGetMethod(getter);
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
                                            setterIL.Emit(OpCodes.Ldarg_0);
                                            setterIL.Emit(OpCodes.Ldarg_1);
                                            setterIL.Emit(OpCodes.Stfld, backingField);
                                        } else {
                                            CauliflowerInline.CreateNewScope(setterIL, interpreterField);
                                            CauliflowerInline.AddThisToScope(setterIL, interpreterField);
                                            CauliflowerInline.LoadInterpreterInvocation(setterIL, interpreterField, aArgs);
                                            CauliflowerInline.ReturnToParentScope(setterIL, interpreterField);
                                        }
                                        setterIL.Emit(OpCodes.Ret);

                                        newProp.SetSetMethod(setter);
                                        break;
                                    default:
                                        throw new Exception($"Unrecognized property accessor '{aType}'");
                                }
                            }

                            var initPropVal = sArgs.Values.ElementAtOrDefault(2);

                            // Init value inside constructor
                            if (initPropVal != null) {
                                if (propStatements.Any(a => a.Item3.Values.Length <= 0))
                                    throw new Exception($"Only auto-properties can have initial values ('{propName.Value}')");

                                ctorIL.Emit(OpCodes.Ldarg_0);
                                CauliflowerInline.CreateNewScope(ctorIL, interpreterField);
                                CauliflowerInline.LoadInterpreterInvocation(ctorIL, interpreterField, initPropVal);
                                CauliflowerInline.ReturnToParentScope(ctorIL, interpreterField);
                                ctorIL.Emit(OpCodes.Stfld, backingField);
                            }
                            break;
                        case "fn":
                            if (!(sArgs.Values.ElementAtOrDefault(0) is BAtom fnName))
                                throw new ArgumentTypeException(sArgs.Values.ElementAtOrDefault(0), "atom", 1, "fn");

                            if (!(sArgs.Values.ElementAtOrDefault(1) is ValueExpression vexp2))
                                throw new ArgumentTypeException(sArgs.Values.ElementAtOrDefault(1), "expression", 2, "fn");

                            if (fnName.Value == "init") continue;
                            var newMethod = typeBuilder.DefineMethod(
                                fnName.Value,
                                MethodAttrsFromAllMods(sModifiers),
                                typeof(IValue),
                                CauliflowerInline.GetParameterTypes(vexp2)
                            );
                            var methodIL = newMethod.GetILGenerator();

                            // Add new function scope + populate
                            CauliflowerInline.CreateNewScope(methodIL, interpreterField);
                            CauliflowerInline.AddThisToScope(methodIL, interpreterField);
                            CauliflowerInline.AddParametersToScope(methodIL, interpreterField, vexp2);

                            // Invoke interpreter, back out of scope, return
                            CauliflowerInline.LoadInterpreterInvocation(methodIL, interpreterField, new ValueExpression(sArgs.Values.Skip(2)));
                            CauliflowerInline.ReturnToParentScope(methodIL, interpreterField);
                            methodIL.Emit(OpCodes.Ret);
                            break;
                        default:
                            throw new Exception($"Unrecognized class definition statement '{sName}'");
                    }
                }

                // Custom constructor implementation (after initial values)
                if (isCustomCtor) {
                    CauliflowerInline.AddParametersToScope(ctorIL, interpreterField, ctorParamDecl);
                    CauliflowerInline.CreateNewScope(ctorIL, interpreterField);
                    CauliflowerInline.LoadInterpreterInvocation(ctorIL, interpreterField, new ValueExpression(ctorParamTuple.Item3.Values.Skip(2)));
                    CauliflowerInline.ReturnToParentScope(ctorIL, interpreterField);
                    ctorIL.Emit(OpCodes.Ret);
                }

                // TODO: place in scope somewhere?
                var classType = typeBuilder.CreateType();

                Console.WriteLine(string.Join<Type>(", ", ctorParams));

                // FIXME
                var testInstance = classType.GetConstructor(ctorParams)
                    .Invoke(new object[] {cauliflower, new BInteger(1)});
                classType.InvokeMember("foo", BindingFlags.InvokeMethod, null, testInstance,
                    Array.Empty<object>());

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
            {"+", new Function("+", ~0, (broccoli, args) => {
                if (args.Length == 0 || args[0] is BInteger || args[0] is BFloat)
                    foreach (var (value, index) in args.WithIndex()) {
                        if (!(value is BInteger || value is BFloat))
                            throw new ArgumentTypeException(value, "integer or float", index + 1, "+");
                    }
                else {
                    var type = args[0].GetType();
                    string typeString = new Dictionary<Type, string>{
                        {typeof(BString), "string"},
                        {typeof(BAtom), "atom"},
                        {typeof(BList), "list"},
                        {typeof(BDictionary), "dictioanry"}
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

                return args.Aggregate((IValue) new BInteger(1), (m, v) => {
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

            {"not", new Function("not", 1, (cauliflower, args) => Boolean(CauliflowerInline.Truthy(args[0]).Equals(BAtom.Nil)))},
            {"and", new ShortCircuitFunction("and", ~0, (cauliflower, args) =>
                Boolean(!args.Any(arg => CauliflowerInline.Truthy(cauliflower.Run(arg))))
            )},
            {"or", new ShortCircuitFunction("or", ~0, (cauliflower, args) =>
                Boolean(args.Any(arg => !CauliflowerInline.Truthy(cauliflower.Run(arg))))
            )},

            {"\\", new ShortCircuitFunction("\\", ~1, (cauliflower, args) => {
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

            {"if", new ShortCircuitFunction("if", ~2, (cauliflower, args) => {
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
            {"slice", new Function("slice", ~2, (cauliflower, args) => {
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
            {"range", new Function("range", ~1, (cauliflower, args) => {
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

            /// <summary>
            /// Imports a Cauliflower native module.
            /// </summary>
            /// <param name="cauliflower">Interpreter instance wanting to import module</param>
            /// <param name="path">Path to module</param>
            /// <param name="isStatic">Whether to static import module</param>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static void Import(Interpreter cauliflower, IValue[] path, bool isStatic = false) {
                if (cauliflower.Scope.Namespaces.Value == null)
                    cauliflower.Scope.Namespaces.Value = new Scope();
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
                StringBuilder fullModuleName = new StringBuilder("cauliflower.");
                StringBuilder fullTypeName = new StringBuilder();
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
                        AddTypes(nested, cauliflower.Scope.Namespaces);
                        if (isStatic)
                            foreach (var method in nested.GetMethods(BindingFlags.Public | BindingFlags.Static)) {
                                var name = method.Name.Replace('_', '-');
                                cauliflower.Scope.Functions[method.Name.Replace('_', '-')] = new Function(
                                    name, ~0, (_, innerArgs) => CauliflowerMethod(nested, method.Name, null, innerArgs)
                                );
                            }
                        else
                            if (nested.GetInterface("IValue") != null)
                                cauliflower.Scope.Namespaces.Value.Scalars[nested.Name.Replace('_', '-')] = (BCauliflowerType) nested;
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
                    int i = 0;
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
                        case ScalarVar s:
                            results[index] = typeof(IScalar);
                            break;
                        case ListVar l:
                            results[index] = typeof(BList);
                            break;
                        case DictVar d:
                            results[index] = typeof(BDictionary);
                            break;
                        // Rest args
                        case ValueExpression v:
                            if (!(v.Values.ElementAtOrDefault(0) is ListVar rest))
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
            public static void LoadInterpreterReference(ILGenerator gen, FieldInfo interpreterField) {
                gen.Emit(OpCodes.Ldarg_0);
                gen.Emit(OpCodes.Ldfld, interpreterField);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static void LoadScopeReference(ILGenerator gen, FieldInfo interpreterField) {
                LoadInterpreterReference(gen, interpreterField);
                gen.Emit(OpCodes.Ldfld, typeof(Interpreter).GetField("Scope"));
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static void CreateNewScope(ILGenerator gen, FieldInfo interpreterField) {
                LoadInterpreterReference(gen, interpreterField); // Load to store in scope
                LoadScopeReference(gen, interpreterField); // Load to get scope value
                gen.Emit(OpCodes.Newobj, typeof(Scope).GetConstructor(new [] { typeof(Scope) })); // Create new scope
                gen.Emit(OpCodes.Stfld, typeof(Interpreter).GetField("Scope"));
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static void ReturnToParentScope(ILGenerator gen, FieldInfo interpreterField) {
                LoadInterpreterReference(gen, interpreterField);
                LoadScopeReference(gen, interpreterField);
                gen.Emit(OpCodes.Ldfld, typeof(Scope).GetField("Parent"));
                gen.Emit(OpCodes.Stfld, typeof(Interpreter).GetField("Scope"));
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static void AddThisToScope(ILGenerator gen, FieldInfo interpreterField) {
                LoadScopeReference(gen, interpreterField);
                gen.Emit(OpCodes.Ldfld, typeof(Scope).GetField("Scalars"));
                gen.Emit(OpCodes.Ldstr, "this");
                gen.Emit(OpCodes.Ldarg_0);
                gen.Emit(OpCodes.Callvirt, typeof(Dictionary<string, IValue>).GetMethod("set_Item"));
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static void LoadInterpreterInvocation(ILGenerator gen, FieldInfo interpreterField, IValueExpressible expr) {
                LoadInterpreterReference(gen, interpreterField);
                gen.Emit(OpCodes.Ldstr, expr.Inspect());
                gen.Emit(OpCodes.Callvirt, typeof(CauliflowerInterpreter).GetMethod("Run", new [] {typeof(string)}));
            }

            /// <summary>
            /// Populates a function's scope with values from the given parameter expression.
            /// </summary>
            /// <param name="methILGen">The <see cref="ILGenerator"/> for the method.</param>
            /// <param name="interpreterField">The <see cref="FieldInfo"/> that represents the interpreter field in the class.</param>
            /// <param name="paramExp">The parameter expression.</param>
            /// <exception cref="ArgumentTypeException">Throws when parameter types are invalid.</exception>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static void AddParametersToScope(ILGenerator methILGen, FieldInfo interpreterField, ValueExpression paramExp) {
                foreach (var (param, index) in paramExp.Values.WithIndex()) {
                    LoadScopeReference(methILGen, interpreterField);
                    switch (param) {
                        case ScalarVar s:
                            methILGen.Emit(OpCodes.Ldfld, typeof(Scope).GetField("Scalars"));
                            methILGen.Emit(OpCodes.Ldstr, s.Value);
                            methILGen.Emit(OpCodes.Ldarg_S, index);
                            methILGen.Emit(OpCodes.Callvirt, typeof(Dictionary<string, IValue>).GetMethod("set_Item"));
                            break;
                        case ListVar l:
                            methILGen.Emit(OpCodes.Ldfld, typeof(Scope).GetField("Lists"));
                            methILGen.Emit(OpCodes.Ldstr, l.Value);
                            methILGen.Emit(OpCodes.Ldarg_S, index);
                            methILGen.Emit(OpCodes.Callvirt, typeof(Dictionary<string, BList>).GetMethod("set_Item"));
                            break;
                        case DictVar d:
                            methILGen.Emit(OpCodes.Ldfld, typeof(Scope).GetField("Dictionaries"));
                            methILGen.Emit(OpCodes.Ldstr, d.Value);
                            methILGen.Emit(OpCodes.Ldarg_S, index);
                            methILGen.Emit(OpCodes.Callvirt, typeof(Dictionary<string, BDictionary>).GetMethod("set_Item"));
                            break;
                        // Rest args
                        case ValueExpression v:
                            if (!(v.Values.ElementAtOrDefault(0) is ListVar rest))
                                throw new ArgumentTypeException(v.Values.ElementAtOrDefault(0), "rest arguments list variable", index + 1, "fn parameters");

                            methILGen.Emit(OpCodes.Ldfld, typeof(Scope).GetField("Lists"));
                            methILGen.Emit(OpCodes.Ldstr, rest.Value);
                            methILGen.Emit(OpCodes.Ldarg_S, index);
                            methILGen.Emit(OpCodes.Callvirt, typeof(Dictionary<string, BList>).GetMethod("set_Item"));
                            break;
                        default:
                            throw new ArgumentTypeException(param, "variable name", index + 1, "fn parameters");
                    }
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

        /// <summary>/
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