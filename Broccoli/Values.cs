using System.Collections.Generic;

namespace Broccoli {
    public interface IValue : IValueExpressible { }

    public struct Integer : IValue {
        public long Value { get; }

        public Integer(long i) {
            Value = i;
        }

        public static implicit operator Integer(int i) => new Integer(i);

        public static implicit operator Integer(long i) => new Integer(i);

        public static bool operator ==(Integer left, object right) => right is Integer i && left.Value == i.Value;

        public static bool operator !=(Integer left, object right) => right is Integer i && left.Value != i.Value;

        public override bool Equals(object other) => other is Integer i && Value == i.Value;

        public override int GetHashCode() => Value.GetHashCode();

        public override string ToString() => Value.ToString();
    }

    public struct Float : IValue {
        public double Value { get; }

        public Float(double f) {
            Value = f;
        }

        public static implicit operator Float(float f) => new Float(f);

        public static implicit operator Float(double f) => new Float(f);

        public static implicit operator Float(Integer f) => new Float(f.Value);

        public static bool operator ==(Float left, object right) => right is Float f && left.Value == f.Value;

        public static bool operator !=(Float left, object right) => right is Float f && left.Value != f.Value;

        public override bool Equals(object other) => other is Float f && Value == f.Value;

        public override int GetHashCode() => Value.GetHashCode();

        public override string ToString() => Value.ToString();
    }

    public struct String : IValue {
        public string Value { get; }

        public String(string s) {
            Value = s;
        }

        public static implicit operator String(string s) => new String(s);

        public static bool operator ==(String left, object right) => right is String s && left.Value == s.Value;

        public static bool operator !=(String left, object right) => right is String s && left.Value != s.Value;

        public override bool Equals(object other) => (other is String s) && Value == s.Value;

        public override int GetHashCode() => Value.GetHashCode();

        public override string ToString() => Value;
    }

    public struct Atom : IValue {
        public static readonly Atom True = new Atom("t");
        public static readonly Atom Nil = new Atom("nil");

        public string Value { get; }

        public Atom(string a) {
            Value = a;
        }

        public static implicit operator Atom(string a) => new Atom(a);

        public static bool operator ==(Atom left, object right) => right is Atom a && left.Value == a.Value;

        public static bool operator !=(Atom left, object right) => right is Atom a && left.Value != a.Value;

        public override bool Equals(object other) => (other is Atom a) && Value == a.Value;

        public override int GetHashCode() => Value.GetHashCode();

        public override string ToString() => Value;
    }

    public struct ScalarVar : IValue {
        public string Value { get; }

        public ScalarVar(string s) {
            Value = s;
        }

        public override string ToString() => Value;
    }

    public struct ListVar : IValue {
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
            for (int i = 0; i < Count; i++)
                if (!this[i].Equals(otherList[i]))
                    return false;
            return true;
        }

        public override int GetHashCode() => Value.GetHashCode();

        public override string ToString() => '(' + string.Join(' ', Value) + ')';
    }
}