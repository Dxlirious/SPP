using System;

namespace CustomTestLib
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public class AfterClassAttribute : Attribute { }
}