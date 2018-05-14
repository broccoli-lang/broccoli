using System.Text;
using System.Collections.Generic;
using System.Linq;
using System;
using System.Globalization;
// ReSharper disable InconsistentNaming

namespace Broccoli {
    /// <inheritdoc />
    /// <summary>
    /// Represents a class that can be used as a value within a Broccoli program.
    /// </summary>
    public interface IValue : IValueExpressible {
        object ToCSharp();
        Type Type();

        IScalar ScalarContext();
        IList ListContext();
        IDictionary DictionaryContext();
    }

    public interface IScalar : IValue { }

    public interface IList : IValue, IEnumerable<IValue> { }

    public interface IDictionary : IValue, IDictionary<IValue, IValue> { }

    /// <summary>
    /// Represents a 64-bit signed integer.
    /// </summary>
    public class BInteger : IScalar {
        public long Value { get; }

        public BInteger(short i) => Value = i;

        public BInteger(char i) => Value = i;

        public BInteger(int i) => Value = i;

        public BInteger(long i) => Value = i;

        public BInteger(float f) => Value = (long) f;

        public BInteger(double f) => Value = (long) f;

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

        public IScalar ScalarContext() => this;

        public IList ListContext() => throw new NoListContextException(this);

        public IDictionary DictionaryContext() => throw new NoDictionaryContextException(this);
    }

    /// <summary>
    /// Represents a double-precision float.
    /// </summary>
    public class BFloat : IScalar {
        public double Value { get; }

        public BFloat(short i) => Value = i;

        public BFloat(char i) => Value = i;

        public BFloat(int i) => Value = i;

        public BFloat(long i) => Value = i;

        public BFloat(float f) => Value = f;

        public BFloat(double f) => Value = f;

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

        public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);

        public string Inspect() => Value.ToString(CultureInfo.InvariantCulture);

        public object ToCSharp() => Value;

        public Type Type() => typeof(double);

        public IScalar ScalarContext() => this;

        public IList ListContext() => throw new NoListContextException(this);

        public IDictionary DictionaryContext() => throw new NoDictionaryContextException(this);
    }

    /// <summary>
    /// Represents a string of characters.
    /// </summary>
    public class BString : IScalar {
        public string Value { get; }

        public BString(string s) => Value = s;

        public static implicit operator BString(string s) => new BString(s);

        public static bool operator ==(BString left, object right) => right is BString s && left.Value == s.Value;

        public static bool operator !=(BString left, object right) => right is BString s && left.Value != s.Value;

        public override bool Equals(object other) => (other is BString s) && Value == s.Value;

        public override int GetHashCode() => Value.GetHashCode();

        public override string ToString() => Value;

        public string Inspect() => $"\"{System.Text.RegularExpressions.Regex.Escape(Value)}\"";

        public object ToCSharp() => Value;

        public Type Type() => typeof(string);

        public IScalar ScalarContext() => this;

        public IList ListContext() => throw new NoListContextException(this);

        public IDictionary DictionaryContext() => throw new NoDictionaryContextException(this);
    }

    /// <summary>
    /// Represents a bare atomic value.
    /// </summary>
    public class BAtom : IScalar {
        public static readonly BAtom True = new BAtom("t");
        public static readonly BAtom Nil = new BAtom("nil");

        public string Value { get; }

        public BAtom(string a) => Value = a;

        public static implicit operator BAtom(string s) => new BAtom(s);

        public static implicit operator string(BAtom a) => a.Value;

        public static bool operator ==(BAtom left, object right) => right is BAtom a && left.Value == a.Value;

        public static bool operator !=(BAtom left, object right) => right is BAtom a && left.Value != a.Value;

        public override bool Equals(object other) => (other is BAtom a) && Value == a.Value;

        public override int GetHashCode() => Value.GetHashCode();

        public override string ToString() => Value;

        public string Inspect() => Value.Length == 2 && char.IsDigit(Value[0]) && char.IsLetter(Value[1]) ? '|' + Value + '|' : Value;

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

        public IScalar ScalarContext() => this;

        public IList ListContext() => throw new NoListContextException(this);

        public IDictionary DictionaryContext() => throw new NoDictionaryContextException(this);
    }

    /// <summary>
    /// Represents a scalar variable.
    /// </summary>
    public class ScalarVar : IScalar {
        public string Value { get; }

        public ScalarVar(string s) => Value = s;

        public static implicit operator ScalarVar(string s) => new ScalarVar(s);

        public static implicit operator string(ScalarVar s) => s.Value;

        public override string ToString() => Value;

        public string Inspect() => '$' + Value;

        public object ToCSharp() => Value;

        public Type Type() => typeof(string);

        public IScalar ScalarContext() => this;

        public IList ListContext() => throw new NoListContextException(this);

        public IDictionary DictionaryContext() => throw new NoDictionaryContextException(this);
    }

    /// <summary>
    /// Represents a list variable.
    /// </summary>
    public class ListVar : IScalar {
        public string Value { get; }

        public ListVar(string l) => Value = l;

        public static implicit operator ListVar(string s) => new ListVar(s);

        public static implicit operator string(ListVar l) => l.Value;

        public override string ToString() => Value;

        public string Inspect() => '@' + Value;

        public object ToCSharp() => Value;

        public Type Type() => typeof(string);

        public IScalar ScalarContext() => this;

        public IList ListContext() => throw new NoListContextException(this);

        public IDictionary DictionaryContext() => throw new NoDictionaryContextException(this);
    }

    /// <summary>
    /// Represents a dictionary variable.
    /// </summary>
    public class DictVar : IScalar {
        public string Value { get; }

        public DictVar(string d) => Value = d;

        public static implicit operator DictVar(string s) => new DictVar(s);

        public static implicit operator string(DictVar d) => d.Value;

        public override string ToString() => Value;

        public string Inspect() => '%' + Value;

        public object ToCSharp() => Value;

        public Type Type() => typeof(string);

        public IScalar ScalarContext() => this;

        public IList ListContext() => throw new NoListContextException(this);

        public IDictionary DictionaryContext() => throw new NoDictionaryContextException(this);
    }

    /// <summary>
    /// Represents a type name.
    /// </summary>
    public class TypeName : IValue {
        public string Value { get; }

        public TypeName(string t) => Value = t;

        public static implicit operator TypeName(string t) => new TypeName(t);

        public static implicit operator string(TypeName t) => t.Value;

        public override string ToString() => Value;

        public string Inspect() => '!' + Value;

        public object ToCSharp() => Value;

        public Type Type() => typeof(string);

        public IScalar ScalarContext() => throw new NoScalarContextException(this);

        public IList ListContext() => throw new NoListContextException(this);

        public IDictionary DictionaryContext() => throw new NoDictionaryContextException(this);
    }

    /// <summary>
    /// Represents a list of values.
    /// </summary>
    public class BList : List<IValue>, IList {
        public BList Value { get; }

        public BList() => Value = this;

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

        public IScalar ScalarContext() => (BInteger) Count;

        public IList ListContext() => this;

        public IDictionary DictionaryContext() => new BDictionary(this.WithIndex().ToDictionary(item => (IValue) (BInteger) item.index, item => item.value));
    }

    public class BCSharpList : List<object>, IValue {
        public BCSharpList Value { get; }

        public BCSharpList() => Value = this;

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

        public IScalar ScalarContext() => (BInteger) Count;

        public IList ListContext() => new BList(this.Select(item => new CauliflowerInterpreter.BCSharpValue(item)));

        public IDictionary DictionaryContext() => new BDictionary(this.WithIndex().ToDictionary(item => (IValue) (BInteger) item.index, item => (IValue) new CauliflowerInterpreter.BCSharpValue(item.value)));
    }

    /// <summary>
    /// Represents a dictionary of values paired with other values.
    /// </summary>
    public class BDictionary : Dictionary<IValue, IValue>, IDictionary {
        public BDictionary Value { get; }

        public BDictionary() => Value = this;

        public BDictionary(IReadOnlyDictionary<IValue, IValue> values) : base(values) => Value = this;

        public static bool operator ==(BDictionary left, object right) => right is IReadOnlyDictionary<IValue, IValue> rdict
            && rdict.Count == left.Count && !left.Except(rdict).Any();
        public static bool operator !=(BDictionary left, object right) => !(left == right);

        public override bool Equals(object obj) => obj is IReadOnlyDictionary<IValue, IValue> rdict
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
                sb.Append($"{i.Key.Inspect()}: {i.Value.Inspect()}, ");
            if (sb.Length > 1)
                sb.Length -= 2;
            sb.Append(")");
            return sb.ToString();
        }

        public object ToCSharp() => new BCSharpDictionary(Value.ToDictionary(item => item.Key.ToCSharp(), item => item.Value.ToCSharp()));

        public Type Type() => typeof(Dictionary<IValue, IValue>);

        public IScalar ScalarContext() => (BInteger) Count;

        public IList ListContext() => new BList(this.Select(kvp => new BList(kvp.Key, kvp.Value)));

        public IDictionary DictionaryContext() => this;
    }

    public class BCSharpDictionary : Dictionary<object, object>, IValue {
        public BCSharpDictionary Value { get; }

        public BCSharpDictionary() => Value = this;

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

        public IScalar ScalarContext() => (BInteger) Count;

        public IList ListContext() => new BList(this.Select(kvp => new BList(new CauliflowerInterpreter.BCSharpValue(kvp.Key), new CauliflowerInterpreter.BCSharpValue(kvp.Value))));

        public IDictionary DictionaryContext() => new BDictionary(this.ToDictionary(kvp => (IValue) new CauliflowerInterpreter.BCSharpValue(kvp.Key), kvp => (IValue) new CauliflowerInterpreter.BCSharpValue(kvp.Value)));
    }
}
