using System;
using System.Collections.Generic;
using ServerCodeExcisionCommon;

namespace UnrealAngelscriptServerCodeExcision
{
    public class UnrealAngelscriptServerCodeExcisionLanguage : IServerCodeExcisionLanguage
    {
        private List<string> _angelscriptServerOnlySymbolRegexes = new List<string>
        {
            @"^System::IsServer\(\)$",
            @"^[A-z]+\.HasAuthority\(\)$"
        };

        public List<string> ServerOnlySymbolRegexes { get { return _angelscriptServerOnlySymbolRegexes; } }

        private List<string> _angelscriptServerOnlySymbols = new List<string>
        {
            "hasauthority()",
            "server"
        };

        public List<string> ServerOnlySymbols { get { return _angelscriptServerOnlySymbols; } }

        public string ServerPrecompilerSymbol { get { return "WITH_SERVER"; } }

        public string ServerScopeStartString { get { return "#ifdef " + ServerPrecompilerSymbol; } }

        public string ServerScopeEndString { get { return "#endif // " + ServerPrecompilerSymbol; } }

        public T CreateLexer<T>(Antlr4.Runtime.AntlrInputStream inputStream)
            where T : Antlr4.Runtime.Lexer
        {
            return (T)Activator.CreateInstance(typeof(T), inputStream);
        }

        public T CreateParser<T>(Antlr4.Runtime.CommonTokenStream tokenStream)
            where T : Antlr4.Runtime.Parser
        {
            return (T)Activator.CreateInstance(typeof(T), tokenStream);
        }

        public IServerCodeVisitor CreateSimpleVisitor(string code)
        {
            return new UnrealAngelscriptSimpleVisitor(code);
        }

        public IServerCodeVisitor CreateFunctionVisitor(string code)
        {
            return new UnrealAngelscriptFunctionVisitor(code);
        }

        public IServerCodeVisitor CreateSymbolVisitor(string code)
        {
            return new UnrealAngelscriptSymbolVisitor(code, this);
        }

        public bool AnyServerOnlySymbolsInScript(string script)
        {
            foreach (var serverOnlySymbol in _angelscriptServerOnlySymbols)
            {
                if (script.ToLower().Contains(serverOnlySymbol))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
