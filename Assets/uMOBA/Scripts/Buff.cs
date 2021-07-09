// Buffs are like Skills but for the Buffs list.
using System.Text;
using UnityEngine;
using Mirror;

[System.Serializable]
public partial struct Buff {
    // name used to reference the database entry (cant save template directly
    // because synclist only support simple types)
    public string name;

    // dynamic stats (cooldowns etc.)
    public int level;
    public double buffTimeEnd; // server time. double for long term precision.

    // constructors
    public Buff(BuffSkillTemplate template, int level) {
        name = template.name;
        this.level = level;
        buffTimeEnd = NetworkTime.time + template.buffTime.Get(level); // start buff immediately
    }

    // does the template still exist?
    public bool TemplateExists() { return SkillTemplate.dict.ContainsKey(name); }

    // template property wrappers for easier access
    public BuffSkillTemplate template { get { return (BuffSkillTemplate)SkillTemplate.dict[name]; } }
    public Sprite image { get { return template.image; } }
    public float buffTime { get { return template.buffTime.Get(level); } }
    public int buffsHealthMax { get { return template.buffsHealthMax.Get(level); } }
    public int buffsManaMax { get { return template.buffsManaMax.Get(level); } }
    public int buffsDamage { get { return template.buffsDamage.Get(level); } }
    public int buffsDefense { get { return template.buffsDefense.Get(level); } }
    public float buffsBlockChance { get { return template.buffsBlockChance.Get(level); } }
    public float buffsCriticalChance { get { return template.buffsCriticalChance.Get(level); } }
    public float buffsHealthPercentPerSecond { get { return template.buffsHealthPercentPerSecond.Get(level); } }
    public float buffsManaPercentPerSecond { get { return template.buffsManaPercentPerSecond.Get(level); } }

    // tooltip - runtime part
    public string ToolTip() {
        // we use a StringBuilder so that addons can modify tooltips later too
        // ('string' itself can't be passed as a mutable object)
        StringBuilder tip = new StringBuilder(template.ToolTip(level));

        return tip.ToString();
    }

    public float BuffTimeRemaining() {
        // how much time remaining until the buff ends? (using server time)
        return NetworkTime.time >= buffTimeEnd ? 0 : (float)(buffTimeEnd - NetworkTime.time);
    }
}

public class SyncListBuff : SyncList<Buff> { }
