// HealthBar via TextMesh of "_". This is a workaround for the following Unity
// bug: https://issuetracker.unity3d.com/issues/destroy-gameobject-time-causes-unity-to-crash-in-sharedgfxbuffer-unshare-sharedgfxbuffer-star
using UnityEngine;
using Mirror;

public class TextMeshHealthBar : MonoBehaviour {
    public Color teamColor = Color.green;
    public Color enemyColor = Color.red;
    public Color backgroundColor = Color.black;

    public Entity self; // own entity component, probably in parents

    string GenerateHealthString(int n, Color color) {
        return "<color=#" + ColorUtility.ToHtmlStringRGBA(color) + ">" +
               new string('_', n) +
               "</color>" +
               "<color=#" + ColorUtility.ToHtmlStringRGBA(backgroundColor) + ">" +
               new string('_', 10-n) +
               "</color>";
    }

    void Update() {
        // find local player
        var local = ClientScene.localPlayer;
        if (!local) return;

        // set color based on same team or not
        var color = (local.GetComponent<Entity>().team == self.team ? teamColor : enemyColor);

        // draw health as _ _ _
        // -> one _ per 10% in health color, the rest in black as background
        // -> "" while dead, looks best
        if (self.health > 0) {
            int n = Mathf.RoundToInt(self.HealthPercent() * 10);
            GetComponent<TextMesh>().text = GenerateHealthString(n, color);
        } else GetComponent<TextMesh>().text = "";
    }
}
