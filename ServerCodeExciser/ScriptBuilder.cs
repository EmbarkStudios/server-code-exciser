using System;
using System.Text;

namespace ServerCodeExciser
{
    public class ScriptBuilder
    {
        private ScopeStack m_scope = new ScopeStack();
        private StringBuilder m_text = new StringBuilder();

        public void AddLine(string line)
        {
            var trimmedLine = line.Trim();
            if (trimmedLine.StartsWith("#ifdef "))
            {
                if (m_scope.Push(trimmedLine))
                {
                    m_text.AppendLine(trimmedLine);
                }
            }
            else if (trimmedLine.StartsWith("#if "))
            {
                if (m_scope.Push(trimmedLine))
                {
                    m_text.AppendLine(trimmedLine);
                }
            }
            else if (trimmedLine.StartsWith("#ifndef "))
            {
                if (m_scope.Push(trimmedLine))
                {
                    m_text.AppendLine(trimmedLine);
                }
            }
            else if (trimmedLine.StartsWith("#else"))
            {
                if (m_scope.Pop(out var name))
                {
                    name = name.Trim('!');
                    m_scope.Push(name);
                    m_text.AppendLine($"#else // {name}");
                }
                else
                {
                    m_text.AppendLine($"#else");
                }
            }
            else if (trimmedLine.StartsWith("#endif"))
            {
                if (m_scope.Pop(out var name))
                {
                    m_text.AppendLine($"#endif // {name}");
                }
            }
            else
            {
                m_text.AppendLine(line);
            }
        }

        public override string ToString()
        {
            return m_text.ToString();
        }

        public bool IsInScope(string name)
        {
            return m_scope.IsInScope(name);
        }
    }
}
