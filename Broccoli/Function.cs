using System;
using System.Collections.Generic;
using System.Linq;

namespace Broccoli {
    /// <summary>
    ///     Represents a class that can act as a Broccoli function.
    /// </summary>
    public interface IFunction : IScalar {
        /// <summary>
        ///     Invokes the Broccoli function with the given arguments.
        /// </summary>
        /// <param name="broccoli">The current Broccoli interpreter context.</param>
        /// <param name="args">The arguments to evaluate the function with.</param>
        /// <returns>The return value of the function.</returns>
        IValue Invoke(Interpreter broccoli, params IValueExpressible[] args);
    }

    /// <summary>
    ///     Represents the main type of Broccoli function that evaluates all subexpressions.
    /// </summary>
    public struct Function : IFunction {
        public delegate IValue Call(Interpreter broccoli, IValue[] args);

        private readonly string                         _name;
        private readonly int                            _argc;
        private readonly Call                           _call;
        private readonly IEnumerable<IValueExpressible> _args;

        // If the function has variadic arguments, argc is -n - 1, where n is the number of required args
        public Function(string name, int argc, Call call, IEnumerable<IValueExpressible> args = null) {
            _name = name;
            _argc = argc;
            _call = call;
            _args = args;
        }

        // ReSharper disable once UnusedMethodReturnValue.Global
        public IValue Invoke(Interpreter broccoli, params IValueExpressible[] args) {
            var runArgs = args.ToList().Select(broccoli.Run).ToArray();
            ValidateArgs(_argc, runArgs, _name);
            return _call(broccoli, runArgs);
        }

        /// <summary>
        ///     Validates that a function is being supplied the correct number of arguments.
        /// </summary>
        /// <param name="argc">
        ///     The total argument count.
        ///     If the function has variadic arguments, the argument count is -(required argument count) - 1.
        /// </param>
        /// <param name="args">The arguments to check.</param>
        /// <param name="name">The name of the function being called.</param>
        /// <typeparam name="T">The type of the arguments.</typeparam>
        /// <exception cref="Exception">Throws when the amount of arguments given does not match with the given argument count.</exception>
        public static void ValidateArgs<T>(int argc, T[] args, string name) {
            var isVariadic   = argc < 0;
            var requiredArgc = isVariadic ? -argc - 1 : argc;

            if (isVariadic ? args.Length < requiredArgc : args.Length != requiredArgc)
                throw new Exception(
                    $"Function '{name}' requires {(isVariadic ? "at least" : "exactly")} {requiredArgc} arguments, {args.Length} provided"
                );
        }

        public override string ToString() => _args == null
            ? $"{_name}({(_argc < 0 ? (~_argc).ToString() + '+' : _argc.ToString())} argument{(_argc == 1 ? "" : "s")})"
            : $"{_name}({string.Join(' ', _args.Select(arg => arg.Inspect()))})";

        public string Inspect() => ToString();

        public object ToCSharp() => _call;

        public Type Type() => typeof(Call);

        public IScalar ScalarContext() => this;

        public IList ListContext() => throw new NoListContextException(this);

        public IDictionary DictionaryContext() => throw new NoDictionaryContextException(this);
    }

    /// <summary>
    ///     Represents a Broccoli function that keeps all subexpressions unevaluated.
    /// </summary>
    public struct ShortCircuitFunction : IFunction {
        public delegate IValue Call(Interpreter broccoli, IValueExpressible[] args);

        private readonly string                         _name;
        private readonly int                            _argc;
        private readonly Call                           _call;
        private readonly IEnumerable<IValueExpressible> _args;

        // If the function has variadic arguments, argc is -n-1, where n is the number of required args
        public ShortCircuitFunction(string name, int argc, Call call, IEnumerable<IValueExpressible> args = null) {
            _name = name;
            _argc = argc;
            _call = call;
            _args = args;
        }

        // ReSharper disable once UnusedMethodReturnValue.Global
        public IValue Invoke(Interpreter broccoli, params IValueExpressible[] args) {
            Function.ValidateArgs(_argc, args, _name);
            return _call(broccoli, args);
        }

        public override string ToString() => _args == null
            ? $"{_name}({(_argc < 0 ? (~_argc).ToString() + '+' : _argc.ToString())} argument{(_argc == 1 ? "" : "s")})"
            : $"{_name}({string.Join(' ', _args.Select(arg => arg.Inspect()))})";

        public string Inspect() => ToString();

        public object ToCSharp() => _call;

        public Type Type() => typeof(Call);

        public IScalar ScalarContext() => this;

        public IList ListContext() => throw new NoListContextException(this);

        public IDictionary DictionaryContext() => throw new NoDictionaryContextException(this);
    }

    /// <summary>
    ///     Represents a Cauliflower function that is also a value.
    /// </summary>
    public struct AnonymousFunction : IFunction {
        public delegate IValue Call(IValue[] args);

        private readonly int                            _argc;
        private readonly Call                           _call;
        private readonly IEnumerable<IValueExpressible> _args;

        public AnonymousFunction(int argc, Call call, IEnumerable<IValueExpressible> args = null) {
            _argc = argc;
            _call = call;
            _args = args;
        }

        public IValue Invoke(Interpreter broccoli, params IValueExpressible[] args) {
            var runArgs = args.ToList().Select(broccoli.Run).ToArray();
            Function.ValidateArgs(_argc, runArgs, "(anonymous)");
            return _call(runArgs);
        }

        public override string ToString() => _args == null
            ? $"<anonymous>({(_argc < 0 ? (~_argc).ToString() + '+' : _argc.ToString())} argument{(_argc == 1 ? "" : "s")})"
            : $"<anonymous>({string.Join(' ', _args.Select(arg => arg.Inspect()))})";

        public string Inspect() => ToString();

        public object ToCSharp() => _call;

        public Type Type() => typeof(Call);

        public IScalar ScalarContext() => this;

        public IList ListContext() => throw new NoListContextException(this);

        public IDictionary DictionaryContext() => throw new NoDictionaryContextException(this);
    }
}
