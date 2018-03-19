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
        string Inspect();
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

        public static implicit operator BInteger(int i) => new BInteger(i);

        public static implicit operator BInteger(long i) => new BInteger(i);

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

        public static implicit operator BFloat(float f) => new BFloat(f);

        public static implicit operator BFloat(double f) => new BFloat(f);

        public static implicit operator BFloat(BInteger f) => new BFloat(f.Value);

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

        public object ToCSharp() => Value;

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
    public class ValueList : List<IValue>, IValue {
        public ValueList Value { get; }

        public ValueList() : base() => Value = this;

        public ValueList(IEnumerable<IValue> values) : base(values) => Value = this;

        public ValueList(params IValue[] values) : base(values) => Value = this;

        public static implicit operator ValueList(IValue[] values) => new ValueList(values);

        public static bool operator ==(ValueList left, object right) => right is ValueList list && left.Equals(list);

        public static bool operator !=(ValueList left, object right) => !(left == right);

        public override bool Equals(object other) {
            if (!(other is ValueList otherList) || otherList.Count != Count)
                return false;
            for (var i = 0; i < Count; i++)
                if (!this[i].Equals(otherList[i]))
                    return false;
            return true;
        }

        public override int GetHashCode() => this.Aggregate(0, (p, c) => p * -1521134295 + c.GetHashCode());

        public override string ToString() => '(' + string.Join(' ', Value) + ')';

        public string Inspect() => '(' + string.Join(' ', Value.Select(value => value.Inspect())) + ')';

        public object ToCSharp() => new List<IValue>(Value);

        public Type Type() => typeof(List<IValue>);
    }

    /// <summary>
    /// Represents a dictionary of values paired with other values.
    /// </summary>
    public class ValueDictionary : Dictionary<IValue, IValue>, IValue {
        public ValueDictionary Value { get; }
        public ValueDictionary() : base() => Value = this;
        public ValueDictionary(IReadOnlyDictionary<IValue, IValue> values) : base(values) => Value = this;

        public static bool operator ==(ValueDictionary left, object right) => right is IReadOnlyDictionary<IValue, IValue> rdict
            && rdict.Count == left.Count && !left.Except(rdict).Any();
        public static bool operator !=(ValueDictionary left, object right) => !(left == right);

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

        public object ToCSharp() => new Dictionary<IValue, IValue>(Value);

        public Type Type() => typeof(Dictionary<IValue, IValue>);
    }
}