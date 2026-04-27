using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using CustomTestLib;

namespace TestRunner
{
    class Program
    {
        private static readonly object ConsoleLock = new object();

        static async Task Main(string[] args)
        {
            string assemblyPath = args.Length > 0
                ? args[0]
                : Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..\\..\\..\\..\\TestsProject\\bin\\Debug\\net9.0\\TestsProject.dll"));

            if (!File.Exists(assemblyPath))
            {
                Console.WriteLine($"DLL с тестами не найдена: {assemblyPath}");
                return;
            }

            Console.WriteLine($"Загрузка тестов: {assemblyPath}");

            var assembly = Assembly.LoadFrom(assemblyPath);

            var options = new TestRunnerOptions
            {
                MaxDegreeOfParallelism = 3,
                RunInParallel = true,
                WriteToConsole = true
            };

            var sequentialWatch = Stopwatch.StartNew();
            var sequentialResults = await RunTestsSequentialAsync(assembly);
            sequentialWatch.Stop();

            PrintSummary("Последовательный запуск", sequentialResults, sequentialWatch.ElapsedMilliseconds);

            var parallelWatch = Stopwatch.StartNew();
            var parallelResults = await RunTestsParallelAsync(assembly, options);
            parallelWatch.Stop();

            PrintSummary("Параллельный запуск", parallelResults, parallelWatch.ElapsedMilliseconds);

            lock (ConsoleLock)
            {
                Console.WriteLine();
                Console.WriteLine("Сравнение эффективности:");
                Console.WriteLine($"Sequential: {sequentialWatch.ElapsedMilliseconds} ms");
                Console.WriteLine($"Parallel:   {parallelWatch.ElapsedMilliseconds} ms");
                Console.WriteLine(parallelWatch.ElapsedMilliseconds < sequentialWatch.ElapsedMilliseconds
                    ? "Параллельный запуск быстрее."
                    : "Параллельный запуск не дал выигрыша.");
            }
        }

        static async Task<List<TestResult>> RunTestsSequentialAsync(Assembly assembly)
        {
            var results = new List<TestResult>();
            var testMethods = GetAllTestMethods(assembly);

            foreach (var testInfo in testMethods)
            {
                var result = await RunSingleTestAsync(testInfo.classType, testInfo.method);
                results.Add(result);
            }

            return results;
        }

        static async Task<List<TestResult>> RunTestsParallelAsync(Assembly assembly, TestRunnerOptions options)
        {
            var results = new ConcurrentBag<TestResult>();
            var semaphore = new SemaphoreSlim(options.MaxDegreeOfParallelism);
            var testMethods = GetAllTestMethods(assembly);

            var tasks = testMethods.Select(async testInfo =>
            {
                await semaphore.WaitAsync();
                try
                {
                    var result = await RunSingleTestAsync(testInfo.classType, testInfo.method);
                    results.Add(result);
                }
                finally
                {
                    semaphore.Release();
                }
            });

            await Task.WhenAll(tasks);
            return results.OrderBy(r => r.TestName).ToList();
        }

        static List<(Type classType, MethodInfo method)> GetAllTestMethods(Assembly assembly)
        {
            var result = new List<(Type, MethodInfo)>();

            var testClasses = assembly.GetTypes()
                .Where(t => t.GetMethods().Any(m => m.GetCustomAttribute<TestAttribute>() != null))
                .ToList();

            foreach (var testClass in testClasses)
            {
                var methods = testClass.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic)
                    .Where(m => m.GetCustomAttribute<TestAttribute>() != null);

                foreach (var method in methods)
                {
                    result.Add((testClass, method));
                }
            }

            return result;
        }

        static async Task<TestResult> RunSingleTestAsync(Type testClass, MethodInfo method)
        {
            var result = new TestResult
            {
                TestName = $"{testClass.Name}.{method.Name}",
                Passed = true,
                ManagedThreadId = Environment.CurrentManagedThreadId
            };

            var sw = Stopwatch.StartNew();
            object instance = null;

            try
            {
                instance = Activator.CreateInstance(testClass);

                var beforeMethods = testClass.GetMethods()
                    .Where(m => m.GetCustomAttribute<BeforeClassAttribute>() != null);

                foreach (var before in beforeMethods)
                    before.Invoke(instance, null);

                var timeoutAttr = method.GetCustomAttribute<TimeoutAttribute>();
                int timeoutMs = timeoutAttr?.Milliseconds ?? Timeout.Infinite;

                Task testTask = InvokeTestMethodAsync(instance, method);

                if (timeoutMs != Timeout.Infinite)
                {
                    var completedTask = await Task.WhenAny(testTask, Task.Delay(timeoutMs));
                    if (completedTask != testTask)
                    {
                        result.Passed = false;
                        result.TimedOut = true;
                        result.Message = $"Превышено время выполнения: {timeoutMs} мс";
                    }
                    else
                    {
                        await testTask;
                    }
                }
                else
                {
                    await testTask;
                }

                var afterMethods = testClass.GetMethods()
                    .Where(m => m.GetCustomAttribute<AfterClassAttribute>() != null);

                foreach (var after in afterMethods)
                    after.Invoke(instance, null);
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
                result.Error = tie.InnerException;
            }
            catch (Exception ex)
            {
                result.Passed = false;
                result.Message = $"Error: {ex.Message}";
                result.Error = ex;
            }
            finally
            {
                sw.Stop();
                result.DurationMs = sw.ElapsedMilliseconds;

                lock (ConsoleLock)
                {
                    Console.WriteLine(
                        $"{(result.Passed ? "✅" : "❌")} {result.TestName} | " +
                        $"Thread={result.ManagedThreadId} | " +
                        $"Time={result.DurationMs} ms | " +
                        $"{(result.TimedOut ? "TIMEOUT" : result.Message ?? "OK")}");
                }
            }

            return result;
        }

        static Task InvokeTestMethodAsync(object instance, MethodInfo method)
        {
            var returnValue = method.Invoke(instance, null);

            if (returnValue is Task task)
                return task;

            return Task.CompletedTask;
        }

        static void PrintSummary(string title, List<TestResult> results, long totalMs)
        {
            int passed = results.Count(r => r.Passed);
            int failed = results.Count(r => !r.Passed);
            int timedOut = results.Count(r => r.TimedOut);

            lock (ConsoleLock)
            {
                Console.WriteLine();
                Console.WriteLine(new string('=', 70));
                Console.WriteLine(title);
                Console.WriteLine($"PASSED: {passed}, FAILED: {failed}, TIMEOUT: {timedOut}, TOTAL: {results.Count}");
                Console.WriteLine($"Общее время: {totalMs} ms");

                foreach (var fail in results.Where(r => !r.Passed))
                {
                    Console.WriteLine($" - {fail.TestName}: {fail.Message}");
                }
                Console.WriteLine(new string('=', 70));
            }
        }
    }
}