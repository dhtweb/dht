using System.IO;
using JiebaNet.Segmenter;
using Lucene.Net.Analysis;
using Lucene.Net.Analysis.Core;
using Lucene.Net.Util;

namespace DhtCrawler.Common.Index.Analyzer
{
    public class JieBaAnalyzer : Lucene.Net.Analysis.Analyzer
    {

        protected override TokenStreamComponents CreateComponents(string fieldName, TextReader reader)
        {
            var seg = new JiebaSegmenter();
            var tokenizer = new JiebaTokenizer(seg, reader);
            var tokenStream = new LowerCaseFilter(LuceneVersion.LUCENE_48, tokenizer);
            return new JiebaTokenStreamComponents(tokenizer, tokenStream);
        }

        private class JiebaTokenStreamComponents : TokenStreamComponents
        {
            private JiebaTokenizer jiebaTokenizer;
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
