using System;
using MoonSharp.Interpreter;

namespace DevConsole
{
    [MoonSharpUserData]
    public class ComponentProxyArray
    {
        private readonly ComponentProxy[] _items;

        public ComponentProxyArray(ComponentProxy[] items)
        {
            _items = items;
        }

        public int Count => _items.Length;

        public ComponentProxy Get(int index) => _items[index - 1];

        public override string ToString()
        {
            if (_items.Length == 0) return "No components found.";
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < _items.Length; i++)
            {
                sb.AppendLine($"[{i + 1}] {_items[i]}");
            }
            return sb.ToString().TrimEnd();
        }
    }
}