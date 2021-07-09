using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Serialization;

public class UIHealthMana : MonoBehaviour {
    public Slider healthSlider;
    public Text healthStatus;
    public Slider manaSlider;
    public Text manaStatus;

    void Update() {
        var player = Player.localPlayer;;
        if (!player) return;

        healthSlider.value = player.HealthPercent();
        healthStatus.text = player.health + " / " + player.healthMax;

        manaSlider.value = player.ManaPercent();
        manaStatus.text = player.mana + " / " + player.manaMax;
    }
}
