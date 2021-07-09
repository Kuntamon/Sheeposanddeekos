using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Serialization;

public class UICharacterInfo : MonoBehaviour {
    public Text damageText;
    public Text defenseText;
    public Text speedText;

    void Update() {
        var player = Player.localPlayer;
        if (!player) return;

        // show all stats like base(+bonus)
        damageText.text = player.damage.ToString();
        defenseText.text = player.defense.ToString();
        speedText.text = player.speed.ToString();
    }
}
