﻿namespace Broccoli
{
    public interface IValue { }

    public interface IValue<out T> : IValue
    {
        T Value { get; }
    }

    public struct Int : IValue<int>
    {
        public int Value { get; }

        public Int(int i)
        {
            Value = i;
        }
    }

    public struct Float : IValue<double>
    {
        public double Value { get; }

        public Float(double f)
        {
            Value = f;
        }
    }

    public struct String : IValue<string>
    {
        public string Value { get; }

        public String(string s)
        {
            Value = s;
        }
    }

    public struct Atom : IValue<string>
    {
        public string Value { get; }

        public Atom(string a)
        {
            Value = a;
        }
    }
}