using System.Collections.Generic;
using ServerCodeExcisionCommon;

namespace ServerCodeExciser
{
    public sealed class PreprocessorNode
    {
        public PreprocessorNode(string directive, SourceSpan span)
        {
            Directive = directive;
            Span = span;
        }

        public string Directive { get; set; }

        public SourceSpan Span { get; set; }

        public List<PreprocessorNode> Children { get; set; } = new();
    }
}
