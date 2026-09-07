using UnityEngine;
using UnityEngine.UI;

namespace StudentGrowth
{
    // Keep the whole settings page visible on portrait, 16:10 and ultrawide displays.
    [RequireComponent(typeof(CanvasScaler))]
    public sealed class SettingsCanvasFitView : MonoBehaviour
    {
        private CanvasScaler scaler;
        private int width, height;
        private void Awake() { scaler = GetComponent<CanvasScaler>(); }
        private void LateUpdate()
        {
            if (width == Screen.width && height == Screen.height) return;
            width = Screen.width; height = Screen.height;
            scaler.matchWidthOrHeight = height > 0 && (float)width / height > scaler.referenceResolution.x / scaler.referenceResolution.y ? 1 : 0;
        }
    }
}
