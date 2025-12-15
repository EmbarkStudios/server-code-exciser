using System.Collections.Generic;
using ServerCodeExcisionCommon;

namespace ServerCodeExciser
{
    public sealed class PreprocessorScope
    {
        public PreprocessorScope(string directive, SourceSpan span)
        {
            Directive = directive;
            Span = span;
        }

        public string Directive { get; set; }

        public SourceSpan Span { get; set; }

        public List<PreprocessorScope> Children { get; set; } = new();
    }
}
