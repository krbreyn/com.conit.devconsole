using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using MoonSharp.Interpreter;
using UnityEngine;

namespace DevConsole
{
    [MoonSharpUserData]
    public class GameObjectProxy
    {
        private readonly GameObject _target;

        public GameObjectProxy(GameObject target)
        {
            _target = target;
        }

        public bool IsValid => _target != null;
        public string Name => _target != null ? _target.name : "<null>";
        public string Tag => _target != null ? _target.tag : "<null>";
        public int Layer => _target != null ? _target.layer : -1;
        public bool Active => _target != null && _target.activeInHierarchy;
        public string Position => _target != null ? _target.transform.position.ToString("F2") : "(null)";
        public Vector3 WorldPosition => _target != null ? _target.transform.position : Vector3.zero;
        public float X => _target != null ? _target.transform.position.x : 0f;
        public float Y => _target != null ? _target.transform.position.y : 0f;
        public float Z => _target != null ? _target.transform.position.z : 0f;

        public string Rotation => _target != null ? _target.transform.eulerAngles.ToString("F2") : "(null)";
        public Vector3 WorldRotation => _target != null ? _target.transform.eulerAngles : Vector3.zero;

        public string Scale => _target != null ? _target.transform.localScale.ToString("F2") : "(null)";
        public Vector3 WorldScale => _target != null ? _target.transform.localScale : Vector3.zero;

        public static GameObjectProxy FindFirst(string name)
        {
            var go = GameObject.Find(name);
            if (go == null)
            {
                throw new ScriptRuntimeException($"GameObject '{name}' not found.");
            }

            return new GameObjectProxy(go);
        }

        public static GameObjectProxy FindFirstByTag(string tag)
        {
            var go = GameObject.FindWithTag(tag);
            if (go == null)
            {
                throw new ScriptRuntimeException($"No GameObject with tag '{tag}' found.");
            }

            return new GameObjectProxy(go);
        }

        public static GameObjectProxyArray FindAll(string name)
        {
            var results = GameObject.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Where(go => go.name == name)
                .Select(go => new GameObjectProxy(go))
                .ToArray();

            if (results.Length == 0)
            {
                throw new ScriptRuntimeException($"No GameObjects named '{name}' found.");
            }

            return new GameObjectProxyArray(results);
        }

        public static GameObjectProxyArray FindAllByTag(string tag)
        {
            var results = GameObject.FindGameObjectsWithTag(tag)
                .Select(go => new GameObjectProxy(go))
                .ToArray();

            if (results.Length == 0)
            {
                throw new ScriptRuntimeException($"No GameObjects with tag '{tag}' found.");
            }

            return new GameObjectProxyArray(results);
        }

        [LuaCommand("Info", "Returns a formatted summary of this GameObject.")]
        public string Info() => ToString();

        [LuaCommand("SetPos", "Sets world position: x, y, z.")]
        public void SetPos(float x, float y, float z)
        {
            if (_target == null) throw new ScriptRuntimeException("GameObject is null.");
            _target.transform.position = new Vector3(x, y, z);
        }

        [LuaCommand("AddPos", "Adds to world position: x, y, z.")]
        public void AddPos(float x, float y, float z)
        {
            if (_target == null) throw new ScriptRuntimeException("GameObject is null.");
            _target.transform.position += new Vector3(x, y, z);
        }

        [LuaCommand("SetScale", "Sets local scale: x, y, z.")]
        public void SetScale(float x, float y, float z)
        {
            if (_target == null) throw new ScriptRuntimeException("GameObject is null.");
            _target.transform.localScale = new Vector3(x, y, z);
        }

        [LuaCommand("AddScale", "Adds to local scale: x, y, z.")]
        public void AddScale(float x, float y, float z)
        {
            if (_target == null) throw new ScriptRuntimeException("GameObject is null.");
            _target.transform.localScale += new Vector3(x, y, z);
        }

        [LuaCommand("SetRot", "Sets world rotation (Euler angles): x, y, z.")]
        public void SetRot(float x, float y, float z)
        {
            if (_target == null) throw new ScriptRuntimeException("GameObject is null.");
            _target.transform.eulerAngles = new Vector3(x, y, z);
        }

        [LuaCommand("AddRot", "Adds to world rotation (Euler angles): x, y, z.")]
        public void AddRot(float x, float y, float z)
        {
            if (_target == null) throw new ScriptRuntimeException("GameObject is null.");
            _target.transform.eulerAngles += new Vector3(x, y, z);
        }

        [LuaCommand("SetActive", "Sets active state (true/false).")]
        public void SetActive(bool active)
        {
            _target.SetActive(active);
        }

        [LuaCommand("Delete", "Destroys this GameObject.")]
        public void Delete()
        {
            UnityEngine.Object.Destroy(_target);
        }

        [LuaCommand("Clone", "Clones this GameObject and returns the clone proxy.")]
        public GameObjectProxy Clone()
        {
            if (_target == null) throw new ScriptRuntimeException("GameObject is null.");
            var clone = UnityEngine.Object.Instantiate(_target);
            return new GameObjectProxy(clone);
        }

        public override string ToString()
        {
            if (_target == null) return "GameObject: <null>";
            var pos = _target.transform.position;
            return $"GameObject '{_target.name}' tag='{_target.tag}' layer={_target.layer} active={_target.activeInHierarchy} pos=({pos.x:F2}, {pos.y:F2}, {pos.z:F2})";
        }

        public ComponentProxyArray GetAllComponents()
        {
            if (_target == null) throw new ScriptRuntimeException("GameObject is null.");

            var components = _target.GetComponents<Component>()
                .Select(c => new ComponentProxy(c))
                .ToArray();

            return new ComponentProxyArray(components);
        }

        public Transform GetTransform()
        {
            if (_target == null) throw new ScriptRuntimeException("GameObject is null.");
            return _target.transform;
        }

        public ComponentProxy GetComponent(string componentTypeName)
        {
            if (_target == null) throw new ScriptRuntimeException("GameObject is null.");
            if (string.IsNullOrWhiteSpace(componentTypeName))
                throw new ScriptRuntimeException("Component type name cannot be null or empty.");

            var componentType = ResolveComponentType(componentTypeName);
            if (componentType == null)
                throw new ScriptRuntimeException($"Component type '{componentTypeName}' was not found.");

            if (!typeof(Component).IsAssignableFrom(componentType))
                throw new ScriptRuntimeException($"Type '{componentTypeName}' is not a Unity Component.");

            if (_target.TryGetComponent(componentType, out var component))
                return new ComponentProxy(component);

            throw new ScriptRuntimeException(
                $"GameObject '{_target.name}' does not have component '{componentType.Name}'.");
        }

        private static Type ResolveComponentType(string componentTypeName)
        {
            var name = componentTypeName.Trim();

            // Full type name first (e.g. UnityEngine.Rigidbody)
            var direct = Type.GetType(name, throwOnError: false, ignoreCase: true);
            if (direct != null) return direct;

            // Then search loaded assemblies by full name and short name (e.g. Rigidbody)
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                var byFullName = assembly.GetType(name, throwOnError: false, ignoreCase: true);
                if (byFullName != null) return byFullName;

                Type[] types;
                try
                {
                    types = assembly.GetTypes();
                }
                catch (ReflectionTypeLoadException ex)
                {
                    types = ex.Types.Where(t => t != null).ToArray();
                }

                var byShortName = types.FirstOrDefault(t =>
                    t != null && string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase));

                if (byShortName != null) return byShortName;
            }

            return null;
        }

        [LuaCommand("ListMethods", "Lists public callable methods on this GameObject type.")]
        public StringArrayProxy ListMethods()
        {
            if (_target == null) throw new ScriptRuntimeException("GameObject is null.");
            return new StringArrayProxy(ReflectionProxyUtils.GetReadableMethodSignatures(_target.GetType()));
        }

        #region Spaghetti Reflection & Method Invocation

        [LuaCommand("InvokeComponentMethod", "Invokes a component method by reflection.")]
        public object InvokeComponentMethod(string componentTypeName, string methodName, DynValue arg = null)
        {
            if (_target == null) throw new ScriptRuntimeException("GameObject is null.");
            if (string.IsNullOrWhiteSpace(componentTypeName)) throw new ScriptRuntimeException("Component type is required.");
            if (string.IsNullOrWhiteSpace(methodName)) throw new ScriptRuntimeException("Method name is required.");

            var componentType = ResolveComponentType(componentTypeName);
            if (componentType == null || !typeof(Component).IsAssignableFrom(componentType))
                throw new ScriptRuntimeException($"Invalid component type '{componentTypeName}'.");

            if (!_target.TryGetComponent(componentType, out var component))
                throw new ScriptRuntimeException($"GameObject '{_target.name}' does not have component '{componentTypeName}'.");

            var inputArgs = BuildInvokeArgs(arg);

            var candidates = component.GetType()
                .GetMethods(BindingFlags.Instance | BindingFlags.Public)
                .Where(m => m.Name == methodName && m.GetParameters().Length == inputArgs.Length)
                .ToArray();

            if (candidates.Length == 0)
                throw new ScriptRuntimeException($"No overload '{methodName}' with {inputArgs.Length} argument(s) on '{component.GetType().Name}'.");

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
                    $"No compatible overload '{methodName}' found on '{component.GetType().Name}' for argument types ({string.Join(", ", inputArgs.Select(a => a?.GetType().Name ?? "null"))}).");
            }

            if (ambiguous)
            {
                throw new ScriptRuntimeException(
                    $"Ambiguous overload '{methodName}' on '{component.GetType().Name}'. Use more specific argument types.");
            }

            try
            {
                return bestMethod.Invoke(component, bestConvertedArgs);
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
                return TableToObjectArray(arg.Table);

            return new[] { DynValueToObject(arg) };
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
            switch (Type.GetTypeCode(t))
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

        [LuaCommand("SendMessage", "Calls Unity SendMessage on this GameObject.")]
        public void SendMessage(string methodName, DynValue arg = null, bool requireReceiver = true)
        {
            if (_target == null) throw new ScriptRuntimeException("GameObject is null.");
            if (string.IsNullOrWhiteSpace(methodName))
                throw new ScriptRuntimeException("Method name cannot be null or empty.");

            var options = requireReceiver
                ? SendMessageOptions.RequireReceiver
                : SendMessageOptions.DontRequireReceiver;

            if (arg == null || arg.Type == DataType.Nil || arg.Type == DataType.Void)
            {
                _target.SendMessage(methodName, options);
                return;
            }

            _target.SendMessage(methodName, DynValueToObject(arg), options);
        }

        private static object DynValueToObject(DynValue dv)
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

        #endregion

        private static object[] TableToObjectArray(Table table)
        {
            if (table == null || table.Length == 0)
                return Array.Empty<object>();

            var result = new object[table.Length];
            for (int i = 1; i <= table.Length; i++)
                result[i - 1] = DynValueToObject(table.Get(i));

            return result;
        }

        [LuaCommand("GetParent", "Returns this GameObject's parent as a proxy, or nil if it has no parent.")]
        public GameObjectProxy GetParent()
        {
            if (_target == null) throw new ScriptRuntimeException("GameObject is null.");

            var parent = _target.transform.parent;
            return parent != null ? new GameObjectProxy(parent.gameObject) : null;
        }

        [LuaCommand("GetChildren", "Returns direct children as a proxy array.")]
        public GameObjectProxyArray GetChildren()
        {
            if (_target == null) throw new ScriptRuntimeException("GameObject is null.");

            var transform = _target.transform;
            var children = new GameObjectProxy[transform.childCount];

            for (int i = 0; i < transform.childCount; i++)
                children[i] = new GameObjectProxy(transform.GetChild(i).gameObject);

            return new GameObjectProxyArray(children);
        }

        [LuaCommand("GetChild", "Returns direct child by 1-based index.")]
        public GameObjectProxy GetChild(int index)
        {
            if (_target == null) throw new ScriptRuntimeException("GameObject is null.");

            var transform = _target.transform;
            if (index < 1 || index > transform.childCount)
                throw new ScriptRuntimeException(
                    $"Child index {index} is out of range. Valid range is 1..{transform.childCount}.");

            return new GameObjectProxy(transform.GetChild(index - 1).gameObject);
        }

        [LuaCommand("GetSiblings", "Returns siblings as a proxy array. Optionally include this object.")]
        public GameObjectProxyArray GetSiblings(bool includeSelf = false)
        {
            if (_target == null) throw new ScriptRuntimeException("GameObject is null.");

            var transform = _target.transform;
            var parent = transform.parent;

            GameObject[] siblings;
            if (parent != null)
            {
                siblings = Enumerable.Range(0, parent.childCount)
                    .Select(i => parent.GetChild(i).gameObject)
                    .Where(go => includeSelf || go != _target)
                    .ToArray();
            }
            else
            {
                // Root object: siblings are other root objects in the same scene.
                siblings = _target.scene.GetRootGameObjects()
                    .Where(go => includeSelf || go != _target)
                    .ToArray();
            }

            return new GameObjectProxyArray(siblings.Select(go => new GameObjectProxy(go)).ToArray());
        }

        [LuaCommand("GetSiblingIndex", "Returns this GameObject's 1-based sibling index.")]
        public int GetSiblingIndex()
        {
            if (_target == null) throw new ScriptRuntimeException("GameObject is null.");
            return _target.transform.GetSiblingIndex() + 1;
        }
    }

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

        public StringArrayProxy ListMethods()
        {
            if (_component == null) throw new ScriptRuntimeException("Component is null.");
            return new StringArrayProxy(ReflectionProxyUtils.GetReadableMethodSignatures(_component.GetType()));
        }

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

        public override string ToString()
        {
            if (_component == null) return "Component: <null>";
            return $"{Type} ({FullType})";
        }
    }

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