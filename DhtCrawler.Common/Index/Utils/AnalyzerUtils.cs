using System.Collections.Generic;
using System.Linq;
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

        public static IEnumerable<string> CutForSearch(this string str)
        {
            if (string.IsNullOrWhiteSpace(str))
            {
                return new string[0];
            }
            var segement = new JiebaSegmenter();
            return segement.CutForSearch(str);
        }
    }
}
