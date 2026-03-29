using System;

namespace CustomTestLib
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
    public class TestAttribute : Attribute
    {
        public string Description { get; }
        public TestAttribute(string description = "") => Description = description;
    }
}