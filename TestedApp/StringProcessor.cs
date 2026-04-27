using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace TestedApp
{
    public class InvalidStringException : Exception
    {
        public InvalidStringException(string message) : base(message) { }
    }

    public class StringProcessor
    {
        private readonly List<string> processedStrings = new List<string>();

        public int CountWords(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                throw new InvalidStringException("Строка пуста");

            return input.Split(new char[] { ' ', '\t', '\n' }, StringSplitOptions.RemoveEmptyEntries).Length;
        }

        public string ToUpperCase(string input)
        {
            if (string.IsNullOrEmpty(input))
                throw new InvalidStringException("Строка null или empty");

            return input.ToUpper();
        }

        public string RemoveDuplicates(string input)
        {
            if (string.IsNullOrEmpty(input) || input.Length < 2)
                throw new InvalidStringException("Строка слишком короткая");

            return new string(input.Distinct().ToArray());
        }

        public bool IsPalindrome(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                throw new InvalidStringException("Строка пуста для палиндрома");

            string cleaned = input.Replace(" ", "").ToLower();
            return cleaned.SequenceEqual(cleaned.Reverse());
        }

        public async Task<string> ProcessWithDelayAsync(string input, int delayMs)
        {
            if (string.IsNullOrWhiteSpace(input))
                throw new InvalidStringException("Строка пуста");

            if (delayMs < 0)
                throw new InvalidStringException("Задержка отрицательная");

            await Task.Delay(delayMs);
            lock (processedStrings)
            {
                processedStrings.Add(input);
            }

            return input.ToLower();
        }

        public async Task<int> SimulateLongOperationAsync(int delayMs)
        {
            if (delayMs < 0)
                throw new InvalidStringException("Задержка отрицательная");

            await Task.Delay(delayMs);
            return delayMs;
        }

        public int GetProcessedCount()
        {
            lock (processedStrings)
            {
                return processedStrings.Count;
            }
        }
    }
}