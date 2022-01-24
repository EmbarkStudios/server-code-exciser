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
		public List<string> ServerOnlySymbolRegexes	{ get { return _angelscriptServerOnlySymbolRegexes; } }

		private List<string> _angelscriptServerOnlySymbols = new List<string>
		{
			"hasauthority()",
			"server"
		};
		public List<string> ServerOnlySymbols { get { return _angelscriptServerOnlySymbols; } }

		public string ServerPrecompilerSymbol { get { return "WITH_SERVER"; } }
		public string ServerScopeStartString { get { return "\r\n#ifdef " + ServerPrecompilerSymbol; } }
		public string ServerScopeEndString { get { return "#endif // " + ServerPrecompilerSymbol + "\r\n"; } }

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
