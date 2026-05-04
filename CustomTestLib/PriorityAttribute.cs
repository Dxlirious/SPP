using System;

namespace CustomTestLib
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public class PriorityAttribute : Attribute
    {
        public int Value { get; }
        public PriorityAttribute(int value) { Value = value; }
    }
}