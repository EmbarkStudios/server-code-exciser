using System;
using System.Collections.Generic;
using System.Linq;
using Antlr4.Runtime;
using ServerCodeExcisionCommon;

namespace ServerCodeExciser
{
    public class PreprocessorParser
    {
        public static List<PreprocessorNode> Parse(BufferedTokenStream tokenStream)
        {
            var directives = tokenStream
                .GetTokens()
                .Where(t => t.Channel == UnrealAngelscriptLexer.PREPROCESSOR_CHANNEL)
                .Where(t => t.Type == UnrealAngelscriptLexer.Directive)
                .OrderBy(t => t.StartIndex)
                .ToList();

            var rootNodes = new List<PreprocessorNode>();
            var ifStack = new Stack<PreprocessorNode>();

            foreach (var token in directives)
            {
                switch (token.Text)
                {
                    case var t when t.StartsWith("#if", StringComparison.Ordinal): // #if, #ifdef, #ifndef
                        {
                            var scope = CreateScope(token);
                            if (ifStack.TryPeek(out var parent))
                            {
                                parent.Children.Add(scope);
                            }
                            else
                            {
                                rootNodes.Add(scope);
                            }
                            ifStack.Push(scope);
                        }
                        break;

                    case var t when t.StartsWith("#elif", StringComparison.Ordinal) || t.StartsWith("#else", StringComparison.Ordinal): // #elif, #elifdef, #elifndef, #else
                        {
                            var scope = CreateScope(token);
                            if (ifStack.TryPeek(out var parent))
                            {
                                parent.Children.Add(scope);
                                // adjust #if / #elif scope for closing bounds.
                                parent.Span = new SourceSpan
                                {
                                    Start = parent.Span.Start,
                                    StartIndex = parent.Span.StartIndex,
                                    End = new SourcePosition(token.Line, token.Column),
                                    EndIndex = token.StartIndex,
                                };
                            }
                        }
                        break;

                    case var t when t.StartsWith("#endif", StringComparison.Ordinal):
                        {
                            if (ifStack.TryPop(out var parent))
                            {
                                // close parent (#if) to the range of the entire block.
                                parent.Span = new SourceSpan
                                {
                                    Start = parent.Span.Start,
                                    StartIndex = parent.Span.StartIndex,
                                    End = new SourcePosition(token.Line, token.Column),
                                    EndIndex = token.StopIndex,
                                };
                            }
                        }
                        break;
                }
            }

            return rootNodes;
        }

        private static PreprocessorNode CreateScope(IToken token)
        {
            return new PreprocessorNode(
                token.Text,
                new SourceSpan(
                    token.Line,
                    token.Column,
                    token.Line,
                    token.Column,
                    token.StartIndex,
                    token.StopIndex
                )
            );
        }
    }
}
