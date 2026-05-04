using CustomTestLib;
using TestedApp;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace TestsProject
{
    [Test("Тесты процессора строк")]
    public class StringProcessorTests
    {
        private StringProcessor processor;

        [BeforeClassAttribute]
        public void Setup()
        {
            processor = new StringProcessor();
        }

        [AfterClassAttribute]
        public void Teardown()
        {
            processor = null;
        }

        // ТЕСТЫ ЛР1/ЛР2

        [Test("Подсчёт слов")]
        [TestCategory("Unit")]
        [Priority(1)]
        [Author("Roman")]
        public void TestCountWords_Normal()
        {
            int count = processor.CountWords("Hello world test");
            Assert.AreEqual(3, count);
            Assert.InRange(count, 1, 10);
        }

        [Test("ToUpperCase")]
        [TestCategory("Unit")]
        [Priority(1)]
        [Author("Roman")]
        public void TestToUpper_Normal()
        {
            string result = processor.ToUpperCase("hello");
            Assert.AreEqual("HELLO", result);
            Assert.StringLength(result, 5);
            Assert.Contains("HEL", result);
        }

        [Test("Удаление дубликатов")]
        [TestCategory("Unit")]
        [Priority(2)]
        [Author("Roman")]
        public void TestRemoveDuplicates()
        {
            string result = processor.RemoveDuplicates("aabbcc");
            Assert.AreEqual("abc", result);
        }

        [Test("Палиндром true")]
        [TestCategory("Unit")]
        [TestCategory("String")]
        [Priority(1)]
        [Author("Roman")]
        public void TestIsPalindrome_True()
        {
            Assert.IsTrue(processor.IsPalindrome("aba"));
            Assert.IsTrue(processor.IsPalindrome("A man a plan a canal Panama"));
        }

        [Test("Палиндром false")]
        [TestCategory("Unit")]
        [TestCategory("String")]
        [Priority(1)]
        [Author("Roman")]
        public void TestIsPalindrome_False()
        {
            Assert.IsFalse(processor.IsPalindrome("abc"));
        }

        [Test("Async обработка 1")]
        [TestCategory("Async")]
        [Priority(2)]
        [Author("Roman")]
        public async Task TestProcessWithDelayAsync_1()
        {
            string result = await processor.ProcessWithDelayAsync("TeSt1", 1000);
            Assert.AreEqual("test1", result);
        }

        [Test("Async обработка 2")]
        [TestCategory("Async")]
        [Priority(2)]
        [Author("Roman")]
        public async Task TestProcessWithDelayAsync_2()
        {
            string result = await processor.ProcessWithDelayAsync("TeSt2", 1000);
            Assert.AreEqual("test2", result);
        }

        [Test("Async обработка 3")]
        [TestCategory("Async")]
        [Priority(2)]
        [Author("Roman")]
        public async Task TestProcessWithDelayAsync_3()
        {
            string result = await processor.ProcessWithDelayAsync("TeSt3", 1000);
            Assert.AreEqual("test3", result);
        }

        [Test("Исключение CountWords пустая")]
        [TestCategory("Exception")]
        [Priority(1)]
        [Author("Roman")]
        public void TestCountWords_Empty_Throws()
        {
            Assert.Throws<InvalidStringException>(() => processor.CountWords(""));
        }

        [Test("Исключение ToUpper null")]
        [TestCategory("Exception")]
        [Priority(1)]
        [Author("Roman")]
        public void TestToUpper_Null_Throws()
        {
            Assert.Throws<InvalidStringException>(() => processor.ToUpperCase(null));
        }

        [Test("Async отрицательная")]
        [TestCategory("Exception")]
        [TestCategory("Async")]
        [Priority(2)]
        [Author("Roman")]
        public async Task TestProcessWithDelay_Negative_Throws()
        {
            await Assert.ThrowsAsync<InvalidStringException>(
                () => processor.ProcessWithDelayAsync("test", -1));
        }

        [Test("Тест с таймаутом - должен пройти")]
        [TestCategory("Timeout")]
        [Priority(3)]
        [Author("Roman")]
        [Timeout(1500)]
        public async Task TestTimeout_Success()
        {
            int result = await processor.SimulateLongOperationAsync(700);
            Assert.AreEqual(700, result);
        }

        [Test("Тест с таймаутом - должен упасть")]
        [TestCategory("Timeout")]
        [Priority(3)]
        [Author("Roman")]
        [Timeout(500)]
        public async Task TestTimeout_Fail()
        {
            int result = await processor.SimulateLongOperationAsync(2000);
            Assert.AreEqual(2000, result);
        }

        //  ПАРАМЕТРИЗОВАННЫЕ ТЕСТЫ ЛР4

        // Источник данных - итератор yield return
        public static IEnumerable<object[]> CountWordsData()
        {
            yield return new object[] { "Hello world", 2 };
            yield return new object[] { "One two three four", 4 };
            yield return new object[] { "Single", 1 };
            yield return new object[] { "a b c d e", 5 };
            yield return new object[] { "foo bar baz", 3 };
        }

        [Test("Параметризованный CountWords")]
        [TestCategory("Parametrized")]
        [Priority(1)]
        [Author("Roman")]
        [TestCaseSource(nameof(CountWordsData))]
        public void TestCountWords_Parametrized(string input, int expected)
        {
            int result = processor.CountWords(input.Trim());
            Assert.AreEqual(expected, result);
        }

        public static IEnumerable<object[]> PalindromeData()
        {
            yield return new object[] { "aba", true };
            yield return new object[] { "abc", false };
            yield return new object[] { "level", true };
            yield return new object[] { "hello", false };
            yield return new object[] { "madam", true };
        }

        [Test("Параметризованный IsPalindrome")]
        [TestCategory("Parametrized")]
        [TestCategory("String")]
        [Priority(1)]
        [Author("Roman")]
        [TestCaseSource(nameof(PalindromeData))]
        public void TestIsPalindrome_Parametrized(string input, bool expected)
        {
            bool result = processor.IsPalindrome(input);
            Assert.AreEqual(expected, result);
        }

        // ===== Assert.That с деревом выражений 

        [Test("Assert.That успешный")]
        [TestCategory("Expression")]
        [Priority(3)]
        [Author("Roman")]
        public void TestAssertThat_Success()
        {
            int a = 5;
            int b = 10;
            Assert.That(() => a < b);
            Assert.That(() => a + b == 15);
        }

        [Test("Assert.That провал с деталями дерева")]
        [TestCategory("Expression")]
        [Priority(3)]
        [Author("Roman")]
        public void TestAssertThat_FailWithDetails()
        {
            int x = 7;
            int y = 3;
            // Намеренно провальный тест для демонстрации разбора дерева выражений
            Assert.That(() => x == y);
        }
    }
}