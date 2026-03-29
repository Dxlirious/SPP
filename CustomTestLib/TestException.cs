using System;

namespace CustomTestLib
{
    public class TestException : Exception
    {
        public TestException(string message) : base(message) { }
    }
}