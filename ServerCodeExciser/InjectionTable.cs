using System;
using System.Collections.Generic;

namespace ServerCodeExciser
{
    public class InjectionTable
    {
        private readonly Dictionary<int, List<string>> m_table = new Dictionary<int, List<string>>();

        public void Add(int line, string value)
        {
            if (m_table.TryGetValue(line, out var list))
            {
                list.Add(value);
            }
            else
            {
                m_table.Add(line, new List<string> { value });
            }
        }

        public IEnumerable<string> Get(int line)
        {
            if (m_table.TryGetValue(line, out var list))
            {
                return list;
            }
            return Array.Empty<string>();
        }
    }
}
