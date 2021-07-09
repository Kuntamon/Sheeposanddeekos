using UnityEngine;
using System.Collections;

public class UISkillTargetIndicator : MonoBehaviour {
    public GameObject indicator;

    void Update() {
        var player = Player.localPlayer;;
        if (!player) return;

        indicator.SetActive(player.wantedSkill != -1);
        indicator.transform.position = Input.mousePosition;
    }
}
