using UnityEngine;
using UnityEngine.UI;

public class UIShortcuts : MonoBehaviour {
    public Button quitButton;

    void Update() {
        var player = Player.localPlayer;;
        if (!player) return;

        quitButton.onClick.SetListener(() => {
            NetworkManagerMOBA.Quit();
        });
    }
}
