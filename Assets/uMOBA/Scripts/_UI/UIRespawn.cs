// Note: this script has to be on an always-active UI parent, so that we can
// always find it from other code. (GameObject.Find doesn't find inactive ones)
using UnityEngine;
using UnityEngine.UI;
using Mirror;

public class UIRespawn : MonoBehaviour {
    public GameObject panel;
    public Text text;

    void Update() {
        var player = Player.localPlayer;;
        if (!player) return;

        // player dead or alive?
        if (player.health == 0) {
            panel.SetActive(true);

            // calculate the respawn time remaining for the client
            double respawnAt = player.respawnTimeEnd;
            double remaining = respawnAt - NetworkTime.time;
            text.text = remaining.ToString("F0");
        } else {
            // was active before? then we just respawned. focus cam on player.
            if (panel.activeSelf)
                Camera.main.GetComponent<CameraScrolling>().FocusOn(player.transform.position);
            panel.SetActive(false);
        }
    }
}
