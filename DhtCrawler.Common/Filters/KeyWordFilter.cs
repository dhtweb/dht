using System;
using System.Collections;
using System.Collections.Generic;

namespace DhtCrawler.Common.Filters
{
    public class KeyWordFilter : IFilter<string>
    {
        private readonly BitArray _headBitArray;
        private readonly BitArray _allBitArray;
        private readonly HashSet<int> _wordLengths;
        private readonly HashSet<string> _words;

        public KeyWordFilter(params string[] words)
        {
            _headBitArray = new BitArray(char.MaxValue + 1);
            _allBitArray = new BitArray(char.MaxValue + 1);
            _wordLengths = new HashSet<int>();
            _words = new HashSet<string>();
            foreach (var word in words)
            {
                Add(word);
            }
        }

        public bool Contain(string content)
        {
            for (var startIndex = 0; startIndex < content.Length; startIndex++)
            {
                var ch = content[startIndex];
                Console.WriteLine(ch);
                Console.WriteLine(startIndex);
                if (!_headBitArray[ch])
                {
                    continue;
                }
                var endIndex = startIndex + 1;
                while (endIndex < content.Length && _allBitArray[content[endIndex]])
                {
                    endIndex++;
                }
                var wordLength = endIndex - startIndex;
                if (!_wordLengths.Contains(wordLength))
                {
                    continue;
                }
                var word = content.Substring(startIndex, wordLength);
                if (_words.Contains(word))
                {
                    return true;
                }
            }
            return false;
        }

        public void Add(string word)
        {
            if (word.Length == 0)
            {
                return;
            }
            if (!_words.Add(word))
            {
                return;
            }
            _wordLengths.Add(word.Length);
            for (var index = 0; index < word.Length; index++)
            {
                var ch = word[index];
                if (index == 0)
                {
                    _headBitArray[ch] = true;
                }
                else
                {
                    _allBitArray[ch] = true;
                }
            }
        }
    }
}