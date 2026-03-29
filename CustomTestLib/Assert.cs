using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CustomTestLib
{
    public static class Assert
    {
        public static void AreEqual<T>(T expected, T actual, string message = "")
        {
            if (!EqualityComparer<T>.Default.Equals(expected, actual))
                throw new TestException($"Ожидалось: {expected}, получено: {actual}. {message}");
        }

        public static void AreNotEqual<T>(T expected, T actual, string message = "") =>
            AreEqual(default, !EqualityComparer<T>.Default.Equals(expected, actual) ? actual : default, message);

        public static void IsTrue(bool condition, string message = "") => AreEqual(true, condition, message);
        public static void IsFalse(bool condition, string message = "") => IsTrue(!condition, message);
        public static void IsNull(object obj, string message = "") => AreEqual(null, obj, message);
        public static void IsNotNull(object obj, string message = "") => IsNull(obj, "Ожидался не-null");

        public static void InRange(int value, int min, int max, string message = "")
        {
            if (value < min || value > max) throw new TestException($"Значение {value} вне [{min}-{max}]. {message}");
        }

        public static void Contains(string expectedSubstr, string input, string message = "")
        {
            if (!input.Contains(expectedSubstr)) throw new TestException($"'{expectedSubstr}' не в '{input}'. {message}");
        }

        public static void StringLength(string input, int expectedLen, string message = "")
        {
            AreEqual(expectedLen, input?.Length ?? 0, message);
        }

        public static void Throws<T>(Action action, string message = "") where T : Exception
        {
            try { action(); }
            catch (T) { return; }
            catch (Exception ex) { throw new TestException($"Ожидалось {typeof(T).Name}, получено {ex.GetType().Name}"); }
            throw new TestException($"{typeof(T).Name} не выброшен. {message}");
        }

        public static async Task ThrowsAsync<T>(Func<Task> action, string message = "") where T : Exception
        {
            try { await action(); }
            catch (T) { return; }
            catch (Exception ex) { throw new TestException($"Ожидалось {typeof(T).Name}, получено {ex.GetType().Name}"); }
            throw new TestException($"{typeof(T).Name} не выброшен. {message}");
        }
    }
}