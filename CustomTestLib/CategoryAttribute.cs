using System;

namespace CustomTestLib
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    public class TestCategoryAttribute : Attribute
    {
        public string Name { get; }
        public TestCategoryAttribute(string name) { Name = name; }
    }
}