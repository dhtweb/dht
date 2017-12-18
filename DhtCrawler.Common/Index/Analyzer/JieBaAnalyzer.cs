using System;
using System.Collections.Generic;
using System.IO;
using JiebaNet.Segmenter;
using Lucene.Net.Analysis;
using Lucene.Net.Analysis.Core;
using Lucene.Net.Analysis.Util;
using Lucene.Net.Util;

namespace DhtCrawler.Common.Index.Analyzer
{
    public class JieBaAnalyzer : Lucene.Net.Analysis.Analyzer
    {
        private static readonly ISet<string> StopWordSet;
        private readonly int _minWordLength;
        private readonly int _maxWordLength;
        static JieBaAnalyzer()
        {
            var stopWordPath = Path.Combine(AppContext.BaseDirectory, "Resources", "stopwords.txt");
            StopWordSet = File.Exists(stopWordPath) ? new HashSet<string>(File.ReadAllLines(stopWordPath)) : new HashSet<string>(new[] { " ", "@", "\r", "\n", "。", "，", "：", "；", "、", "“", "”", "【", "】", "《", "》", "（", "）", "—", "'…", ".", ",", ":", ";", "\"", "\"", "[", "]", "<", ">", "(", ")", "#", "*", "&", "%", "￥", "$", "-", "+", "=", "|", "\\", "{", "}" });
        }

        public JieBaAnalyzer(int minWordLength, int maxWordLength)
        {
            this._minWordLength = minWordLength;
            this._maxWordLength = maxWordLength;
        }

        protected override TokenStreamComponents CreateComponents(string fieldName, TextReader reader)
        {
            var seg = new JiebaSegmenter();
            var tokenizer = new JiebaTokenizer(seg, reader);
            TokenStream tokenStream = new LowerCaseFilter(LuceneVersion.LUCENE_48, tokenizer);
            tokenStream = new WordLengthFilter(_minWordLength, _maxWordLength, tokenStream);
            tokenStream = new StopFilter(LuceneVersion.LUCENE_48, tokenStream, new CharArraySet(LuceneVersion.LUCENE_48, StopWordSet, true));
            return new JiebaTokenStreamComponents(tokenizer, tokenStream);
        }

        private class JiebaTokenStreamComponents : TokenStreamComponents
        {
            private readonly JiebaTokenizer jiebaTokenizer;
            public JiebaTokenStreamComponents(JiebaTokenizer tokenizer, TokenStream result)
                : base(tokenizer, result)
            {
                this.jiebaTokenizer = tokenizer;
            }

            protected override void SetReader(TextReader reader)
            {
                base.SetReader(reader);
                jiebaTokenizer.ResetTextReader(reader);
            }
        }
    }
}
