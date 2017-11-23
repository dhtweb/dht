using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DhtCrawler.Common.Compare;
using JiebaNet.Segmenter;

namespace DhtCrawler.Common.Index.Utils
{
    public static class AnalyzerUtils
    {
        public static IList<Token> MergeTokenList(this IList<Token> tokens)
        {
            var resultToken = new List<Token>(tokens.Count);
            for (var i = 0; i < tokens.Count; i++)
            {
                var select = tokens[i];
                var i1 = i;
                var flag = tokens.Where((t, j) => i1 != j).All(compare => @select.StartIndex < compare.StartIndex || @select.EndIndex > compare.EndIndex);
                if (flag)
                    resultToken.Add(select);
            }
            return resultToken;
        }

        public static IList<Token> MergeTokenList(this IEnumerable<Token> tokens, ISet<string> keyWords)
        {
            var contentWords = tokens.Where(t => keyWords.Contains(t.Word)).ToArray();
            if (contentWords.Length <= 1)
            {
                return contentWords;
            }
            return MergeTokenList(contentWords);
        }

        public static IEnumerable<string> Cut(this string str, bool cutAll = true)
        {
            if (string.IsNullOrWhiteSpace(str))
            {
                return new string[0];
            }
            var segement = new JiebaSegmenter();
            return segement.Cut(str, cutAll);
        }

        public static IEnumerable<string> CutForSearch(this string str)
        {
            if (string.IsNullOrWhiteSpace(str))
            {
                return new string[0];
            }
            var segement = new JiebaSegmenter();
            return segement.CutForSearch(str);
        }

        public static IEnumerable<Token> CutToToken(this JiebaSegmenter segmenter, string text, bool cutAll = true)
        {
            var words = segmenter.Cut(text, cutAll).Where(s => !string.IsNullOrWhiteSpace(s)).ToArray();
            var indexDic = new Dictionary<string, int>();
            var tokenArray = new Token[words.Length];
            var checkIndex = 0;
            for (var i = 0; i < words.Length; i++)
            {
                var word = words[i];
                checkIndex = text.IndexOf(word, indexDic.ContainsKey(word) ? indexDic[word] + 1 : checkIndex, StringComparison.Ordinal);
                tokenArray[i] = new Token(word, checkIndex, checkIndex + word.Length);
                indexDic[word] = checkIndex;
            }
            return tokenArray;
        }

        private static readonly IEqualityComparer<Token> TokenEqualComparer = new WrapperEqualityComparer<Token>((t1, t2) => t1.StartIndex == t2.StartIndex && t1.EndIndex == t2.EndIndex && string.Equals(t1.Word, t2.Word, StringComparison.Ordinal), t => t.Word.GetHashCode() ^ (t.StartIndex << 16 & t.EndIndex));

        private static readonly IComparer<Token> TokenComparer = new WrapperComparer<Token>((t1, t2) =>
        {
            var start = t1.StartIndex - t2.StartIndex;
            if (start == 0 && t1.EndIndex == t2.EndIndex && string.Equals(t1.Word, t2.Word, StringComparison.Ordinal))
                return 0;
            return start == 0 ? t1.EndIndex - t2.EndIndex : start;
        });

        public static IEnumerable<Token> TokenizeAll(this JiebaSegmenter segmenter, string text)
        {
            return new SortedSet<Token>(segmenter.CutToToken(text).Union(segmenter.CutToToken(text, false)), TokenComparer);

        }
    }
}
