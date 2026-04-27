using CustomTestLib;
using TestedApp;
using System.Threading.Tasks;

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

        [Test("Подсчёт слов")]
        public void TestCountWords_Normal()
        {
            int count = processor.CountWords("Hello world test");
            Assert.AreEqual(3, count);
            Assert.InRange(count, 1, 10);
        }

        [Test("ToUpperCase")]
        public void TestToUpper_Normal()
        {
            string result = processor.ToUpperCase("hello");
            Assert.AreEqual("HELLO", result);
            Assert.StringLength(result, 5);
            Assert.Contains("HEL", result);
        }

        [Test("Удаление дубликатов")]
        public void TestRemoveDuplicates()
        {
            string result = processor.RemoveDuplicates("aabbcc");
            Assert.AreEqual("abc", result);
        }

        [Test("Палиндром true")]
        public void TestIsPalindrome_True()
        {
            Assert.IsTrue(processor.IsPalindrome("aba"));
            Assert.IsTrue(processor.IsPalindrome("A man a plan a canal Panama"));
        }

        [Test("Палиндром false")]
        public void TestIsPalindrome_False()
        {
            Assert.IsFalse(processor.IsPalindrome("abc"));
        }

        [Test("Async обработка 1")]
        public async Task TestProcessWithDelayAsync_1()
        {
            string result = await processor.ProcessWithDelayAsync("TeSt1", 1000);
            Assert.AreEqual("test1", result);
        }

        [Test("Async обработка 2")]
        public async Task TestProcessWithDelayAsync_2()
        {
            string result = await processor.ProcessWithDelayAsync("TeSt2", 1000);
            Assert.AreEqual("test2", result);
        }

        [Test("Async обработка 3")]
        public async Task TestProcessWithDelayAsync_3()
        {
            string result = await processor.ProcessWithDelayAsync("TeSt3", 1000);
            Assert.AreEqual("test3", result);
        }

        [Test("Исключение CountWords пустая")]
        public void TestCountWords_Empty_Throws()
        {
            Assert.Throws<InvalidStringException>(() => processor.CountWords(""));
        }

        [Test("Исключение ToUpper null")]
        public void TestToUpper_Null_Throws()
        {
            Assert.Throws<InvalidStringException>(() => processor.ToUpperCase(null));
        }

        [Test("Async отрицательная")]
        public async Task TestProcessWithDelay_Negative_Throws()
        {
            await Assert.ThrowsAsync<InvalidStringException>(() => processor.ProcessWithDelayAsync("test", -1));
        }

        [Test("Тест с таймаутом - должен пройти")]
        [Timeout(1500)]
        public async Task TestTimeout_Success()
        {
            int result = await processor.SimulateLongOperationAsync(700);
            Assert.AreEqual(700, result);
        }

        [Test("Тест с таймаутом - должен упасть")]
        [Timeout(500)]
        public async Task TestTimeout_Fail()
        {
            int result = await processor.SimulateLongOperationAsync(2000);
            Assert.AreEqual(2000, result);
        }
    }
}