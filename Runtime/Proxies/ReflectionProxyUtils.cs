using System;
using System.Linq;
using System.Reflection;
using MoonSharp.Interpreter;

namespace DevConsole
{
    internal static class ReflectionProxyUtils
    {
        public static string[] GetReadableMethodSignatures(Type type)
        {
            return type
                .GetMethods(BindingFlags.Instance | BindingFlags.Public)
                .Where(m => !m.IsSpecialName && m.DeclaringType != typeof(object))
                .Select(FormatMethodSignature)
                .Distinct()
                .OrderBy(s => s)
                .ToArray();
        }

        public static string[] GetReadablePropertyValues(object instance)
        {
            if (instance == null) return Array.Empty<string>();

            return instance.GetType()
                .GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .Where(p => p.GetIndexParameters().Length == 0) // skip indexers
                .Where(p => p.GetCustomAttribute<ObsoleteAttribute>() == null) // skip legacy Unity props
                .OrderBy(p => p.Name, StringComparer.Ordinal)
                .Select(p => FormatPropertyWithValue(instance, p))
                .ToArray();
        }

        internal static object DynValueToObject(DynValue dv)
        {
            switch (dv.Type)
            {
                case DataType.Boolean:
                    return dv.Boolean;
                case DataType.Number:
                    return (float)dv.Number; // common Unity expectation
                case DataType.String:
                    return dv.String;
                case DataType.UserData:
                    return dv.ToObject();
                case DataType.Table:
                    return TableToObjectArray(dv.Table); // supports pseudo-multi-arg payload
                case DataType.Nil:
                case DataType.Void:
                    return null;
                default:
                    return dv.ToObject();
            }
        }

        internal static object[] TableToObjectArray(Table table)
        {
            if (table == null || table.Length == 0)
                return Array.Empty<object>();

            var result = new object[table.Length];
            for (int i = 1; i <= table.Length; i++)
                result[i - 1] = DynValueToObject(table.Get(i));

            return result;
        }

        private static string FormatMethodSignature(MethodInfo method)
        {
            var parameters = method.GetParameters();
            var args = string.Join(", ", parameters.Select(p => $"{p.ParameterType.Name} {p.Name}"));
            return $"{method.Name}({args})";
        }

        private static string FormatPropertyWithValue(object instance, PropertyInfo property)
        {
            if (!property.CanRead)
                return $"{property.Name} = <write-only>";

            try
            {
                var value = property.GetValue(instance);
                return $"{property.Name} = {(value == null ? "null" : value.ToString())}";
            }
            catch (TargetInvocationException tie) when (tie.InnerException != null)
            {
                return $"{property.Name} = <error: {tie.InnerException.GetType().Name}: {tie.InnerException.Message}>";
            }
            catch (Exception ex)
            {
                return $"{property.Name} = <error: {ex.GetType().Name}: {ex.Message}>";
            }
        }
    }
}