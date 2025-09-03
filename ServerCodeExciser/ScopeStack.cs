using System;
using System.Collections.Generic;

namespace ServerCodeExciser
{
    public class ScopeStack
    {
        private Dictionary<string, int> m_scopes = new Dictionary<string, int>();
        private Stack<string> m_scope = new Stack<string>();

        public bool Push(string name)
        {
            if (name.StartsWith("#ifdef "))
            {
                name = TrimAndStripComments(name.Substring(7));
            }
            else if (name.StartsWith("#if "))
            {
                name = TrimAndStripComments(name.Substring(4));
            }
            else if (name.StartsWith("#ifndef "))
            {
                name = "!" + TrimAndStripComments(name.Substring(8));
            }

            m_scope.Push(name);

            if (m_scopes.ContainsKey(name) && m_scopes[name] > 0)
            {
                m_scopes[name] += 1;
                return false;
            }
            else
            {
                m_scopes[name] = 1;
                return true;
            }
        }

        public bool Pop(out string name)
        {
            if (m_scope.Count <= 0)
            {
                name = string.Empty;
                return false;
            }

            name = m_scope.Pop();
            m_scopes[name] -= 1;
            return m_scopes[name] == 0;
        }

        public bool IsInScope(string name)
        {
            if (m_scopes.TryGetValue(name, out var count))
            {
                return count > 0;
            }
            return false;
        }

        private string TrimAndStripComments(string text)
        {
            int idx = text.IndexOf("//");
            if (idx >= 0)
            {
                return text.Substring(0, idx).Trim();
            }
            return text.Trim();
        }
    }
}
