using System.Collections.Generic;

namespace ServerCodeExcisionCommon
{
    public struct ServerOnlyScopeData
    {
        public int StartIndex;
        public int StopIndex;
        public string Opt_ElseContent;

        public ServerOnlyScopeData(int startIndex, int stopIndex)
        {
            StartIndex = startIndex;
            StopIndex = stopIndex;
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
