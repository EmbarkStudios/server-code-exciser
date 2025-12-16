using Antlr4.Runtime;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ServerCodeExciser.Tests
{
    [TestClass]
    public class PreprocessorParserTests
    {
        [TestMethod]
        public void ConditionalBranchTest()
        {
            var script = "#ifdef WITH_SERVER\r\n" +
                "#elif RELEASE\r\n" +
                "#elif DEBUG\r\n" +
                "#else\r\n" +
                "#endif // WITH_SERVER\r\n";

            var lexer = new UnrealAngelscriptLexer(new AntlrInputStream(script));
            var tokenStream = new CommonTokenStream(lexer);
            tokenStream.Fill();

            var nodes = PreprocessorParser.Parse(tokenStream);
            Assert.HasCount(1, nodes);
            Assert.AreEqual("#ifdef WITH_SERVER", nodes[0].Directive);
            Assert.AreEqual("#elif RELEASE", nodes[0].Children[0].Directive);
            Assert.AreEqual("#elif DEBUG", nodes[0].Children[1].Directive);
            Assert.AreEqual("#else", nodes[0].Children[2].Directive);
            Assert.AreEqual(1, nodes[0].Span.Start.Line);
            Assert.AreEqual(0, nodes[0].Span.Start.Column);
            Assert.AreEqual(0, nodes[0].Span.StartIndex);
            Assert.AreEqual(5, nodes[0].Span.End.Line);
            Assert.AreEqual(0, nodes[0].Span.End.Column);
            Assert.AreEqual(script.Length - "\r\n".Length - 1, nodes[0].Span.EndIndex);
        }

        [TestMethod]
        public void NestedTest()
        {
            var script = "#ifdef WITH_SERVER\r\n" +
                "  #if RELEASE\r\n" +
                "  #elif DEBUG\r\n" +
                "  #endif // !RELEASE\r\n" +
                "#endif // WITH_SERVER\r\n";

            var lexer = new UnrealAngelscriptLexer(new AntlrInputStream(script));
            var tokenStream = new CommonTokenStream(lexer);
            tokenStream.Fill();

            var nodes = PreprocessorParser.Parse(tokenStream);
            Assert.HasCount(1, nodes);
            Assert.AreEqual("#ifdef WITH_SERVER", nodes[0].Directive);
            Assert.AreEqual("#if RELEASE", nodes[0].Children[0].Directive);
            Assert.AreEqual("#elif DEBUG", nodes[0].Children[0].Children[0].Directive);
            Assert.AreEqual(0, nodes[0].Span.End.Column);
            Assert.AreEqual(script.Length - "\r\n".Length - 1, nodes[0].Span.EndIndex);
        }
    }
}
