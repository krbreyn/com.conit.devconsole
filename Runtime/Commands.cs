using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

namespace DevConsole
{
    public class Commands
    {
        private readonly Action<string> _output;
        private readonly Action<string> _debug;

        private readonly List<string> logLines;
        private readonly List<string> commandHistory;
        private Text outputText;

        private static readonly Regex ColorTagRegex = new Regex(@"</?color(?:=[^>]*)?>", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public Commands(Action<string> output, Action<string> debug,
            List<string> log_Lines, List<string> command_History, Text output_Text)
        {
            _output = output;
            _debug = message => debug($"DEVCONSOLE DEBUG: {message}");
            logLines = log_Lines;
            commandHistory = command_History;
            outputText = output_Text;
        }

        [LuaCommand("echo", "Prints given text")]
        public void Echo(string msg) => _output(msg);

        [LuaCommand("timescale", "Change the timescale")]
        public void Timescale(float scale)
        {
            Time.timeScale = scale;
            _output($"Time scale set to {scale}");
        }

        [LuaCommand("quit", "Quit the game")]
        public void Quit()
        {
            Application.Quit();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
        }

        [LuaCommand("clear", "Clear the console")]    
        public void Clear()
        {
            logLines.Clear();
            outputText.text = string.Empty;
        }

        [LuaCommand("reloadscene", "Reload the current scene")]
        public void ReloadScene()
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        [LuaCommand("fps", "Show FPS")]
        public void FPS()
        {
            _output($"FPS: {(1f / Time.unscaledDeltaTime):F1}");
        }

        [LuaCommand("dumpcmdhist", "Copy command history to clipboard")]
        public void DumpCmdHist()
        {
            DumpLinesToClipboard(commandHistory, "command history", stripColorTags: false);
        }

        [LuaCommand("dumploghist", "Copy log history to clipboard")]
        public void DumpLogHist()
        {
            DumpLinesToClipboard(logLines, "log history", stripColorTags: true);
        }

        [LuaCommand("findfirst", "Find first GameObject with the given name")]
        public GameObjectProxy FindFirst(string name)
        {
            return GameObjectProxy.FindFirst(name);
        }

        [LuaCommand("findall", "Find all GameObjects with the given name")]
        public GameObjectProxyArray FindAll(string name)
        {
            var results = GameObjectProxy.FindAll(name);
            _output($"Found {results.Count} GameObject(s)");
            return results;
        }

        [LuaCommand("findfirstbytag", "Find first GameObject with the given tag")]
        public GameObjectProxy FindFirstByTag(string tag)
        {
            return GameObjectProxy.FindFirstByTag(tag);
        }

        [LuaCommand("findallbytag", "Find all GameObjects with the given tag")]
        public GameObjectProxyArray FindAllByTag(string tag)
        {
            var results = GameObjectProxy.FindAllByTag(tag);
            _output($"Found {results.Count} GameObject(s) with tag '{tag}'");
            return results;
        }

        [LuaCommand("vec3", "Create a Vector3 value")]
        public Vector3 Vec3(float x, float y, float z)
        {
            return new Vector3(x, y, z);
        }

        private void DumpLinesToClipboard(List<string> lines, string label, bool stripColorTags)
        {
            if (lines == null || lines.Count == 0)
            {
                _output($"No {label} to copy.");
                return;
            }

            if (!stripColorTags)
            {
                GUIUtility.systemCopyBuffer = string.Join("\n", lines);
            }
            else
            {
                var cleaned = new List<string>(lines.Count);
                foreach (var line in lines)
                {
                    cleaned.Add(StripColorTags(line));
                }
                GUIUtility.systemCopyBuffer = string.Join("\n", cleaned);
            }

            _output($"Copied {lines.Count} {label} line(s) to clipboard.");
        }

        private static string StripColorTags(string value)
        {
            if (string.IsNullOrEmpty(value)) return value;
            return ColorTagRegex.Replace(value, string.Empty);
        }
    }
}