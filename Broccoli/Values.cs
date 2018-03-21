using System.Text;
using System.Collections.Generic;
using System.Linq;
using System;

namespace Broccoli {
    /// <inheritdoc />
    /// <summary>
    /// Represents a class that can be used as a value within a Broccoli program.
    /// </summary>
    public interface IValue : IValueExpressible {
        object ToCSharp();
        Type Type();
    }

    public interface IScalar : IValue { }

    /// <summary>
    /// Represents a 64-bit signed integer.
    /// </summary>
    public class BInteger : IScalar {
        public long Value { get; }

        public BInteger(long i) {
            Value = i;
        }

        public static implicit operator BInteger(char i) => new BInteger(i);

        public static implicit operator BInteger(short i) => new BInteger(i);

        public static implicit operator BInteger(int i) => new BInteger(i);

        public static implicit operator BInteger(long i) => new BInteger(i);

        public static implicit operator BInteger(float f) => new BInteger((int) f);

        public static implicit operator BInteger(double f) => new BInteger((int) f);

        public static implicit operator char (BInteger i) => (char) i.Value;

        public static implicit operator short (BInteger i) => (short) i.Value;

        public static implicit operator int (BInteger i) => (int) i.Value;

        public static implicit operator long (BInteger i) => i.Value;

        public static implicit operator float (BInteger i) => i.Value;

        public static implicit operator double (BInteger i) => i.Value;

        public static implicit operator BInteger(BFloat f) => new BInteger((int) f.Value);

        public static bool operator ==(BInteger left, object right) => right is BInteger i && left.Value == i.Value;

        public static bool operator !=(BInteger left, object right) => right is BInteger i && left.Value != i.Value;

        public override bool Equals(object other) => other is BInteger i && Value == i.Value;

        public override int GetHashCode() => Value.GetHashCode();

        public override string ToString() => Value.ToString();

        public string Inspect() => Value.ToString();

        public object ToCSharp() => Value;

        public Type Type() => typeof(long);
    }

    /// <summary>
    /// Represents a double-precision float.
    /// </summary>
    public class BFloat : IScalar {
        public double Value { get; }

        public BFloat(double f) {
            Value = f;
        }

        public static implicit operator BFloat(char i) => new BFloat(i);

        public static implicit operator BFloat(short i) => new BFloat(i);

        public static implicit operator BFloat(int i) => new BFloat(i);

        public static implicit operator BFloat(long i) => new BFloat(i);

        public static implicit operator BFloat(float f) => new BFloat(f);

        public static implicit operator BFloat(double f) => new BFloat(f);

        public static implicit operator char(BFloat f) => (char) f.Value;

        public static implicit operator short(BFloat f) => (short) f.Value;

        public static implicit operator int(BFloat f) => (int) f.Value;

        public static implicit operator long(BFloat f) => (long) f.Value;

        public static implicit operator float(BFloat f) => (float) f.Value;

        public static implicit operator double(BFloat f) => f.Value;

        public static implicit operator BFloat(BInteger i) => new BFloat(i.Value);

        public static bool operator ==(BFloat left, object right) => right is BFloat f && left.Value == f.Value;

        public static bool operator !=(BFloat left, object right) => right is BFloat f && left.Value != f.Value;

        public override bool Equals(object other) => other is BFloat f && Value == f.Value;

        public override int GetHashCode() => Value.GetHashCode();

        public override string ToString() => Value.ToString();

        public string Inspect() => Value.ToString();

        public object ToCSharp() => Value;

        public Type Type() => typeof(double);
    }

    /// <summary>
    /// Represents a string of characters.
    /// </summary>
    public class BString : IScalar {
        public string Value { get; }

        public BString(string s) {
            Value = s;
        }

        public static implicit operator BString(string s) => new BString(s);

        public static bool operator ==(BString left, object right) => right is BString s && left.Value == s.Value;

        public static bool operator !=(BString left, object right) => right is BString s && left.Value != s.Value;

        public override bool Equals(object other) => (other is BString s) && Value == s.Value;

        public override int GetHashCode() => Value.GetHashCode();

        public override string ToString() => Value;

        public string Inspect() => $"\"{System.Text.RegularExpressions.Regex.Escape(Value)}\"";

        public object ToCSharp() => Value;

        public Type Type() => typeof(string);
    }

    /// <summary>
    /// Represents a bare atomic value.
    /// </summary>
    public class BAtom : IScalar {
        public static readonly BAtom True = new BAtom("t");
        public static readonly BAtom Nil = new BAtom("nil");

        public string Value { get; }

        public BAtom(string a) {
            Value = a;
        }

        public static implicit operator BAtom(string a) => new BAtom(a);

        public static bool operator ==(BAtom left, object right) => right is BAtom a && left.Value == a.Value;

        public static bool operator !=(BAtom left, object right) => right is BAtom a && left.Value != a.Value;

        public override bool Equals(object other) => (other is BAtom a) && Value == a.Value;

        public override int GetHashCode() => Value.GetHashCode();

        public override string ToString() => Value;

        public string Inspect() => Value;

        public object ToCSharp() {
            switch (Value) {
                case "t":
                    return true;
                case "nil":
                    return false;
                default:
                    return Value;
            }
        }

        public Type Type() => typeof(string);
    }

    /// <summary>
    /// Represents a scalar variable.
    /// </summary>
    public class ScalarVar : IScalar {
        public string Value { get; }

        public ScalarVar(string s) {
            Value = s;
        }

        public override string ToString() => Value;

        public string Inspect() => '$' + Value;

        public object ToCSharp() => Value;

        public Type Type() => typeof(string);
    }

    /// <summary>
    /// Represents a list variable.
    /// </summary>
    public class ListVar : IScalar {
        public string Value { get; }

        public ListVar(string l) {
            Value = l;
        }

        public override string ToString() => Value;

        public string Inspect() => '@' + Value;

        public object ToCSharp() => Value;

        public Type Type() => typeof(string);
    }

    /// <summary>
    /// Represents a dictionary variable.
    /// </summary>
    public class DictVar : IScalar {
        public string Value { get; }

        public DictVar(string d) {
            Value = d;
        }

        public override string ToString() => Value;

        public string Inspect() => '%' + Value;

        public object ToCSharp() => Value;

        public Type Type() => typeof(string);
    }

    /// <summary>
    /// Represents a list of values.
    /// </summary>
    public class BList : List<IValue>, IValue {
        public BList Value { get; }

        public BList() : base() => Value = this;

        public BList(IEnumerable<IValue> values) : base(values) => Value = this;

        public BList(params IValue[] values) : base(values) => Value = this;

        public static implicit operator BList(IValue[] values) => new BList(values);

        public static bool operator ==(BList left, object right) => right is BList list && left.Equals(list);

        public static bool operator !=(BList left, object right) => !(left == right);

        public override bool Equals(object other) {
            if (!(other is BList otherList) || otherList.Count != Count)
                return false;
            for (var i = 0; i < Count; i++)
                if (!this[i].Equals(otherList[i]))
                    return false;
            return true;
        }

        public override int GetHashCode() => this.Aggregate(0, (p, c) => p * -1521134295 + c.GetHashCode());

        public override string ToString() => '(' + string.Join(' ', Value) + ')';

        public string Inspect() => '(' + string.Join(' ', Value.Select(value => value.Inspect())) + ')';

        public object ToCSharp() => new BCSharpList(Value.Select(item => item.ToCSharp()));

        public Type Type() => typeof(List<object>);
    }

    public class BCSharpList : List<object>, IValue {
        public BCSharpList Value { get; }

        public BCSharpList() : base() => Value = this;

        public BCSharpList(IEnumerable<object> values) : base(values) => Value = this;

        public BCSharpList(params object[] values) : base(values) => Value = this;

        public static bool operator ==(BCSharpList left, object right) => right is BCSharpList list && left.Equals(list);

        public static bool operator !=(BCSharpList left, object right) => !(left == right);

        public override bool Equals(object other) {
            if (!(other is BCSharpList otherList) || otherList.Count != Count)
                return false;
            for (var i = 0; i < Count; i++)
                if (!this[i].Equals(otherList[i]))
                    return false;
            return true;
        }

        public override int GetHashCode() => this.Aggregate(0, (p, c) => p * -1521134295 + c.GetHashCode());

        public override string ToString() => '(' + string.Join(' ', Value) + ')';

        public string Inspect() => '(' + string.Join(' ', Value) + ')';

        public object ToCSharp() => Value;

        public Type Type() => typeof(List<object>);
    }

    /// <summary>
    /// Represents a dictionary of values paired with other values.
    /// </summary>
    public class BDictionary : Dictionary<IValue, IValue>, IValue {
        public BDictionary Value { get; }

        public BDictionary() : base() => Value = this;

        public BDictionary(IReadOnlyDictionary<IValue, IValue> values) : base(values) => Value = this;

        public static bool operator ==(BDictionary left, object right) => right is IReadOnlyDictionary<IValue, IValue> rdict
            && rdict.Count == left.Count && !left.Except(rdict).Any();
        public static bool operator !=(BDictionary left, object right) => !(left == right);

        public override bool Equals(object obj) => obj is IReadOnlyDictionary<IValue, IValue> rdict
            && rdict.Count == Count && !this.Except(rdict).Any();

        public override int GetHashCode() => this.Aggregate(0, (p, c) => p + c.Key.GetHashCode() + c.Value.GetHashCode());

        public override string ToString() {
            var sb = new StringBuilder("(");
            foreach (KeyValuePair<IValue, IValue> i in this)
                sb.Append($"{i.Key}: {i.Value}, ");
            if (sb.Length > 1)
                sb.Length -= 2;
            sb.Append(")");
            return sb.ToString();
        }

        public string Inspect() {
            var sb = new StringBuilder("(");
            foreach (KeyValuePair<IValue, IValue> i in this)
                sb.Append($"{i.Key.Inspect()}: {i.Value.Inspect()}, ");
            if (sb.Length > 1)
                sb.Length -= 2;
            sb.Append(")");
            return sb.ToString();
        }

        public object ToCSharp() => new BCSharpDictionary(Value.ToDictionary(item => item.Key.ToCSharp(), item => item.Value.ToCSharp()));

        public Type Type() => typeof(Dictionary<IValue, IValue>);
    }

    public class BCSharpDictionary : Dictionary<object, object>, IValue {
        public BCSharpDictionary Value { get; }

        public BCSharpDictionary() : base() => Value = this;

        public BCSharpDictionary(IReadOnlyDictionary<object, object> values) : base(values) => Value = this;

        public static bool operator ==(BCSharpDictionary left, object right) => right is IReadOnlyDictionary<object, object> rdict
            && rdict.Count == left.Count && !left.Except(rdict).Any();
        public static bool operator !=(BCSharpDictionary left, object right) => !(left == right);

        public override bool Equals(object obj) => obj is IReadOnlyDictionary<object, object> rdict
            && rdict.Count == Count && !this.Except(rdict).Any();

        public override int GetHashCode() => this.Aggregate(0, (p, c) => p + c.Key.GetHashCode() + c.Value.GetHashCode());

        public override string ToString() {
            var sb = new StringBuilder("(");
            foreach (var i in this)
                sb.Append($"{i.Key}: {i.Value}, ");
            if (sb.Length > 1)
                sb.Length -= 2;
            sb.Append(")");
            return sb.ToString();
        }

        public string Inspect() {
            var sb = new StringBuilder("(");
            foreach (var i in this)
                sb.Append($"{i.Key}: {i.Value}, ");
            if (sb.Length > 1)
                sb.Length -= 2;
            sb.Append(")");
            return sb.ToString();
        }

        public object ToCSharp() => Value;

        public Type Type() => typeof(Dictionary<object, object>);
    }
}