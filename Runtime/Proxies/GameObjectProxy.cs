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

        [LuaCommand("Info", "Returns a formatted summary of this GameObject.")]
        public string Info() => ToString();
 
        public override string ToString()
        {
            if (_target == null) return "GameObject: <null>";
            var pos = _target.transform.position;
            return $"GameObject '{_target.name}' tag='{_target.tag}' layer={_target.layer} active={_target.activeInHierarchy} pos=({pos.x:F2}, {pos.y:F2}, {pos.z:F2})";
        }

        #region Find Commands

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

        #endregion

        #region Set/Add Commands

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

        #endregion

        #region component related commands

        public ComponentProxyArray GetAllComponents()
        {
            if (_target == null) throw new ScriptRuntimeException("GameObject is null.");

            var components = _target.GetComponents<Component>()
                .Select(c => new ComponentProxy(c))
                .ToArray();

            return new ComponentProxyArray(components);
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

        #endregion

        #region parent/child/sibling commands

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

        #endregion

        #region misc commands

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

        public Transform GetTransform()
        {
            if (_target == null) throw new ScriptRuntimeException("GameObject is null.");
            return _target.transform;
        }

        [LuaCommand("ListMethods", "Lists public callable methods on this GameObject type.")]
        public StringArrayProxy ListMethods()
        {
            if (_target == null) throw new ScriptRuntimeException("GameObject is null.");
            return new StringArrayProxy(ReflectionProxyUtils.GetReadableMethodSignatures(_target.GetType()));
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

            _target.SendMessage(methodName, ReflectionProxyUtils.DynValueToObject(arg), options);
        }

        #endregion
    }
}