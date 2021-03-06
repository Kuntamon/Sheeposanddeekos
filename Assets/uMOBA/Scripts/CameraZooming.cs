// Zooms in with the mouse wheel.
using UnityEngine;
using UnityEngine.EventSystems;

public class CameraZooming : MonoBehaviour {
    public float yMin = 5;
    public float yMax = 12;
    public float speed = 2;

    void LateUpdate () {
        // only if window is focused and if not in a UI right now
        // note: this only works if the UI's CanvasGroup blocks Raycasts
        if (DetectFocus.focused && !Utils.IsCursorOverUserInterface()) {
            // we use a custom getaxis function to get same scroll speed on all
            // platforms
            float scroll = Utils.GetAxisRawScrollUniversal();
            float step = scroll * speed;

            // can only zoom if still in allowed range
            if (step < 0 && transform.position.y < yMax ||
                step > 0 && transform.position.y > yMin)
                transform.position += transform.rotation * Vector3.forward * step;
        }
    }
}
