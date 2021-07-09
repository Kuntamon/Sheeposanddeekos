using UnityEngine;
using UnityEngine.UI;

public class UIStatus : MonoBehaviour {
    public GameObject panel;

    void Update() {
        // local player joined the world? then done loading
        if (Player.localPlayer != null)
            panel.SetActive(false);
    }

    public void Show(string message) {
        panel.SetActive(true);
        panel.GetComponentInChildren<Text>().text = message;
    }
}