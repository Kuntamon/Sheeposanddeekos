using UnityEngine;
using UnityEngine.UI;

public class UIMainPanel : MonoBehaviour {
    public Image portrait;

    void Update() {
        var player = Player.localPlayer;;
        if (!player) return;

        portrait.sprite = player.portrait;
    }
}
