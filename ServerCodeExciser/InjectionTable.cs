using SharpCompress.Common;
using System;
using System.Collections.Generic;

namespace ServerCodeExciser
{
    public class Marker
    {
        public bool Start { get; }
        public string OptElse { get; }
        public string Context { get; }

        public Marker(string context, bool start, string optElse = "")
        {
            Start = start;
            OptElse = optElse;
            Context = context;
        }

        public void Write(ScriptBuilder builder)
        {
            if (Start)
            {
                builder.AddLine($"#ifdef WITH_SERVER // {Context}");
            }
            else
            {
                if (builder.IsInScope("WITH_SERVER"))
                {
                    var values = OptElse.Split(new char[] {'\r', '\n'}, StringSplitOptions.RemoveEmptyEntries);
                    foreach (var v in values)
                    {
                        builder.AddLine(v);
                    }
                }
                builder.AddLine($"#endif // WITH_SERVER {Context}");
            }
        }
    }

    public class InjectionTable
    {
        private readonly Dictionary<int, List<Marker>> m_table = new Dictionary<int, List<Marker>>();

        public void Add(int line, Marker value)
        {
            if (m_table.TryGetValue(line, out var list))
            {
                list.Add(value);
            }
            else
            {
                m_table.Add(line, new List<Marker> { value });
            }
        }

        public IEnumerable<Marker> Get(int line)
        {
            if (m_table.TryGetValue(line, out var list))
            {
                return list;
            }
            return Array.Empty<Marker>();
        }
    }
}
