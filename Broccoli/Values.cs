using System.Collections.Generic;
using System.Linq;
using System;

// TODO: should ==, != etc have expression body?

namespace Broccoli {
    public interface IValue : IValueExpressible { }

    public struct Integer : IValue {
        public long Value { get; }

        public Integer(long i) {
            Value = i;
        }

        public static implicit operator Integer(long i) {
            return new Integer(i);
        }

        public static bool operator ==(Integer left, object right) {
            return right is Integer integer && left.Value == integer.Value;
        }

        public static bool operator !=(Integer left, object right) {
            return right is Integer integer && left.Value != integer.Value;
        }

        public override bool Equals(object other) {
            return other is Integer integer && Value == integer.Value;
        }

        public override int GetHashCode() {
            return Value.GetHashCode();
        }

        public override string ToString() => Value.ToString();
    }

    public struct Float : IValue {
        public double Value { get; }

        public Float(double f) {
            Value = f;
        }

        public static implicit operator Float(double f) {
            return new Float(f);
        }

        public static implicit operator Float(Integer f) {
            return new Float(f.Value);
        }

        public static bool operator ==(Float left, object right) {
            return right is Float f && left.Value == f.Value;
        }

        public static bool operator !=(Float left, object right) {
            return right is Float f && left.Value != f.Value;
        }

        public override bool Equals(object other) {
            return other is Float f && Value == f.Value;
        }

        public override int GetHashCode() {
            return Value.GetHashCode();
        }

        public override string ToString() => Value.ToString();
    }

    public struct String : IValue {
        public string Value { get; }

        public String(string s) {
            Value = s;
        }

        public static implicit operator String(string s) {
            return new String(s);
        }

        public static bool operator ==(String left, object right) {
            return right is String s && left.Value == s.Value;
        }

        public static bool operator !=(String left, object right) {
            return right is String s && left.Value != s.Value;
        }

        public override bool Equals(object other) {
            return (other is String s) && Value == s.Value;
        }

        public override int GetHashCode() {
            return Value.GetHashCode();
        }

        public override string ToString() => Value;
    }

    public struct Atom : IValue {
        public static readonly Atom True = new Atom("t");
        public static readonly Atom Nil = new Atom("nil");

        public string Value { get; }

        public Atom(string a) {
            Value = a;
        }

        public static implicit operator Atom(string a) {
            return new Atom(a);
        }

        public static bool operator ==(Atom left, object right) {
            return right is Atom atom && left.Value == atom.Value;
        }

        public static bool operator !=(Atom left, object right) {
            return right is Atom atom && left.Value != atom.Value;
        }

        public override bool Equals(object other) {
            return (other is Atom atom) && Value == atom.Value;
        }

        public override int GetHashCode() {
            return Value.GetHashCode();
        }

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

        public static implicit operator ValueList(IValue[] values) {
            return new ValueList(values);
        }

        public static bool operator ==(ValueList left, object right) {
            return right is ValueList list && left.Equals(list);
        }

        public static bool operator !=(ValueList left, object right) {
            return right is ValueList list && !left.Equals(list);
        }

        public override bool Equals(object other) {
            if (!(other is ValueList) || ((ValueList) other).Count != Count)
                return false;
            var otherList = (ValueList) other;
            for (int i = 0; i < Count; i++)
                if (!this[i].Equals(otherList[i]))
                    return false;
            return true;
        }

        public override int GetHashCode() {
            return Value.GetHashCode();
        }

        public override string ToString() => '(' + string.Join(' ', Value) + ')';
    }
}