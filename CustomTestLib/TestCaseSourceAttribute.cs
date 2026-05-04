using System;

namespace CustomTestLib
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public class TestCaseSourceAttribute : Attribute
    {
        public string SourceMethod { get; }
        public TestCaseSourceAttribute(string sourceMethod) { SourceMethod = sourceMethod; }
    }
}