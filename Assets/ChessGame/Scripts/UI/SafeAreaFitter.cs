using UnityEngine;

namespace Chess.UI
{
    /// <summary>
    /// Keeps a rect inside the device safe area so HUD controls never sit under a notch or a
    /// home indicator. Re-evaluated whenever the resolution or orientation changes.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class SafeAreaFitter : MonoBehaviour
    {
        private RectTransform _rect;
        private Rect _lastSafeArea;
        private Vector2Int _lastResolution;

        private void Awake()
        {
            _rect = GetComponent<RectTransform>();
            Apply();
        }

        private void Update()
        {
            if (Screen.safeArea != _lastSafeArea ||
                Screen.width != _lastResolution.x ||
                Screen.height != _lastResolution.y)
            {
                Apply();
            }
        }

        private void Apply()
        {
            if (_rect == null) return;

            _lastSafeArea = Screen.safeArea;
            _lastResolution = new Vector2Int(Screen.width, Screen.height);

            if (Screen.width <= 0 || Screen.height <= 0) return;

            Vector2 min = _lastSafeArea.position;
            Vector2 max = min + _lastSafeArea.size;

            min.x /= Screen.width;
            min.y /= Screen.height;
            max.x /= Screen.width;
            max.y /= Screen.height;

            _rect.anchorMin = min;
            _rect.anchorMax = max;
            _rect.offsetMin = Vector2.zero;
            _rect.offsetMax = Vector2.zero;
        }
    }
}
