using UnityEngine;

public class UISkills : MonoBehaviour {
    public UISkillSlot slotPrefab;
    public Transform content;

    // helper function when client clicks on a skill or presses the hotkey
    void OnSkillClicked(Player player, int skillIndex) {
        // learned and ready?
        if (player.skills[skillIndex].learned &&
            player.skills[skillIndex].IsReady()) {
            // set skill wanted so that the skill target indicator starts to show
            // (if required)
            if (player.skills[skillIndex].showSelector)
                player.wantedSkill = skillIndex;
            else
                player.CmdUseSkill(skillIndex);
        }
    }

    void Update() {
        var player = Player.localPlayer;;
        if (!player) return;

        // instantiate/destroy enough slots (except normal attack)
        UIUtils.BalancePrefabs(slotPrefab.gameObject, player.skills.Count-1, content);

        // refresh all (except normal attack)
        for (int i = 1; i < player.skills.Count; ++i) {
            var slot = content.GetChild(i-1).GetComponent<UISkillSlot>();
            var skill = player.skills[i];

            // overlay hotkey (without 'Alpha' etc.)
            slot.hotKeyText.text = player.skillHotkeys[i].ToString().Replace("Alpha", "");

            // click event (done more than once but w/e)
            int icopy = i;
            slot.button.interactable = skill.learned;
            slot.button.onClick.SetListener(() => {
                OnSkillClicked(player, icopy);
            });

            // hotkey pressed and not typing in any input right now?
            if (Input.GetKeyDown(player.skillHotkeys[i]) && !UIUtils.AnyInputActive())
                OnSkillClicked(player, i);

            // tooltip
            slot.tooltip.text = skill.ToolTip();

            // image
            slot.image.sprite = skill.image;
            slot.image.color = skill.learned ? Color.white : Color.gray;

            // -> learnable?
            if (player.CanLearnSkill(skill)) {
                slot.learnButton.gameObject.SetActive(true);
                slot.learnButton.onClick.SetListener(() => { player.CmdLearnSkill(icopy); });
            // -> upgradeable?
            } else if (player.CanUpgradeSkill(skill)) {
                slot.learnButton.gameObject.SetActive(true);
                slot.learnButton.onClick.SetListener(() => { player.CmdUpgradeSkill(icopy); });
            // -> otherwise no button needed
            } else slot.learnButton.gameObject.SetActive(false);

            // cooldown overlay
            float cd = skill.CooldownRemaining();
            slot.cooldownOverlay.SetActive(skill.learned && cd > 0);
            slot.cooldownText.text = cd.ToString("F0");
        }
    }
}
