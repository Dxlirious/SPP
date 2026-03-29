using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using CustomTestLib;

namespace TestRunner
{
    class Program
    {
        static async Task Main(string[] args)
        {
            string assemblyPath = args.Length > 0 ? args[0] :
                @"..\..\..\bin\Debug\net9.0\TestsProject.dll";

            Console.WriteLine($"Загрузка тестов: {assemblyPath}");

            var assembly = Assembly.LoadFrom(assemblyPath);
            var testClasses = assembly.GetTypes()
                .Where(t => Attribute.IsDefined(t, typeof(TestAttribute)) ||
                           t.GetMethods().Any(m => Attribute.IsDefined(m, typeof(TestAttribute))))
                .ToArray();

            List<TestResult> results = new();

            foreach (var testClass in testClasses)
            {
                var instance = Activator.CreateInstance(testClass);

                var beforeMethods = testClass.GetMethods()
                    .Where(m => Attribute.IsDefined(m, typeof(BeforeClassAttribute)));
                foreach (var before in beforeMethods) before.Invoke(instance, null);

                var testMethods = testClass.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Static)
                    .Where(m => Attribute.IsDefined(m, typeof(TestAttribute)));

                foreach (var method in testMethods)
                {
                    var result = new TestResult
                    {
                        TestName = $"{testClass.Name}.{method.Name}",
                        Passed = true
                    };
                    var attr = (TestAttribute)Attribute.GetCustomAttribute(method, typeof(TestAttribute));
                    Console.WriteLine($"🧪 {result.TestName} {attr?.Description}");

                    try
                    {
                        object[] parameters = null;
                        if (method.GetParameters().Length > 0)
                            parameters = new object[method.GetParameters().Length]; // для будущих параметров

                        object returnValue = method.Invoke(instance, parameters);

                        if (method.ReturnType == typeof(Task) ||
                            (method.ReturnType.IsGenericType && method.ReturnType.GetGenericTypeDefinition() == typeof(Task<>)))
                        {
                            if (returnValue is Task task)
                                await task;
                        }
                    }
                    catch (TargetInvocationException tie) when (tie.InnerException is TestException te)
                    {
                        result.Passed = false;
                        result.Message = $"Assert failed: {te.Message}";
                    }
                    catch (TargetInvocationException tie)
                    {
                        result.Passed = false;
                        result.Message = $"Exception: {tie.InnerException?.Message}";
                    }
                    catch (Exception ex)
                    {
                        result.Passed = false;
                        result.Message = $"Error: {ex.Message}";
                    }
                    results.Add(result);
                }

                var afterMethods = testClass.GetMethods()
                    .Where(m => Attribute.IsDefined(m, typeof(AfterClassAttribute)));
                foreach (var after in afterMethods) after.Invoke(instance, null);
            }

            var passed = results.Count(r => r.Passed);
            var failed = results.Count(r => !r.Passed);
            Console.WriteLine($"\n{'=' * 50}");
            Console.WriteLine($"✅ PASSED: {passed} | ❌ FAILED: {failed} | TOTAL: {results.Count}");

            foreach (var r in results.Where(r => !r.Passed))
                Console.WriteLine($"❌ {r.TestName}\n   {r.Message}\n");
        }
    }
}