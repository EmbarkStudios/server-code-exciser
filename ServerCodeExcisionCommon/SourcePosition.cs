namespace ServerCodeExcisionCommon
{
    /// <summary>
    /// Represents a position in source code (line and column).
    /// Lines and columns are 1-based to match editor conventions.
    /// </summary>
    public readonly struct SourcePosition
    {
        public int Line { get; }

        public int Column { get; }

        public SourcePosition(int line, int column)
        {
            Line = line;
            Column = column;
        }

        public override string ToString() => $"({Line}:{Column})";
    }
}
