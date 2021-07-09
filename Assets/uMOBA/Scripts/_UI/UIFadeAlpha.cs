using UnityEngine;
using UnityEngine.UI;

public class UIFadeAlpha : MonoBehaviour {
    public float alpha = 0;
    public float delay = 0;
    public float duration = 1;

    void Start() {
        ShowAndFade();
    }

    // put it into a function so we can call it later again too
    public void ShowAndFade() {
        // reset to original alpha first so that we can use this function
        // multiple times
        foreach (var g in GetComponents<Graphic>())
            g.CrossFadeAlpha(1, 0, true);

        // fade after delay (cancel existing attempts in case we call this
        // function twice in a short time)
        CancelInvoke("OnFade");
        Invoke("OnFade", delay);
    }

    void OnFade() {
        // fade all graphic components (text, shadow, outline, ...)
        foreach (var g in GetComponents<Graphic>())
            g.CrossFadeAlpha(alpha, duration, true);
    }
}
