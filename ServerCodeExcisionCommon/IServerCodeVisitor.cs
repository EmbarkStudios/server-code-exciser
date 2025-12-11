using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace ServerCodeExcisionCommon
{
    public struct ServerOnlyScopeData
    {
        public string CalledFrom { get; }

        public SourceSpan Span { get; }

        public int Opt_ElseIndex { get; }

        public string Opt_ElseContent { get; set; }

        public ServerOnlyScopeData(SourceSpan span, int elseIndex, [CallerMemberName] string caller = "")
        {
            CalledFrom = caller;
            Span = span;
            Opt_ElseIndex = elseIndex;
            Opt_ElseContent = "";
        }
    }

    public interface IServerCodeVisitor
    {
        List<ServerOnlyScopeData> DetectedServerOnlyScopes { get; }
        Dictionary<int, HashSet<string>> ClassStartIdxDummyReferenceData { get; }
        int TotalNumberOfFunctionCharactersVisited { get; }

        void VisitContext(Antlr4.Runtime.Tree.IParseTree context);
    }
}
