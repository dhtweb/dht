using System.IO;
using JiebaNet.Segmenter;
using Lucene.Net.Analysis;

namespace DhtCrawler.Common.Index.Analyzer
{
    public class JieBaAnalyzer : Lucene.Net.Analysis.Analyzer
    {

        protected override TokenStreamComponents CreateComponents(string fieldName, TextReader reader)
        {
            var seg = new JiebaSegmenter();
            var tokenizer = new JiebaTokenizer(seg, reader);
            return new TokenStreamComponents(tokenizer);
        }
    }
}
