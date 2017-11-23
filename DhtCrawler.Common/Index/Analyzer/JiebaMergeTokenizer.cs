using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DhtCrawler.Common.Index.Utils;
using JiebaNet.Segmenter;
using Lucene.Net.Analysis;
using Lucene.Net.Analysis.TokenAttributes;
using Token = JiebaNet.Segmenter.Token;

namespace DhtCrawler.Common.Index.Analyzer
{
    public class JiebaMergeTokenizer : Tokenizer
    {
        private readonly JiebaSegmenter _segmenter;
        private readonly ICharTermAttribute _termAtt;
        private readonly IOffsetAttribute _offsetAtt;
        private readonly ITypeAttribute _typeAtt;

        private IList<Token> _tokens;
        private readonly ISet<string> _keywords;
        private int _position = -1;


        public JiebaMergeTokenizer(IEnumerable<string> keywords, TextReader input) : base(input)
        {
            _segmenter = new JiebaSegmenter();
            _termAtt = AddAttribute<ICharTermAttribute>();
            _offsetAtt = AddAttribute<IOffsetAttribute>();
            _typeAtt = AddAttribute<ITypeAttribute>();
            _keywords = new HashSet<string>(keywords, StringComparer.OrdinalIgnoreCase);
            ResetTextReader(input);

        }

        public sealed override bool IncrementToken()
        {
            ClearAttributes();
            _position++;
            if (_position < _tokens.Count)
            {
                var token = _tokens[_position];
                var chars = token.Word.ToCharArray();
                _termAtt.CopyBuffer(chars, 0, chars.Length);
                _offsetAtt.SetOffset(token.StartIndex, token.EndIndex);
                _typeAtt.Type = "Jieba";
                return true;
            }

            End();
            return false;
        }

        public void ResetTextReader(TextReader reader)
        {
            var text = reader.ReadToEnd();
            _tokens = _segmenter.TokenizeAll(text).MergeTokenList(_keywords);
            _position = -1;
        }
    }
}
