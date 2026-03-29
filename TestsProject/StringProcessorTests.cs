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

        [AfterClass]
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

        [Test("Async обработка")]
        public async Task TestProcessWithDelayAsync()
        {
            string result = await processor.ProcessWithDelayAsync("TeSt", 100);
            Assert.AreEqual("test", result.ToLower());
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

        [Test("Короткая для дубликатов")]
        public void TestRemoveDuplicates_Short_Throws()
        {
            Assert.Throws<InvalidStringException>(() => processor.RemoveDuplicates("a"));
        }

        [Test("Async отрицательная")]
        public async Task TestProcessWithDelay_Negative_Throws()
        {
            await Assert.ThrowsAsync<InvalidStringException>(() => processor.ProcessWithDelayAsync("test", -1));
        }

        [Test("Количество обработанных")]
        public async Task TestProcessedCount()
        {
            processor = new StringProcessor();  
            await processor.ProcessWithDelayAsync("one", 50);
            await processor.ProcessWithDelayAsync("two", 50);
            Assert.AreEqual(2, processor.GetProcessedCount());

        }
    }
}