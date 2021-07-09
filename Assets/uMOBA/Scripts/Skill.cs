// The Skill struct only contains the dynamic skill properties and a name, so
// that the static properties can be read from the scriptable object. The
// benefits are low bandwidth and easy editing in the Inspector.
//
// Skills have to be structs in order to work with SyncLists.
//
// We implemented the cooldowns in a non-traditional way. Instead of counting
// and increasing the elapsed time since the last cast, we simply set the
// 'end' Time variable to Time.time + cooldown after casting each time. This
// way we don't need an extra Update method that increases the elapsed time for
// each skill all the time.
using System.Text;
using UnityEngine;
using Mirror;

[System.Serializable]
public struct Skill {
    // name used to reference the database entry (cant save template directly
    // because synclist only support simple types)
    public string name;

    // dynamic stats (cooldowns etc.)
    public bool learned;
    public int level;
    public double castTimeEnd; // server time. double for long term precision.
    public double cooldownEnd; // server time. double for long term precision.

    // constructors
    public Skill(SkillTemplate template) {
        name = template.name;

        // learned only if learned by default
        learned = template.learnDefault;
        level = 1;

        // ready immediately
        castTimeEnd = cooldownEnd = NetworkTime.time;
    }

    // does the template still exist?
    public bool TemplateExists() { return SkillTemplate.dict.ContainsKey(name); }

    // template property wrappers for easier access
    public SkillTemplate template { get { return SkillTemplate.dict[name]; } }
    public float castTime { get { return template.castTime.Get(level); } }
    public float cooldown { get { return template.cooldown.Get(level); } }
    public float castRange { get { return template.castRange.Get(level); } }
    public int manaCosts { get { return template.manaCosts.Get(level); } }
    public bool followupDefaultAttack { get { return template.followupDefaultAttack; } }
    public Sprite image { get { return template.image; } }
    public bool learnDefault { get { return template.learnDefault; } }
    public bool cancelCastIfTargetDied { get { return template.cancelCastIfTargetDied; } }
    public bool showSelector { get { return template.showSelector; } }
    public int maxLevel { get { return template.maxLevel; } }
    public int requiredLevel { get { return template.requiredLevel.Get(1); } }
    public int upgradeRequiredLevel { get { return template.requiredLevel.Get(level+1); } }

    // events
    public bool CheckTarget(Entity caster) { return template.CheckTarget(caster); }
    public bool CheckDistance(Entity caster, out Vector3 destination) { return template.CheckDistance(caster, level, out destination); }
    public void Apply(Entity caster) { template.Apply(caster, level); }

    // tooltip - dynamic part
    public string ToolTip() {
        // we use a StringBuilder so that addons can modify tooltips later too
        // ('string' itself can't be passed as a mutable object)
        StringBuilder tip = new StringBuilder(template.ToolTip(level, !learned));

        // upgrade? (don't show if the skill wasn't even learned yet)
        if (learned && level < maxLevel)
            tip.Append("\n<b><i>Upgrade Required Level: " + upgradeRequiredLevel + "</i></b>\n");

        return tip.ToString();
    }

    public float CastTimeRemaining() {
        // how much time remaining until the casttime ends? (using server time)
        return NetworkTime.time >= castTimeEnd ? 0 :(float)(castTimeEnd - NetworkTime.time);
    }

    public bool IsCasting() {
        // we are casting a skill if the casttime remaining is > 0
        return CastTimeRemaining() > 0;
    }

    public float CooldownRemaining() {
        // how much time remaining until the cooldown ends? (using server time)
        return NetworkTime.time >= cooldownEnd ? 0 : (float)(cooldownEnd - NetworkTime.time);
    }

    public bool IsReady() {
        return CooldownRemaining() == 0;
    }
}

public class SyncListSkill : SyncList<Skill> { }
