using ServerCodeExcisionCommon;

namespace UnrealAngelscriptServerCodeExcision
{
	public class UnrealAngelscriptServerCodeParser : IServerCodeParser
	{
		UnrealAngelscriptParser _parser;

		public UnrealAngelscriptServerCodeParser(Antlr4.Runtime.CommonTokenStream tokenStream)
		{
			_parser = new UnrealAngelscriptParser(tokenStream);
			_parser.AddErrorListener(new ExcisionParserErrorListener());
		}

		public Antlr4.Runtime.Tree.IParseTree GetParseTree()
		{
			return _parser.script();
		}
	}
}
