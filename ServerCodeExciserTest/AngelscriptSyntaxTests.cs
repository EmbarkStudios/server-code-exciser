using System;
using System.IO;
using Antlr4.Runtime;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using UnrealAngelscriptServerCodeExcision;

namespace ServerCodeExciser.Tests
{
    [TestClass]
    public class AngelscriptSyntaxTests
    {
        private sealed class ThrowingErrorListener : BaseErrorListener
        {
            public override void SyntaxError(TextWriter output, IRecognizer recognizer, IToken offendingSymbol, int line, int charPositionInLine, string msg, RecognitionException e)
            {
                throw new InvalidOperationException($"line {line}:{charPositionInLine} {msg}");
            }
        }

        [TestMethod]
        [DataRow("mynamespace")]
        [DataRow("Nested::mynamespace")]
        public void Namespace(string @namespace)
        {
            ParseScript($"namespace {@namespace}\r\n{{\r\n}}\r\n");
        }

        [TestMethod]
        public void NamedArgumentsFunctionCall()
        {
            ParseScript("void Func(bool Arg1 = false, int Arg2 = 0) {}\r\nvoid main() { Func(Arg2: 1, Arg1: true); }\r\n");
        }

        [TestMethod]
        [DataRow("")]
        [DataRow("final")]
        [DataRow("override")]
        [DataRow("no_discard")] // UnrealAngelscript
        [DataRow("allow_discard")] // UnrealAngelscript
        [DataRow("accept_temporary_this")] // UnrealAngelscript
        [DataRow("const allow_discard")] // UnrealAngelscript
        public void FunctionModifier(string modifier)
        {
            ParseScript($"bool Func() {modifier}\r\n{{\r\nreturn true;\r\n}}");
        }

        [TestMethod]
        [DataRow("int", "0")]
        [DataRow("int8", "0")]
        [DataRow("int16", "0")]
        [DataRow("int32", "0")]
        [DataRow("int64", "0")]
        [DataRow("uint", "0")]
        [DataRow("uint8", "0")]
        [DataRow("uint16", "0")]
        [DataRow("uint32", "0")]
        [DataRow("uint64", "0")]
        [DataRow("float", "0.0f")]
        [DataRow("float32", "0.0f")]
        [DataRow("float64", "0.0")]
        [DataRow("doublt", "0.0")]
        [DataRow("bool", "false")]
        [DataRow("FName", "n\"MyName\"")] // https://angelscript.hazelight.se/scripting/fname-literals/
        [DataRow("string", "f\"Formatted String: {L:0.1f}\\u00B0\"")] // https://angelscript.hazelight.se/scripting/format-strings/
        public void DataType(string type, string value)
        {
            ParseScript($"{type} VAR = {value};");
        }

        [TestMethod]
        [DataRow("")]
        [DataRow("UCLASS()")]
        [DataRow("UCLASS(Abstract)")]
        public void UClass(string annotation)
        {
            ParseScript($"{annotation} class ClassName : BaseClass {{}};");
        }

        [TestMethod]
        [DataRow("")]
        [DataRow("UPROPERTY()")]
        [DataRow("UPROPERTY(DefaultComponent)")]
        [DataRow("UPROPERTY(DefaultComponent,)")]
        [DataRow("UPROPERTY(DefaultComponent, RootComponent)")]
        public void UProperty(string annotation)
        {
            ParseScript($"class ClassName : BaseClass\r\n{{\r\n\t{annotation}\r\nDummyType DummyProperty;\r\n}};");
        }

        private static void ParseScript(string script)
        {
            var lexer = new UnrealAngelscriptLexer(new AntlrInputStream(script));
            var tokenStream = new CommonTokenStream(lexer);
            var parser = new UnrealAngelscriptParser(tokenStream);
            parser.RemoveErrorListeners();
            parser.AddErrorListener(new ThrowingErrorListener());
            new UnrealAngelscriptSimpleVisitor(script).VisitChildren(parser.script());
        }
    }
}
