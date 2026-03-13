using UnityEngine;
using UnityEngine.EventSystems;

namespace DevConsole
{
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