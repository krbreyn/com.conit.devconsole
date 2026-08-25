using MoonSharp.Interpreter;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using UnityEngine;

namespace DevConsole
{
    public class ConsoleEngine
    {
        private readonly Script _script = new Script(CoreModules.Preset_SoftSandbox);
        private readonly List<(string Name, string Description)> _commands = new List<(string Name, string Description)>();
        private readonly Action<string> _output;
        private readonly Action<string> _debug;

        private readonly List<RunningScript> _runningScripts = new List<RunningScript>();
        private int _nextScriptId = 1;
        private int _instructionsPerTick = 1000;

        /// <summary>Number of VM instructions each coroutine may execute per Tick.</summary>
        public int InstructionsPerTick
        {
            get => _instructionsPerTick;
            set => _instructionsPerTick = Math.Max(1, value);
        }

        public ConsoleEngine(Action<string> output, Action<string> debug)
        {
            _output = output;
            _debug = message => debug($"DEVCONSOLE DEBUG: {message}");

            UserData.RegisterType<GameObjectProxy>();
            UserData.RegisterType<GameObjectProxyArray>();
            UserData.RegisterType<ComponentProxy>();
            UserData.RegisterType<ComponentProxyArray>();
            UserData.RegisterType<StringArrayProxy>();
            UserData.RegisterType<Vector3>();
            UserData.RegisterType<Transform>();


            _script.Globals["help"] = (Action)Help;
            //_script.Globals["gameobjecthelp"] = (Action)GameObjectHelp;
            _script.Globals["scripts"] = (Action)ListScripts;
            _script.Globals["kill"] = (Action<int>)Kill;
            _script.Globals["killall"] = (Action)KillAll;
            _script.Options.DebugPrint = s => { _output(s); };

            // Time source in milliseconds (real-time, not affected by timeScale)
            _script.Globals["__now_ms"] = (Func<double>)(() => Time.realtimeSinceStartupAsDouble * 1000.0);

            // Lua wait that cooperatively yields; Tick() resumes it each frame
            _script.DoString(@"
function wait(ms)
    if ms == nil then error('wait(ms): ms is required') end
    if ms < 0 then error('wait(ms): ms must be >= 0') end

    local deadline = __now_ms() + ms
    while __now_ms() < deadline do
        coroutine.yield()
    end
end
");
        }

        public void RegisterCommands(object provider)
        {
            foreach (var method in provider.GetType().GetMethods())
            {
                if (method.GetCustomAttribute<LuaCommandAttribute>() is { } attr)
                {
                    var paramTypes = method.GetParameters().Select(p => p.ParameterType).ToArray();
                    var returnType = method.ReturnType;

                    // Build Action<...> or Func<...> based on signature
                    var delegateType = returnType == typeof(void)
                        ? Expression.GetActionType(paramTypes)
                        : Expression.GetFuncType(paramTypes.Concat(new[] { returnType }).ToArray());

                    _script.Globals[attr.Name] = Delegate.CreateDelegate(delegateType, provider, method);
                    _commands.Add((attr.Name, attr.Description));
                }
            }
        }

        /// <summary>
        /// Compiles and begins executing a Lua string.
        /// One-shot commands finish immediately; loops auto-yield and are
        /// resumed each frame via <see cref="Tick"/>.
        /// </summary>
        public void Eval(string lua)
        {
            try
            {
                DynValue function = _script.LoadString(lua);
                DynValue coroutine = _script.CreateCoroutine(function);
                coroutine.Coroutine.AutoYieldCounter = _instructionsPerTick;

                coroutine.Coroutine.Resume();

                // Track script if coroutine is still alive (yielded/suspended)
                if (coroutine.Coroutine.State != CoroutineState.Dead)
                {
                    int id = _nextScriptId++;
                    _output($"[script #{id} started]");
                    _runningScripts.Add(new RunningScript(id, lua.Trim(), coroutine));
                }
            }
            catch (SyntaxErrorException ex)
            {
                _debug($"Lua syntax error: {ex.DecoratedMessage}");
            }
            catch (ScriptRuntimeException ex)
            {
                _debug($"Lua runtime error: {ex.DecoratedMessage}");
            }
            catch (InterpreterException ex)
            {
                _debug($"Lua interpreter error: {ex.DecoratedMessage}");
            }
            catch (Exception ex)
            {
                _debug($"Unhandled error: {ex.GetType().Name}: {ex.Message}");
            }
        }

        /// <summary>
        /// Call once per frame. Resumes every active long-running script
        /// for another batch of instructions.
        /// </summary>
        public void Tick()
        {
            for (int i = _runningScripts.Count - 1; i >= 0; i--)
            {
                var rs = _runningScripts[i];
                try
                {
                    rs.Coroutine.Coroutine.Resume();

                    if (rs.Coroutine.Coroutine.State == CoroutineState.Dead)
                    {
                        _output($"[script #{rs.Id} finished]");
                        _runningScripts.RemoveAt(i);
                    }
                }
                catch (ScriptRuntimeException ex)
                {
                    _debug($"Script #{rs.Id} runtime error: {ex.DecoratedMessage}");
                    _runningScripts.RemoveAt(i);
                }
                catch (InterpreterException ex)
                {
                    _debug($"Script #{rs.Id} interpreter error: {ex.DecoratedMessage}");
                    _runningScripts.RemoveAt(i);
                }
                catch (Exception ex)
                {
                    _debug($"Script #{rs.Id} error: {ex.GetType().Name}: {ex.Message}");
                    _runningScripts.RemoveAt(i);
                }
            }
        }

        // ─────────────── Built-in script management commands ───────────────

        private void ListScripts()
        {
            if (_runningScripts.Count == 0)
            {
                _output("No running scripts.");
                return;
            }
            _output($"{_runningScripts.Count} running script(s):");
            foreach (var rs in _runningScripts)
            {
                string preview = rs.Source.Length > 60
                    ? rs.Source.Substring(0, 57) + "..."
                    : rs.Source;
                _output($"  #{rs.Id}  {preview}");
            }
        }

        private void Kill(int id)
        {
            int idx = _runningScripts.FindIndex(rs => rs.Id == id);
            if (idx < 0)
            {
                _debug($"No running script with id #{id}.");
                return;
            }
            _runningScripts.RemoveAt(idx);
            _output($"[script #{id} killed]");
        }

        private void KillAll()
        {
            int count = _runningScripts.Count;
            _runningScripts.Clear();
            _output($"[killed {count} script(s)]");
        }

        // ──────────────────────────────────────────────────────────────────

        public IEnumerable<(string Name, string Description)> Commands => _commands;

        private void Help()
        {
            _output("Available commands:");
            foreach (var (name, description) in _commands)
            {
                _output($"  {name} - {description}");
            }
            _output("Built-in:");
            _output("  scripts        - List running background scripts");
            _output("  kill(id)       - Kill a running script by id");
            _output("  killall        - Kill all running scripts");
            _output("  wait(ms)       - Wait for x milliseconds");
            _output("  help()         - View this helpinfo");
            _output("  gameobjecthelp - List annotated GameObjectProxy commands");
        }

        // TODO refine and distinguish between gameobject/component - be more clear, might as well write a manual
        //private void GameObjectHelp()
        //{
        //    var entries = typeof(GameObjectProxy)
        //        .GetMethods(BindingFlags.Instance | BindingFlags.Public)
        //        .Select(m => new { Method = m, Attr = m.GetCustomAttribute<LuaCommandAttribute>() })
        //        .Where(x => x.Attr != null && !x.Method.IsSpecialName)
        //        .OrderBy(x => x.Attr.Name, StringComparer.OrdinalIgnoreCase)
        //        .ToList();

        //    if (entries.Count == 0)
        //    {
        //        _output("No annotated GameObjectProxy commands found.");
        //        return;
        //    }

        //    _output("GameObjectProxy commands:");
        //    foreach (var entry in entries)
        //    {
        //        var args = string.Join(", ", entry.Method.GetParameters().Select(p => p.Name));
        //        _output($"  {entry.Attr.Name}({args}) - {entry.Attr.Description}");
        //    }
        //    _output("  Get(i) [on returned arrays] - Get the item at the specified index. First item is at index 1, not 0.");
        //}

        private class RunningScript
        {
            public readonly int Id;
            public readonly string Source;
            public readonly DynValue Coroutine;

            public RunningScript(int id, string source, DynValue coroutine)
            {
                Id = id;
                Source = source;
                Coroutine = coroutine;
            }
        }
    }

}
