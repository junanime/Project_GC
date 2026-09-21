using UnityEngine;

namespace Vampire
{
    public class SafeArea : MonoBehaviour
    {
        private Rect previousArea;
        private Vector2Int previousSize;
        private void Update()
        {
            if (previousArea != Screen.safeArea || previousSize.x != Screen.width || previousSize.y != Screen.height)
                ResetSafeArea();
        }
        void Start()
        {
            ResetSafeArea();
        }

        public void ResetSafeArea()
        {
            if (Screen.width <= 0 || Screen.height <= 0) return;
            RectTransform rectTransform = GetComponent<RectTransform>();
            if (rectTransform == null) return;
            Rect safeArea = Screen.safeArea;
            previousArea = safeArea;
            previousSize = new Vector2Int(Screen.width, Screen.height);
            Vector2 minAnchor = safeArea.position;
            Vector2 maxAnchor = minAnchor + safeArea.size;
            
            minAnchor.x /= Screen.width;
            minAnchor.y /= Screen.height;
            maxAnchor.x /= Screen.width;
            maxAnchor.y /= Screen.height;

            rectTransform.anchorMin = minAnchor;
            rectTransform.anchorMax = maxAnchor;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
        }
    }
}
