using System.Collections.Generic;

namespace Broccoli {
    public interface IValue : IValueExpressible { }

    public class BInteger : IValue {
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
    }

    public class BFloat : IValue {
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
    }

    public class BString : IValue {
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
    }

    public class BAtom : IValue {
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
    }

    public class ScalarVar : IValue {
        public string Value { get; }

        public ScalarVar(string s) {
            Value = s;
        }

        public override string ToString() => Value;
    }

    public class ListVar : IValue {
        public string Value { get; }

        public ListVar(string l) {
            Value = l;
        }

        public override string ToString() => Value;
    }

    public class ValueList : List<IValue>, IValue {
        public ValueList Value { get; }

        public ValueList() : base() {
            Value = this;
        }

        public ValueList(IEnumerable<IValue> values) : base(values) {
            Value = this;
        }

        public ValueList(params IValue[] values) : base(values) {
            Value = this;
        }

        public static implicit operator ValueList(IValue[] values) => new ValueList(values);

        public static bool operator ==(ValueList left, object right) => right is ValueList list && left.Equals(list);

        public static bool operator !=(ValueList left, object right) => right is ValueList list && !left.Equals(list);

        public override bool Equals(object other) {
            if (!(other is ValueList otherList) || otherList.Count != Count)
                return false;
            for (var i = 0; i < Count; i++)
                if (!this[i].Equals(otherList[i]))
                    return false;
            return true;
        }

        public override int GetHashCode() => Value.GetHashCode();

        public override string ToString() => '(' + string.Join(' ', Value) + ')';
    }
}