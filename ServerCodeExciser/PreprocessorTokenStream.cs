using Antlr4.Runtime;
using Antlr4.Runtime.Misc;
using Microsoft.Build.Framework;
using System;
using static System.Formats.Asn1.AsnWriter;

namespace ServerCodeExciser
{
    public class Preprocessor : ITokenSource
    {
        private readonly ITokenSource m_tokenSource;
        private readonly ScopeStack m_scope = new ScopeStack();

        public Preprocessor(ITokenSource tokenSource)
        {
            m_tokenSource = tokenSource;
        }

        public int Line
        {
            get { return m_tokenSource.Line; }
        }

        public int Column
        {
            get { return m_tokenSource.Column; }
        }

        public ICharStream InputStream
        {
            get { return m_tokenSource.InputStream; }
        }

        public string SourceName
        {
            get { return m_tokenSource.SourceName; }
        }

        public ITokenFactory TokenFactory
        {
            get { return m_tokenSource.TokenFactory; }
            set { m_tokenSource.TokenFactory = value; }
        }

        public IToken NextToken()
        {
            var token = m_tokenSource.NextToken();
            while (token.Type == UnrealAngelscriptLexer.Preprocessor || m_scope.IsInScope("WITH_SERVER"))
            {
                if (token.Type == UnrealAngelscriptLexer.Preprocessor)
                {
                    Process(token.Text);
                }
                else
                {
                    //Console.WriteLine($"skipping: " + token.Text);
                }
                token = m_tokenSource.NextToken();
            }
            return token;
        }

        private void Process(string line)
        {
            if (line.StartsWith("#ifdef "))
            {
                m_scope.Push(line);
            }
            else if (line.StartsWith("#if "))
            {
                m_scope.Push(line);
            }
            else if (line.StartsWith("#ifndef "))
            {
                m_scope.Push(line);
            }
            else if (line.StartsWith("#else"))
            {
                m_scope.Else(out var name);
            }
            else if (line.StartsWith("#endif"))
            {
                m_scope.Pop(out var name);
            }
        }
    }
}
