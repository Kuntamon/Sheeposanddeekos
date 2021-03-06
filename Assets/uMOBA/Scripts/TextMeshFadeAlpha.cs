// Fade the text mesh color's alpha value over time.
using UnityEngine;

[RequireComponent(typeof(TextMesh))]
public class TextMeshFadeAlpha : MonoBehaviour {
    public float delay = 0;
    public float duration = 1;
    float perSecond = 0;
    float startTime;

    void Start() {
        // calculate by how much to fade per second
        perSecond = GetComponent<TextMesh>().color.a / duration;

        // calculate start time
        startTime = Time.time + delay;
    }

    void Update() {
        if (Time.time >= startTime) {
            // fade all text meshes (in children too in case of shadows etc.)
            var col = GetComponent<TextMesh>().color;
            col.a -= perSecond * Time.deltaTime;
            GetComponent<TextMesh>().color = col;
        }
    }
}
