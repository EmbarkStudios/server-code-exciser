using System.Collections.Generic;

namespace ServerCodeExcisionCommon
{
    public struct ServerOnlyScopeData
    {
        public string Context { get; }
        public string Opt_ElseContent { get; set; }
        public int StartLine { get; }
        public int StopLine { get; }

        public ServerOnlyScopeData(string context, int startLine, int stopLine)
        {
            Context = context;
            StartLine = startLine;
            StopLine = stopLine;
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