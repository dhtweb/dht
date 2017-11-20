using System.Collections.Generic;
using System.IO;
using System.Linq;
using JiebaNet.Segmenter;
using Lucene.Net.Analysis;
using Lucene.Net.Analysis.TokenAttributes;
using Token = JiebaNet.Segmenter.Token;

namespace DhtCrawler.Common.Index.Analyzer
{
    public class JiebaTokenizer : Tokenizer
    {
        private JiebaSegmenter segmenter;
        private ICharTermAttribute termAtt;
        private IOffsetAttribute offsetAtt;
        private ITypeAttribute typeAtt;

        private List<Token> tokens;
        private int position = -1;


        public JiebaTokenizer(JiebaSegmenter seg, TextReader input) : base(input)
        {
            segmenter = seg;
            termAtt = AddAttribute<ICharTermAttribute>();
            offsetAtt = AddAttribute<IOffsetAttribute>();
            typeAtt = AddAttribute<ITypeAttribute>();

            var text = input.ReadToEnd();
            tokens = segmenter.Tokenize(text, TokenizerMode.Search).ToList();
        }

        public sealed override bool IncrementToken()
        {
            ClearAttributes();
            position++;
            if (position < tokens.Count)
            {
                var token = tokens[position];
                termAtt.Append(token.Word);
                offsetAtt.SetOffset(token.StartIndex, token.EndIndex);
                typeAtt.Type = "Jieba";
                return true;
            }

            End();
            return false;
        }
    }
}
