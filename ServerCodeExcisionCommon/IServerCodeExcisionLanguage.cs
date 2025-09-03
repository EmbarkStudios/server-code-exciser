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

        Antlr4.Runtime.Lexer CreateLexer(Antlr4.Runtime.AntlrInputStream inputStream);
        IServerCodeParser CreateParser(Antlr4.Runtime.CommonTokenStream tokenStream);

        IServerCodeVisitor CreateSimpleVisitor(string code);
        IServerCodeVisitor CreateFunctionVisitor(string code);
        IServerCodeVisitor CreateSymbolVisitor(string code);

        bool AnyServerOnlySymbolsInScript(string script);
    }
}