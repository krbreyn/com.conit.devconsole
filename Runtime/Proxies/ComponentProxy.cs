using System;
using System.Globalization;
using System.Linq;
using System.Reflection;
using MoonSharp.Interpreter;
using UnityEngine;

namespace DevConsole
{
    [MoonSharpUserData]
    public class ComponentProxy
    {
        private readonly Component _component;

        public ComponentProxy(Component component)
        {
            _component = component;
        }

        public bool IsValid => _component != null;
        public string Name => _component != null ? _component.name : "<null>";
        public string Type => _component != null ? _component.GetType().Name : "<null>";
        public string FullType => _component != null ? _component.GetType().FullName : "<null>";

        public override string ToString()
        {
            if (_component == null) return "Component: <null>";
            return $"{Type} ({FullType})";
        }

        public StringArrayProxy ListMethods()
        {
            if (_component == null) throw new ScriptRuntimeException("Component is null.");
            return new StringArrayProxy(ReflectionProxyUtils.GetReadableMethodSignatures(_component.GetType()));
        }

        #region property/field commands

        [LuaCommand("ListProperties", "Lists public readable properties and their current values on this component.")]
        public StringArrayProxy ListProperties()
        {
            if (_component == null) throw new ScriptRuntimeException("Component is null.");
            return new StringArrayProxy(ReflectionProxyUtils.GetReadablePropertyValues(_component));
        }

        [LuaCommand("GetProperty", "Gets a single public property value on this component by name.")]
        public object GetProperty(string propertyName)
        {
            if (_component == null) throw new ScriptRuntimeException("Component is null.");
            if (string.IsNullOrWhiteSpace(propertyName))
                throw new ScriptRuntimeException("Property name cannot be null or empty.");

            var property = _component.GetType()
                .GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .FirstOrDefault(p =>
                    p.GetIndexParameters().Length == 0 &&
                    string.Equals(p.Name, propertyName, StringComparison.OrdinalIgnoreCase));

            if (property == null)
                throw new ScriptRuntimeException(
                    $"Property '{propertyName}' was not found on component '{_component.GetType().Name}'.");

            if (!property.CanRead)
                throw new ScriptRuntimeException(
                    $"Property '{property.Name}' on component '{_component.GetType().Name}' is write-only.");

            try
            {
                return property.GetValue(_component);
            }
            catch (TargetInvocationException tie) when (tie.InnerException != null)
            {
                throw new ScriptRuntimeException(
                    $"Failed to read property '{property.Name}': {tie.InnerException.GetType().Name}: {tie.InnerException.Message}");
            }
            catch (Exception ex)
            {
                throw new ScriptRuntimeException(
                    $"Failed to read property '{property.Name}': {ex.GetType().Name}: {ex.Message}");
            }
        }

        [LuaCommand("SetProperty", "Sets a single public property value on this component by name.")]
        public void SetProperty(string propertyName, DynValue value)
        {
            if (_component == null) throw new ScriptRuntimeException("Component is null.");
            if (string.IsNullOrWhiteSpace(propertyName))
                throw new ScriptRuntimeException("Property name cannot be null or empty.");

            var property = _component.GetType()
                .GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .FirstOrDefault(p =>
                    p.GetIndexParameters().Length == 0 &&
                    string.Equals(p.Name, propertyName, StringComparison.OrdinalIgnoreCase));

            if (property == null)
                throw new ScriptRuntimeException(
                    $"Property '{propertyName}' was not found on component '{_component.GetType().Name}'.");

            if (!property.CanWrite)
                throw new ScriptRuntimeException(
                    $"Property '{property.Name}' on component '{_component.GetType().Name}' is read-only.");

            var raw = value == null || value.Type == DataType.Nil || value.Type == DataType.Void
                ? null
                : value.ToObject();

            var targetType = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;

            try
            {
                object converted;

                if (raw == null)
                {
                    converted = null;
                }
                else if (targetType.IsInstanceOfType(raw))
                {
                    converted = raw;
                }
                else if (targetType.IsEnum)
                {
                    converted = raw is string s
                        ? Enum.Parse(targetType, s, true)
                        : Enum.ToObject(targetType, Convert.ChangeType(raw, Enum.GetUnderlyingType(targetType), CultureInfo.InvariantCulture));
                }
                else
                {
                    converted = Convert.ChangeType(raw, targetType, CultureInfo.InvariantCulture);
                }

                property.SetValue(_component, converted);
            }
            catch (Exception ex)
            {
                throw new ScriptRuntimeException(
                    $"Failed to set property '{property.Name}' on '{_component.GetType().Name}': {ex.Message}");
            }
        }


        [LuaCommand("ListFields", "Lists public field values on this component.")]
        public StringArrayProxy ListFields()
        {
            if (_component == null) throw new ScriptRuntimeException("Component is null.");

            var fields = _component.GetType()
                .GetFields(BindingFlags.Instance | BindingFlags.Public)
                .OrderBy(f => f.Name, StringComparer.Ordinal)
                .Select(f =>
                {
                    var value = f.GetValue(_component);
                    return $"{f.Name} = {(value == null ? "null" : value.ToString())}";
                })
                .ToArray();

            return new StringArrayProxy(fields);
        }

        [LuaCommand("GetField", "Gets a single public field value on this component by name.")]
        public object GetField(string fieldName)
        {
            if (_component == null) throw new ScriptRuntimeException("Component is null.");
            if (string.IsNullOrWhiteSpace(fieldName))
                throw new ScriptRuntimeException("Field name cannot be null or empty.");

            var field = _component.GetType()
                .GetFields(BindingFlags.Instance | BindingFlags.Public)
                .FirstOrDefault(f => string.Equals(f.Name, fieldName, StringComparison.OrdinalIgnoreCase));

            if (field == null)
                throw new ScriptRuntimeException(
                    $"Field '{fieldName}' was not found on component '{_component.GetType().Name}'.");

            return field.GetValue(_component);
        }

        [LuaCommand("SetField", "Sets a single public field value on this component by name.")]
        public void SetField(string fieldName, DynValue value)
        {
            if (_component == null) throw new ScriptRuntimeException("Component is null.");
            if (string.IsNullOrWhiteSpace(fieldName))
                throw new ScriptRuntimeException("Field name cannot be null or empty.");

            var field = _component.GetType()
                .GetFields(BindingFlags.Instance | BindingFlags.Public)
                .FirstOrDefault(f => string.Equals(f.Name, fieldName, StringComparison.OrdinalIgnoreCase));

            if (field == null)
                throw new ScriptRuntimeException(
                    $"Field '{fieldName}' was not found on component '{_component.GetType().Name}'.");

            var raw = value == null || value.Type == DataType.Nil || value.Type == DataType.Void
                ? null
                : value.ToObject();

            var targetType = Nullable.GetUnderlyingType(field.FieldType) ?? field.FieldType;

            try
            {
                object converted;

                if (raw == null)
                {
                    converted = null;
                }
                else if (targetType.IsInstanceOfType(raw))
                {
                    converted = raw;
                }
                else if (targetType.IsEnum)
                {
                    converted = raw is string s
                        ? Enum.Parse(targetType, s, true)
                        : Enum.ToObject(targetType, Convert.ChangeType(raw, Enum.GetUnderlyingType(targetType), CultureInfo.InvariantCulture));
                }
                else
                {
                    converted = Convert.ChangeType(raw, targetType, CultureInfo.InvariantCulture);
                }

                field.SetValue(_component, converted);
            }
            catch (Exception ex)
            {
                throw new ScriptRuntimeException(
                    $"Failed to set field '{field.Name}' on '{_component.GetType().Name}': {ex.Message}");
            }
        }

        
        #endregion

        #region Reflection Method Invocation

        [LuaCommand("InvokeMethod", "Invokes a method on this component by reflection.")]
        public object InvokeMethod(string methodName, DynValue arg = null)
        {
            if (_component == null) throw new ScriptRuntimeException("Component is null.");
            if (string.IsNullOrWhiteSpace(methodName)) throw new ScriptRuntimeException("Method name is required.");

            var inputArgs = BuildInvokeArgs(arg);

            var candidates = _component.GetType()
                .GetMethods(BindingFlags.Instance | BindingFlags.Public)
                .Where(m => m.Name == methodName && m.GetParameters().Length == inputArgs.Length)
                .ToArray();

            if (candidates.Length == 0)
                throw new ScriptRuntimeException($"No overload '{methodName}' with {inputArgs.Length} argument(s) on '{_component.GetType().Name}'.");

            MethodInfo bestMethod = null;
            object[] bestConvertedArgs = null;
            int bestScore = int.MaxValue;
            bool ambiguous = false;

            foreach (var method in candidates)
            {
                var parameters = method.GetParameters();
                var converted = new object[inputArgs.Length];
                var score = 0;
                var ok = true;

                for (int i = 0; i < inputArgs.Length; i++)
                {
                    if (!TryConvertArgument(inputArgs[i], parameters[i].ParameterType, out converted[i], out var argScore))
                    {
                        ok = false;
                        break;
                    }

                    score += argScore;
                }

                if (!ok) continue;

                if (score < bestScore)
                {
                    bestScore = score;
                    bestMethod = method;
                    bestConvertedArgs = converted;
                    ambiguous = false;
                }
                else if (score == bestScore)
                {
                    ambiguous = true;
                }
            }

            if (bestMethod == null)
            {
                throw new ScriptRuntimeException(
                    $"No compatible overload '{methodName}' found on '{_component.GetType().Name}' for argument types ({string.Join(", ", inputArgs.Select(a => a?.GetType().Name ?? "null"))}).");
            }

            if (ambiguous)
            {
                throw new ScriptRuntimeException(
                    $"Ambiguous overload '{methodName}' on '{_component.GetType().Name}'. Use more specific argument types.");
            }

            try
            {
                return bestMethod.Invoke(_component, bestConvertedArgs);
            }
            catch (TargetInvocationException ex)
            {
                throw new ScriptRuntimeException(
                    $"Invoke failed: {ex.InnerException?.GetType().Name ?? ex.GetType().Name}: {ex.InnerException?.Message ?? ex.Message}");
            }
        }

        private static object[] BuildInvokeArgs(DynValue arg)
        {
            if (arg == null || arg.Type == DataType.Nil || arg.Type == DataType.Void)
                return Array.Empty<object>();

            if (arg.Type == DataType.Table)
                return ReflectionProxyUtils.TableToObjectArray(arg.Table);

            return new[] { ReflectionProxyUtils.DynValueToObject(arg) };
        }

        private static bool TryConvertArgument(object value, Type targetType, out object converted, out int score)
        {
            converted = null;
            score = 0;

            var nonNullableTarget = Nullable.GetUnderlyingType(targetType) ?? targetType;

            // null handling
            if (value == null)
            {
                if (!nonNullableTarget.IsValueType || Nullable.GetUnderlyingType(targetType) != null)
                {
                    converted = null;
                    score = 1;
                    return true;
                }
                return false;
            }

            var valueType = value.GetType();

            // exact / assignable
            if (nonNullableTarget.IsAssignableFrom(valueType))
            {
                converted = value;
                score = valueType == nonNullableTarget ? 0 : 1;
                return true;
            }

            // enum conversion from string/number
            if (nonNullableTarget.IsEnum)
            {
                if (value is string s && Enum.TryParse(nonNullableTarget, s, true, out var enumValue))
                {
                    converted = enumValue;
                    score = 2;
                    return true;
                }

                if (IsNumericType(valueType))
                {
                    try
                    {
                        var underlying = Enum.GetUnderlyingType(nonNullableTarget);
                        var numeric = Convert.ChangeType(value, underlying, CultureInfo.InvariantCulture);
                        converted = Enum.ToObject(nonNullableTarget, numeric);
                        score = 3;
                        return true;
                    }
                    catch
                    {
                        return false;
                    }
                }

                return false;
            }

            // numeric conversions (Lua numbers often arrive as double)
            if (IsNumericType(valueType) && IsNumericType(nonNullableTarget))
            {
                try
                {
                    converted = Convert.ChangeType(value, nonNullableTarget, CultureInfo.InvariantCulture);
                    score = 3;
                    return true;
                }
                catch
                {
                    return false;
                }
            }

            return false;
        }

        private static bool IsNumericType(Type t)
        {
            t = Nullable.GetUnderlyingType(t) ?? t;
            switch (System.Type.GetTypeCode(t))
            {
                case TypeCode.Byte:
                case TypeCode.SByte:
                case TypeCode.UInt16:
                case TypeCode.UInt32:
                case TypeCode.UInt64:
                case TypeCode.Int16:
                case TypeCode.Int32:
                case TypeCode.Int64:
                case TypeCode.Decimal:
                case TypeCode.Double:
                case TypeCode.Single:
                    return true;
                default:
                    return false;
            }
        }

        #endregion

    }
}