// Group heal that heals all entities of same type in cast range
// => player heals players in cast range
// => monster heals monsters in cast range
using UnityEngine;
using Mirror;

[CreateAssetMenu(menuName="uMOBA Skill/Area Heal", order=999)]
public class AreaHealSkillTemplate : HealSkillTemplate {
    // note: no 'canHealTeam' and 'canHealEnemies' because area heals are always
    // for the team - no point in healing enemies.

    public override bool CheckTarget(Entity caster) {
        // no target necessary
        return true;
    }

    public override bool CheckDistance(Entity caster, int skillLevel, out Vector3 destination) {
        // can cast anywhere
        destination = caster.transform.position;
        return true;
    }

    public override void Apply(Entity caster, int skillLevel) {
        // find all entities of same type in castRange around the caster
        Collider[] colliders = Physics.OverlapSphere(caster.transform.position, castRange.Get(skillLevel));
        foreach (Collider co in colliders) {
            Entity candidate = co.GetComponentInParent<Entity>();
            if (candidate != null && candidate.GetType() == caster.GetType()) {
                // check team
                if (candidate.team == caster.team) {
                    // can't heal dead people
                    if (candidate.health > 0) {
                        candidate.health += healsHealth.Get(skillLevel);
                        candidate.mana += healsMana.Get(skillLevel);

                        // show effect on candidate
                        SpawnEffect(caster, candidate);
                    }
                }
            }
        }
    }
}
