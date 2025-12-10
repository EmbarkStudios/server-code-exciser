using System.Collections.Generic;

namespace ServerCodeExcisionCommon
{
    public interface IServerCodeExcisionLanguage
    {
        List<string> ServerOnlySymbolRegexes { get; }

        List<string> ServerOnlySymbols { get; }

        string ServerPrecompilerSymbol { get; }

        string ServerScopeStartString { get; }

        string ServerScopeEndString { get; }

        T CreateLexer<T>(Antlr4.Runtime.AntlrInputStream inputStream)
            where T : Antlr4.Runtime.Lexer;

        T CreateParser<T>(Antlr4.Runtime.CommonTokenStream tokenStream)
            where T : Antlr4.Runtime.Parser;

        IServerCodeVisitor CreateSimpleVisitor(string code);

        IServerCodeVisitor CreateFunctionVisitor(string code);

        IServerCodeVisitor CreateSymbolVisitor(string code);

        bool AnyServerOnlySymbolsInScript(string script);
    }
}
