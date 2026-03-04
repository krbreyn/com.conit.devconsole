using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace DevConsole
{
    public class DeveloperConsole : MonoBehaviour
    {
        [Header("Console Settings")]
        public Key toggleKey = Key.Backquote; // ` key to open/close
        public int maxLogLines = 100;
        public float scrollSpeed = 0.1f;

        // UI references (created at runtime)
        private GameObject consolePanel;
        private InputField inputField;
        private Text outputText;
        private ScrollRect scrollRect;

        private readonly List<string> logLines = new List<string>();
        private readonly List<string> commandHistory = new List<string>();
        private int historyIndex = -1;
        private bool isOpen;
        private string savedInput = string.Empty;

        public MoonSharpConsole console;

        // Input blocking
        private PlayerInput[] cachedPlayerInputs;
        private CursorLockMode previousCursorLockMode;
        private bool previousCursorVisible;

        void Awake()
        {
            BuildConsoleUI();
            consolePanel.SetActive(false);

            console = new MoonSharpConsole(Log, LogError);
            console.RegisterCommands(new Commands(Log, LogError, logLines, commandHistory, outputText));
            // Capture Unity's debug log messages
            //Application.logMessageReceived += HandleUnityLog;
        }

        void OnDestroy()
        {
            //Application.logMessageReceived -= HandleUnityLog;
        }

        void Update()
        {
            // Resume all background Lua scripts regardless of console visibility
            console.Tick();

            var keyboard = Keyboard.current;
            if (keyboard == null) return;

            if (keyboard[toggleKey].wasPressedThisFrame)
            {
                // Strip the toggle key character that leaks into the input field on close
                if (isOpen && inputField.text.EndsWith("`"))
                    inputField.text = inputField.text.Substring(0, inputField.text.Length - 1);

                ToggleConsole();
                return;
            }

            if (!isOpen) return;

            // Navigate command history with Up/Down arrows
            if (keyboard[Key.UpArrow].wasPressedThisFrame)
            {
                NavigateHistory(-1);
            }
            else if (keyboard[Key.DownArrow].wasPressedThisFrame)
            {
                NavigateHistory(1);
            }
        }

        // ───────────────────────── Public API ─────────────────────────

        /// <summary>Log a plain message to the console.</summary>
        public void Log(string message)
        {
            AppendLog(message, "FFFFFF");
        }

        /// <summary>Log a warning (yellow) to the console.</summary>
        public void LogWarning(string message)
        {
            AppendLog(message, "FFFF00");
        }

        /// <summary>Log an error (red) to the console.</summary>
        public void LogError(string message)
        {
            AppendLog(message, "FF4444");
        }

        // ───────────────────── Console Logic ──────────────────────

        private void ToggleConsole()
        {
            isOpen = !isOpen;

            if (isOpen)
            {
                consolePanel.SetActive(true);
                SetGameInputEnabled(false);
                inputField.ActivateInputField();
                inputField.Select();
            }
            else
            {
                consolePanel.SetActive(false);
                SetGameInputEnabled(true);
            }
        }

        private void SetGameInputEnabled(bool enabled)
        {
            if (!enabled)
            {
                // Save cursor state before overriding
                previousCursorLockMode = Cursor.lockState;
                previousCursorVisible = Cursor.visible;

                // Unlock and show cursor so the user can interact with the console
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            else
            {
                // Restore cursor state
                Cursor.lockState = previousCursorLockMode;
                Cursor.visible = previousCursorVisible;
            }

            // Disable/enable all PlayerInput components to block/restore gameplay input
            cachedPlayerInputs = FindObjectsByType<PlayerInput>(FindObjectsSortMode.None);
            foreach (var pi in cachedPlayerInputs)
            {
                pi.enabled = enabled;
            }
        }

        private void OnSubmitCommand(string text)
        {
            if (!isOpen) return;

            // onEndEdit fires on focus loss (e.g. clicking) — only process on Enter
            if (!Keyboard.current[Key.Enter].wasPressedThisFrame &&
                !Keyboard.current[Key.NumpadEnter].wasPressedThisFrame)
            {
                inputField.ActivateInputField();
                return;
            }

            if (string.IsNullOrWhiteSpace(text)) return;

            AppendLog($"> {text}", "00FF00");

            commandHistory.Add(text);
            historyIndex = commandHistory.Count;

            ProcessCommand(text);

            inputField.text = string.Empty;
            inputField.ActivateInputField();
            inputField.Select();
        }

        private void ProcessCommand(string raw)
        {
            console.Eval(raw);
        }

        private void ScrollOutput(float delta)
        {
            scrollRect.verticalNormalizedPosition = Mathf.Clamp01(
                scrollRect.verticalNormalizedPosition + delta);
        }

        private void NavigateHistory(int direction)
        {
            if (commandHistory.Count == 0) return;

            // Save the current input when first entering history
            if (historyIndex >= commandHistory.Count)
            {
                savedInput = inputField.text;
            }

            historyIndex += direction;

            if (historyIndex < 0)
            {
                historyIndex = 0;
            }
            else if (historyIndex >= commandHistory.Count)
            {
                // Past the newest entry — restore what was in the input box
                historyIndex = commandHistory.Count;
                inputField.text = savedInput;
                inputField.caretPosition = inputField.text.Length;
                return;
            }

            inputField.text = commandHistory[historyIndex];
            inputField.caretPosition = inputField.text.Length;
        }

        private void AppendLog(string message, string hexColor)
        {
            //#if UNITY_EDITOR || DEVELOPMENT_BUILD
            //        Debug.Log($"DeveloperConsole.AppendLog message='{message}' beforeCount={logLines.Count}");
            //#endif
            logLines.Add($"<color=#{hexColor}>{message}</color>");

            while (logLines.Count > maxLogLines)
                logLines.RemoveAt(0);

            outputText.text = string.Join("\n", logLines) + "\n";

            LayoutRebuilder.ForceRebuildLayoutImmediate(outputText.rectTransform);
            LayoutRebuilder.ForceRebuildLayoutImmediate(scrollRect.content);

            //#if UNITY_EDITOR || DEVELOPMENT_BUILD
            //        Debug.Log(
            //            $"DeveloperConsole UI rects output={outputText.rectTransform.rect} " +
            //            $"content={scrollRect.content.rect} viewport={scrollRect.viewport.rect}");
            //        Debug.Log($"DeveloperConsole.AppendLog outputLen={outputText.text.Length}");
            //#endif
            Canvas.ForceUpdateCanvases();
            scrollRect.verticalNormalizedPosition = 0f;
        }

        private void HandleUnityLog(string message, string stackTrace, LogType type)
        {
            switch (type)
            {
                case LogType.Error:
                case LogType.Exception:
                    LogError(message);
                    break;
                case LogType.Warning:
                    LogWarning(message);
                    break;
                default:
                    Log(message);
                    break;
            }
        }

        #region UI
        // ──────────────────── UI Construction ────────────────────

        private void BuildConsoleUI()
        {
            // ── Root Canvas ──
            GameObject canvasGO = new GameObject("DeveloperConsoleCanvas");
            canvasGO.transform.SetParent(transform);
            Canvas canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 9999; // Always on top
            canvasGO.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasGO.AddComponent<GraphicRaycaster>();

            // Ensure an EventSystem exists (using Input System UI module)
            if (FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
            {
                GameObject es = new GameObject("EventSystem");
                es.AddComponent<UnityEngine.EventSystems.EventSystem>();
                es.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
            }

            // ── Console Panel (smaller, centered at top) ──
            consolePanel = CreatePanel(canvasGO.transform, "ConsolePanel",
                new Color(0.1f, 0.1f, 0.1f, 0.92f),
                new Vector2(0.1f, 0.6f), new Vector2(0.9f, 1f),
                new Vector2(0, 0), new Vector2(0, -10));

            // ── Scroll View (output log) ──
            GameObject scrollGO = new GameObject("ScrollView");
            scrollGO.transform.SetParent(consolePanel.transform, false);
            scrollRect = scrollGO.AddComponent<ScrollRect>();
            RectTransform scrollRT = scrollGO.GetComponent<RectTransform>();
            scrollRT.anchorMin = Vector2.zero;
            scrollRT.anchorMax = Vector2.one;
            scrollRT.offsetMin = new Vector2(10, 40); // leave room for input at bottom
            scrollRT.offsetMax = new Vector2(-10, -10);

            // Viewport
            GameObject viewportGO = CreatePanel(scrollGO.transform, "Viewport",
                Color.clear, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            viewportGO.AddComponent<Mask>().showMaskGraphic = false;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        viewportGO.GetComponent<Image>().color = new Color(1f, 0f, 1f, 0.05f);
#endif
            scrollRect.viewport = viewportGO.GetComponent<RectTransform>();

            // Content (holds the output text)
            GameObject contentGO = new GameObject("Content");
            contentGO.transform.SetParent(viewportGO.transform, false);
            RectTransform contentRT = contentGO.AddComponent<RectTransform>();
            contentRT.anchorMin = new Vector2(0, 1);
            contentRT.anchorMax = Vector2.one;
            contentRT.pivot = new Vector2(0, 1);
            contentRT.offsetMin = Vector2.zero;
            contentRT.offsetMax = Vector2.zero;
            ContentSizeFitter fitter = contentGO.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scrollRect.content = contentRT;
            scrollRect.horizontal = false;

            // Output Text (directly on content so ContentSizeFitter works)
            outputText = contentGO.AddComponent<Text>();
            outputText.font = Font.CreateDynamicFontFromOSFont("Consolas", 12);
            outputText.fontSize = 12;
            outputText.color = Color.white;
            outputText.supportRichText = true;
            outputText.alignment = TextAnchor.UpperLeft;
            outputText.horizontalOverflow = HorizontalWrapMode.Wrap;
            outputText.verticalOverflow = VerticalWrapMode.Overflow;
            outputText.raycastTarget = false;

            // ── Input Field (bottom bar) ──
            GameObject inputGO = new GameObject("InputField");
            inputGO.transform.SetParent(consolePanel.transform, false);
            Image inputBG = inputGO.AddComponent<Image>();
            inputBG.color = new Color(0.15f, 0.15f, 0.15f, 1f);
            RectTransform inputRT = inputGO.GetComponent<RectTransform>();
            inputRT.anchorMin = Vector2.zero;
            inputRT.anchorMax = new Vector2(1, 0);
            inputRT.offsetMin = new Vector2(10, 5);
            inputRT.offsetMax = new Vector2(-10, 35);

            // Input text child
            GameObject inputTextGO = new GameObject("Text");
            inputTextGO.transform.SetParent(inputGO.transform, false);
            Text inputText = inputTextGO.AddComponent<Text>();
            inputText.font = Font.CreateDynamicFontFromOSFont("Consolas", 12);
            inputText.fontSize = 12;
            inputText.color = Color.white;
            inputText.supportRichText = false;
            RectTransform inputTextRT = inputTextGO.GetComponent<RectTransform>();
            inputTextRT.anchorMin = Vector2.zero;
            inputTextRT.anchorMax = Vector2.one;
            inputTextRT.offsetMin = new Vector2(8, 2);
            inputTextRT.offsetMax = new Vector2(-8, -2);

            // Placeholder
            GameObject placeholderGO = new GameObject("Placeholder");
            placeholderGO.transform.SetParent(inputGO.transform, false);
            Text placeholder = placeholderGO.AddComponent<Text>();
            placeholder.font = inputText.font;
            placeholder.fontSize = 14;
            placeholder.fontStyle = FontStyle.Italic;
            placeholder.color = new Color(1, 1, 1, 0.35f);
            placeholder.text = "Type a command...";
            RectTransform phRT = placeholderGO.GetComponent<RectTransform>();
            phRT.anchorMin = Vector2.zero;
            phRT.anchorMax = Vector2.one;
            phRT.offsetMin = new Vector2(8, 2);
            phRT.offsetMax = new Vector2(-8, -2);

            inputField = inputGO.AddComponent<InputField>();
            inputField.textComponent = inputText;
            inputField.placeholder = placeholder;
            inputField.onEndEdit.AddListener(OnSubmitCommand);

            // ── Resize Handles (bottom-left and bottom-right corners) ──
            RectTransform panelRT = consolePanel.GetComponent<RectTransform>();
            CreateResizeHandle(consolePanel.transform, "ResizeHandleBL", panelRT,
                new Vector2(0, 0), new Vector2(0, 0), new Vector2(14, 14), // bottom-left
                resizeLeft: true, resizeRight: false);
            CreateResizeHandle(consolePanel.transform, "ResizeHandleBR", panelRT,
                new Vector2(1, 0), new Vector2(1, 0), new Vector2(-14, 14), // bottom-right
                resizeLeft: false, resizeRight: true);

            // ── Drag Handle (top-center, moves the entire panel) ──
            CreateDragHandle(consolePanel.transform, "DragHandle", panelRT);
        }

        private void CreateResizeHandle(Transform parent, string name, RectTransform panelRT,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 sizePivotOffset,
            bool resizeLeft, bool resizeRight)
        {
            const float handleSize = 9f;

            GameObject handleGO = new GameObject(name);
            handleGO.transform.SetParent(parent, false);
            Image img = handleGO.AddComponent<Image>();
            img.color = new Color(1f, 1f, 1f, 0.25f);

            RectTransform rt = handleGO.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = new Vector2(resizeLeft ? 0f : 1f, 0f);
            rt.sizeDelta = new Vector2(handleSize, handleSize);
            rt.anchoredPosition = Vector2.zero;

            ConsoleResizeHandle handle = handleGO.AddComponent<ConsoleResizeHandle>();
            handle.panelRT = panelRT;
            handle.resizeLeft = resizeLeft;
            handle.resizeRight = resizeRight;
        }

        private void CreateDragHandle(Transform parent, string name, RectTransform panelRT)
        {
            const float handleWidth = 40f;
            const float handleHeight = 10f;

            GameObject handleGO = new GameObject(name);
            handleGO.transform.SetParent(parent, false);
            Image img = handleGO.AddComponent<Image>();
            img.color = new Color(1f, 1f, 1f, 0.25f);

            RectTransform rt = handleGO.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 1f);
            rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.sizeDelta = new Vector2(handleWidth, handleHeight);
            rt.anchoredPosition = Vector2.zero;

            ConsoleDragHandle handle = handleGO.AddComponent<ConsoleDragHandle>();
            handle.panelRT = panelRT;
        }

        private GameObject CreatePanel(Transform parent, string name, Color color,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            Image img = go.AddComponent<Image>();
            img.color = color;
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;
            return go;
        }
        #endregion
    }

    /// <summary>
    /// Drag handle that moves the developer console panel by shifting all four anchors equally.
    /// </summary>
    public class ConsoleDragHandle : MonoBehaviour, IBeginDragHandler, IDragHandler
    {
        public RectTransform panelRT;

        private Canvas rootCanvas;

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (rootCanvas == null)
                rootCanvas = panelRT.GetComponentInParent<Canvas>().rootCanvas;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (rootCanvas == null) return;

            Vector2 canvasSize = ((RectTransform)rootCanvas.transform).rect.size;
            Vector2 delta = eventData.delta / canvasSize;

            Vector2 minAnchor = panelRT.anchorMin + delta;
            Vector2 maxAnchor = panelRT.anchorMax + delta;

            // Clamp so the panel stays fully on screen
            float width = maxAnchor.x - minAnchor.x;
            float height = maxAnchor.y - minAnchor.y;

            if (minAnchor.x < 0f) { minAnchor.x = 0f; maxAnchor.x = width; }
            if (maxAnchor.x > 1f) { maxAnchor.x = 1f; minAnchor.x = 1f - width; }
            if (minAnchor.y < 0f) { minAnchor.y = 0f; maxAnchor.y = height; }
            if (maxAnchor.y > 1f) { maxAnchor.y = 1f; minAnchor.y = 1f - height; }

            panelRT.anchorMin = minAnchor;
            panelRT.anchorMax = maxAnchor;
        }
    }

    /// <summary>
    /// Drag handle that resizes the developer console panel by adjusting its anchors.
    /// Attach to a small corner element; set resizeLeft / resizeRight to control which
    /// horizontal edge moves. The bottom edge always moves vertically.
    /// </summary>
    public class ConsoleResizeHandle : MonoBehaviour, IBeginDragHandler, IDragHandler
    {
        public RectTransform panelRT;
        public bool resizeLeft;
        public bool resizeRight;

        private Canvas rootCanvas;
        private Vector2 minAnchor;
        private Vector2 maxAnchor;

        private const float MinWidth = 0.15f;  // minimum panel width  (fraction of screen)
        private const float MinHeight = 0.10f; // minimum panel height (fraction of screen)

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (rootCanvas == null)
                rootCanvas = panelRT.GetComponentInParent<Canvas>().rootCanvas;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (rootCanvas == null) return;

            Vector2 canvasSize = ((RectTransform)rootCanvas.transform).rect.size;
            Vector2 delta = eventData.delta / canvasSize;

            minAnchor = panelRT.anchorMin;
            maxAnchor = panelRT.anchorMax;

            // Vertical: move the bottom edge (anchorMin.y)
            minAnchor.y = Mathf.Clamp(minAnchor.y + delta.y, 0f, maxAnchor.y - MinHeight);

            // Horizontal: move the appropriate side
            if (resizeLeft)
                minAnchor.x = Mathf.Clamp(minAnchor.x + delta.x, 0f, maxAnchor.x - MinWidth);
            if (resizeRight)
                maxAnchor.x = Mathf.Clamp(maxAnchor.x + delta.x, minAnchor.x + MinWidth, 1f);

            panelRT.anchorMin = minAnchor;
            panelRT.anchorMax = maxAnchor;
        }
    }
}