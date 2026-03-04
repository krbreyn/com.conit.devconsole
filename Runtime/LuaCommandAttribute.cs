using System;

namespace DevConsole
{
    [AttributeUsage(AttributeTargets.Method)]
    public class LuaCommandAttribute : Attribute
    {
        public LuaCommandAttribute(string name, string description)
        {
            Name = name;
            Description = description;
        }

        public string Name { get; }
        public string Description { get; }
    }
}