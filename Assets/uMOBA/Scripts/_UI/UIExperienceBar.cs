using UnityEngine;
using UnityEngine.UI;

public class UIExperienceBar : MonoBehaviour {
    public Slider slider;
    public Text statusText;

    void Update() {
        var player = Player.localPlayer;;
        if (!player) return;

        slider.value = player.ExperiencePercent();
        statusText.text = "Lv." + player.level;
    }
}