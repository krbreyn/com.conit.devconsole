using System;
using MoonSharp.Interpreter;

namespace DevConsole
{
    [MoonSharpUserData]
    public class StringArrayProxy
    {
        private readonly string[] _items;

        public StringArrayProxy(string[] items)
        {
            _items = items ?? Array.Empty<string>();
        }

        public int Count => _items.Length;

        public string Get(int index) => _items[index - 1];

        public override string ToString()
        {
            if (_items.Length == 0) return "No entries found.";
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < _items.Length; i++)
            {
                sb.AppendLine($"[{i + 1}] {_items[i]}");
            }
            return sb.ToString().TrimEnd();
        }
    }
}