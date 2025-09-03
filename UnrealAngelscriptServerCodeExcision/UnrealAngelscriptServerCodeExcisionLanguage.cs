using ServerCodeExcisionCommon;
using System.Collections.Generic;

namespace UnrealAngelscriptServerCodeExcision
{
    public class UnrealAngelscriptServerCodeExcisionLanguage : IServerCodeExcisionLanguage
    {
        private readonly List<string> _angelscriptServerOnlySymbolRegexes = new List<string>
        {
            @"^System::IsServer\(\)$",
            @"^[A-z]+\.HasAuthority\(\)$",
            @"^UEventAPI::",
        };
        public List<string> ServerOnlySymbolRegexes { get { return _angelscriptServerOnlySymbolRegexes; } }

        private readonly List<string> _angelscriptServerOnlySymbols = new List<string>
        {
            "hasauthority()",
            "server"
        };
        public List<string> ServerOnlySymbols { get { return _angelscriptServerOnlySymbols; } }

        public string ServerPrecompilerSymbol { get { return "WITH_SERVER"; } }
        public string ServerScopeStartString { get { return "#ifdef " + ServerPrecompilerSymbol; } }
        public string ServerScopeEndString { get { return "#endif // " + ServerPrecompilerSymbol; } }

        public Antlr4.Runtime.Lexer CreateLexer(Antlr4.Runtime.AntlrInputStream inputStream)
        {
            return new UnrealAngelscriptLexer(inputStream);
        }

        public IServerCodeParser CreateParser(Antlr4.Runtime.CommonTokenStream tokenStream)
        {
            return new UnrealAngelscriptServerCodeParser(tokenStream);
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
