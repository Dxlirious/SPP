using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using CustomTestLib;
using CustomThreadPool;

namespace LoadSimulator
{
    class Program
    {
        private static readonly object ConsoleLock = new object();
        private static int _total = 0;
        private static int _completed = 0;

        static void Main(string[] args)
        {
            string assemblyPath = Path.GetFullPath(Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "..\\..\\..\\..\\TestsProject\\bin\\Debug\\net9.0\\TestsProject.dll"));

            if (!File.Exists(assemblyPath))
            {
                Console.WriteLine($"DLL не найдена: {assemblyPath}");
                Console.WriteLine("Убедись что TestsProject собран (Build)");
                Console.ReadKey();
                return;
            }

            var assembly = Assembly.LoadFrom(assemblyPath);
            var allTests = GetAllTestMethods(assembly);
            Console.WriteLine($"Загружено тестовых методов: {allTests.Count}");

            // ФИЛЬТРАЦИЯ ЧЕРЕЗ ДЕЛЕГАТЫ
            DemoFilters(allTests);

            // СОБСТВЕННЫЙ ПУЛ ПОТОКОВ
            var pool = new CustomThreadPool.CustomThreadPool(
                minThreads: 2,
                maxThreads: 6,
                idleTimeoutMs: 3000,
                queueThreshold: 3,
                hungThreadTimeoutMs: 5000);

            // ПОДПИСКА НА СОБЫТИЯ
            pool.ThreadCreated += (s, e) => LogEvent("THREAD_CREATED", e);
            pool.ThreadDestroyed += (s, e) => LogEvent("THREAD_DESTROYED", e);
            pool.TaskEnqueued += (s, e) => LogEvent("TASK_ENQUEUED", e);
            pool.TaskStarted += (s, e) => LogEvent("TASK_STARTED", e);
            pool.TaskCompleted += (s, e) => LogEvent("TASK_COMPLETED", e);
            pool.ThreadHungDetected += (s, e) => LogEvent("HUNG_DETECTED", e);

            // НЕРАВНОМЕРНАЯ НАГРУЗКА 

            Console.WriteLine("\n=== ФАЗА 1: единичные подачи (5 задач) ===");
            for (int i = 0; i < 5; i++)
            {
                EnqueueNextTest(pool, allTests);
                Thread.Sleep(700);
            }

            Console.WriteLine("\n=== ФАЗА 2: пиковая нагрузка (30 задач) ===");
            for (int i = 0; i < 30; i++)
            {
                EnqueueNextTest(pool, allTests);
                Thread.Sleep(40);
            }

            Console.WriteLine("\n=== ФАЗА 3: бездействие 5 сек ===");
            Thread.Sleep(5000);

            Console.WriteLine("\n=== ФАЗА 4: повторная нагрузка (20 задач) ===");
            for (int i = 0; i < 20; i++)
            {
                EnqueueNextTest(pool, allTests);
                Thread.Sleep(80);
            }

            Console.WriteLine("\nОжидаем завершения всех задач...");
            while (Volatile.Read(ref _completed) < Volatile.Read(ref _total))
                Thread.Sleep(300);

            Console.WriteLine($"\n{"=",-70}");
            Console.WriteLine($"ИТОГО: {_completed}/{_total} тестов выполнено");
            Console.WriteLine($"{"=",-70}");

            pool.Shutdown();
            Console.WriteLine("Нажми любую клавишу...");
            Console.ReadKey();
        }

        // ФИЛЬТРАЦИЯ ТЕСТОВ ЧЕРЕЗ ДЕЛЕГАТЫ
        static List<(Type classType, MethodInfo method)> FilterTests(
            List<(Type classType, MethodInfo method)> allTests,
            Func<MethodInfo, bool> filter)
        {
            return allTests.Where(t => filter(t.method)).ToList();
        }

        static void DemoFilters(List<(Type classType, MethodInfo method)> allTests)
        {
            Console.WriteLine("\n=== ДЕМОНСТРАЦИЯ ФИЛЬТРАЦИИ ТЕСТОВ (ЛР4) ===");

            var unitTests = FilterTests(allTests,
                m => m.GetCustomAttributes<TestCategoryAttribute>().Any(c => c.Name == "Unit"));
            Console.WriteLine($"  Category=Unit       : {unitTests.Count} тестов");

            var highPriority = FilterTests(allTests,
                m => (m.GetCustomAttribute<PriorityAttribute>()?.Value ?? 99) <= 1);
            Console.WriteLine($"  Priority <= 1       : {highPriority.Count} тестов");

            var romanTests = FilterTests(allTests,
                m => m.GetCustomAttribute<AuthorAttribute>()?.Name == "Roman");
            Console.WriteLine($"  Author=Roman        : {romanTests.Count} тестов");

            var asyncTests = FilterTests(allTests,
                m => m.GetCustomAttributes<TestCategoryAttribute>().Any(c => c.Name == "Async"));
            Console.WriteLine($"  Category=Async      : {asyncTests.Count} тестов");

            var exprTests = FilterTests(allTests,
                m => m.GetCustomAttributes<TestCategoryAttribute>().Any(c => c.Name == "Expression"));
            Console.WriteLine($"  Category=Expression : {exprTests.Count} тестов");

            Console.WriteLine();
        }

        static void EnqueueNextTest(
            CustomThreadPool.CustomThreadPool pool,
            List<(Type classType, MethodInfo method)> allTests)
        {
            int idx = Volatile.Read(ref _total) % allTests.Count;
            var item = allTests[idx];

            var sourceAttr = item.method.GetCustomAttribute<TestCaseSourceAttribute>();
            if (sourceAttr != null)
            {
                var sourceMethod = item.classType.GetMethod(
                    sourceAttr.SourceMethod,
                    BindingFlags.Static | BindingFlags.Public);

                if (sourceMethod != null)
                {
                    var cases = (IEnumerable<object[]>)sourceMethod.Invoke(null, null);
                    foreach (var caseArgs in cases)
                    {
                        var capturedArgs = caseArgs;
                        var capturedItem = item;
                        Interlocked.Increment(ref _total);
                        pool.Enqueue(() =>
                        {
                            RunParametrizedTest(capturedItem.classType, capturedItem.method, capturedArgs);
                            Interlocked.Increment(ref _completed);
                        });
                    }
                    return;
                }
            }

            var captured = item;
            Interlocked.Increment(ref _total);
            pool.Enqueue(() =>
            {
                RunSingleTest(captured.classType, captured.method);
                Interlocked.Increment(ref _completed);
            });
        }

        static void RunSingleTest(Type classType, MethodInfo method)
        {
            try
            {
                object instance = Activator.CreateInstance(classType);
                InvokeBefore(classType, instance);

                var ret = method.Invoke(instance, null);
                if (ret is System.Threading.Tasks.Task t)
                    t.GetAwaiter().GetResult();

                lock (ConsoleLock)
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"  ✅ {classType.Name}.{method.Name} [T:{Thread.CurrentThread.ManagedThreadId}]");
                    Console.ResetColor();
                }
            }
            catch (TargetInvocationException tie) when (tie.InnerException is TestException te)
            {
                lock (ConsoleLock)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"  ❌ {classType.Name}.{method.Name}: {te.Message}");
                    Console.ResetColor();
                }
            }
            catch (Exception ex)
            {
                lock (ConsoleLock)
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine($"  ⚠ {classType.Name}.{method.Name}: {ex.InnerException?.Message ?? ex.Message}");
                    Console.ResetColor();
                }
            }
        }

        static void RunParametrizedTest(Type classType, MethodInfo method, object[] args)
        {
            try
            {
                object instance = Activator.CreateInstance(classType);
                InvokeBefore(classType, instance);
                method.Invoke(instance, args);

                lock (ConsoleLock)
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"  ✅ {classType.Name}.{method.Name}({string.Join(", ", args)}) [T:{Thread.CurrentThread.ManagedThreadId}]");
                    Console.ResetColor();
                }
            }
            catch (TargetInvocationException tie) when (tie.InnerException is TestException te)
            {
                lock (ConsoleLock)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"  ❌ {classType.Name}.{method.Name}({string.Join(", ", args)}): {te.Message}");
                    Console.ResetColor();
                }
            }
            catch (Exception ex)
            {
                lock (ConsoleLock)
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine($"  ⚠ {classType.Name}.{method.Name}({string.Join(", ", args)}): {ex.InnerException?.Message ?? ex.Message}");
                    Console.ResetColor();
                }
            }
        }

        static void InvokeBefore(Type classType, object instance)
        {
            var before = classType.GetMethods()
                .Where(m => m.GetCustomAttribute<BeforeClassAttribute>() != null);
            foreach (var b in before)
                b.Invoke(instance, null);
        }

        static void LogEvent(string type, ThreadPoolEventArgs e)
        {
            lock (ConsoleLock)
            {
                Console.ForegroundColor = ConsoleColor.Magenta;
                Console.WriteLine(
                    $"  [EVENT:{type,-16}] {e.Message,-40} | " +
                    $"Потоков: {e.ActiveThreads} | Очередь: {e.QueueLength} | {e.Timestamp:HH:mm:ss.fff}");
                Console.ResetColor();
            }
        }

        static List<(Type classType, MethodInfo method)> GetAllTestMethods(Assembly assembly)
        {
            var result = new List<(Type, MethodInfo)>();
            var testClasses = assembly.GetTypes()
                .Where(t => t.GetMethods().Any(m => m.GetCustomAttribute<TestAttribute>() != null));

            foreach (var cls in testClasses)
            {
                var methods = cls.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                    .Where(m => m.GetCustomAttribute<TestAttribute>() != null);
                foreach (var m in methods)
                    result.Add((cls, m));
            }
            return result;
        }
    }
}