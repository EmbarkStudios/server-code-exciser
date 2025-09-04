using Antlr4.Runtime;
using System;
using System.IO;
using System.Linq;

namespace ServerCodeExcisionCommon
{
    public enum EExcisionLanguage
    {
        Unknown,
        Angelscript
    }

    public enum EExcisionMode
    {
        Full,
        AllFunctions,
        ServerOnlyScopes
    }

    public enum EExpressionType
    {
        NotServerOnly,
        ElseIsServerOnly,
        EverythingAfterBranchIsServerOnly,
        ServerOnly
    }

    public enum EReturnType
    {
        NoReturn,
        ReplacedReturn,
        ReferenceReturn,
        RootScopeReferenceReturn
    }

    public struct ExcisionStats
    {
        // How many characters were actually removed.
        public int CharactersExcised;

        // Could be file or function, depending on stats mode.
        public int TotalNrCharacters;
    }

    public struct StatementRun
    {
        public int StartLine;
        public int StartColumn;
        public int StopLine;
        public int StopColumn;

        public StatementRun(int initialVal = -1)
        {
            StartLine = initialVal;
            StartColumn = initialVal;
            StopLine = initialVal;
            StopColumn = initialVal;
        }
    }

    public struct ReturnData
    {
        public EReturnType ReturnType;
        public string DefaultReturnString;
        public StatementRun ReturnStatementRun;

        public ReturnData(string defaultReturnString = "")
        {
            ReturnType = EReturnType.NoReturn;
            DefaultReturnString = defaultReturnString;
            ReturnStatementRun = new StatementRun();
        }
    }

    public enum EExciserReturnValues
    {
        Success,
        BadInputPath,
        InputPathEmpty,
        BadOutputPath,
        BadArgument,
        UnknownExcisionLanguage,
        NothingExcised,
        InternalExcisionError,
        RequiredExcisionRatioNotReached,
        RequiresExcision
    }

    public class ExcisionException : Exception
    {
        public ExcisionException(string excisionError, Exception innerException) : base(excisionError, innerException) { }
    }

    public class ExcisionParserErrorListener : Antlr4.Runtime.IAntlrErrorListener<Antlr4.Runtime.IToken>
    {
        public void SyntaxError(TextWriter output, IRecognizer recognizer, IToken offendingSymbol, int line, int charPositionInLine, string msg, RecognitionException e)
        {
            throw new ExcisionException(string.Format("({0}:{1} - {2})", line, charPositionInLine, msg), e);
        }
    }

    public class ExcisionLexerErrorListener : Antlr4.Runtime.IAntlrErrorListener<int>
    {
        public void SyntaxError(TextWriter output, IRecognizer recognizer, int offendingSymbol, int line, int charPositionInLine, string msg, RecognitionException e)
        {
            throw new ExcisionException(string.Format("({0}:{1} - {2})", line, charPositionInLine, msg), e);
        }
    }

    public static class ExcisionUtils
    {
        private static readonly char[] NewLineChars = { '\r', '\n' };
        private static readonly char[] SkippableScopeChars = { '\t', '\r', '\n' };

        public static int FindScriptIndexForCodePoint(string script, int line, int column)
        {
            int cursor = 0;
            int linesTraversed = 1;
            while (cursor != -1)
            {
                if (linesTraversed == line)
                {
                    break;
                }

                int searchIdx = cursor;
                int windows = script.IndexOf("\r\n", searchIdx);
                int other = script.IndexOfAny(NewLineChars, searchIdx);

                if (windows <= other)
                {
                    cursor = windows + 2;
                }
                else
                {
                    cursor = other + 1;
                }

                ++linesTraversed;
            }

            return (linesTraversed == line) ? (cursor + column) : -1;
        }

        public static int ShrinkServerScope(string script, int start, int end)
        {
            bool skip = true;
            while (skip)
            {
                skip = false;

                int search = script.IndexOfAny(NewLineChars, start) + 2;
                if ((search < end) && SkippableScopeChars.Contains<char>(script.ElementAt(search)))
                {
                    skip = true;
                    ++start;
                }
            }

            return start;
        }

        public static Type FindFirstDirectChildOfType<Type>(Antlr4.Runtime.Tree.IParseTree currentContext)
            where Type : class
        {
            if (currentContext == null)
            {
                return null;
            }

            for (int childIdx = 0; childIdx < currentContext.ChildCount; childIdx++)
            {
                var child = currentContext.GetChild(childIdx) as Type;
                if (child != null)
                {
                    return child;
                }
            }

            return null;
        }

        public static Type FindParentContextOfType<Type>(Antlr4.Runtime.Tree.IParseTree currentContext)
            where Type : class
        {
            if (currentContext == null)
            {
                return null;
            }

            Type candidate = currentContext as Type;
            if (candidate != null)
            {
                return candidate;
            }

            return FindParentContextOfType<Type>(currentContext.Parent);
        }

        public static Type FindDirectParentContextOfTypeWithDifferentSourceInterval<Type>(Antlr4.Runtime.Tree.IParseTree currentContext, Antlr4.Runtime.Misc.Interval initialSourceInterval)
            where Type : class
        {
            if (currentContext == null)
            {
                return null;
            }

            if (currentContext.SourceInterval.a == initialSourceInterval.a && currentContext.SourceInterval.b == initialSourceInterval.b)
            {
                // Go further up
                return FindDirectParentContextOfTypeWithDifferentSourceInterval<Type>(currentContext.Parent, initialSourceInterval);
            }
            else
            {
                // We are now at the first ancestor with a different source interval
                return currentContext as Type;
            }
        }
    }
}
