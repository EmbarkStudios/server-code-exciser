using System;

namespace ServerCodeExcisionCommon
{
    /// <summary>
    /// Represents a span of source code from a start position to an end position.
    /// </summary>
    public struct SourceSpan
    {
        public SourcePosition Start { get; set; }

        public SourcePosition End { get; set; }

        /// <summary>
        /// The absolute start index in the source text (0-based).
        /// </summary>
        public int StartIndex { get; set; }

        /// <summary>
        /// The absolute end index in the source text (0-based, exclusive).
        /// </summary>
        public int EndIndex { get; set; }

        public SourceSpan(SourcePosition start, SourcePosition end, int startIndex, int endIndex)
        {
            if (startIndex > endIndex)
            {
                throw new ArgumentException($"{nameof(startIndex)} is greater than {nameof(endIndex)}");
            }

            Start = start;
            End = end;
            StartIndex = startIndex;
            EndIndex = endIndex;
        }

        public SourceSpan(int startLine, int startColumn, int endLine, int endColumn, int startIndex, int endIndex)
            : this(new SourcePosition(startLine, startColumn), new SourcePosition(endLine, endColumn), startIndex, endIndex)
        {
        }
    }
}
