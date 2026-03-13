using UnityEngine;
using UnityEngine.EventSystems;

namespace DevConsole
{
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
}