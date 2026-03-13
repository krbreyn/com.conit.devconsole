using System;
using MoonSharp.Interpreter;

namespace DevConsole
{
    [MoonSharpUserData]
    public class GameObjectProxyArray
    {
        private readonly GameObjectProxy[] _items;

        public GameObjectProxyArray(GameObjectProxy[] items)
        {
            _items = items;
        }

        public int Count => _items.Length;

        public GameObjectProxy Get(int index) => _items[index - 1];

        public override string ToString()
        {
            if (_items.Length == 0) return "No GameObjects found.";
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < _items.Length; i++)
            {
                sb.AppendLine($"[{i + 1}] {_items[i]}");
            }
            return sb.ToString().TrimEnd();
        }
    }
}