using System;
using Lucene.Net.Analysis;
using Lucene.Net.Analysis.TokenAttributes;

namespace DhtCrawler.Common.Index.Analyzer
{
    public sealed class WordLengthFilter : TokenFilter
    {
        private readonly ICharTermAttribute termAtt;
        private readonly int minLength;
        private readonly int maxLength;

        public WordLengthFilter(int minLength, int maxLength, TokenStream input) : base(input)
        {
            this.minLength = minLength;
            this.maxLength = maxLength;
            this.termAtt = this.AddAttribute<ICharTermAttribute>();
        }

        public override bool IncrementToken()
        {
            if (!this.m_input.IncrementToken())
                return false;
            return this.termAtt.Length >= minLength && this.termAtt.Length <= maxLength;
        }
    }
}
