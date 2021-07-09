using UnityEngine;
using Mirror;

[CreateAssetMenu(menuName="uMOBA Skill/Target Buff", order=999)]
public class TargetBuffSkillTemplate : BuffSkillTemplate {
    public bool canBuffSelf = true;
    public bool canBuffTeam = true;
    public bool canBuffEnemies = false;

    // helper function to determine the target that the skill will be cast on
    // (e.g. cast on self if targeting a monster that can't be buffed)
    Entity CorrectedTarget(Entity caster) {
        // targeting nothing? then try to cast on self
        if (caster.target == null)
            return canBuffSelf ? caster : null;

        // targeting self?
        if (caster.target == caster)
            return canBuffSelf ? caster : null;

        // targeting someone of same type?
        if (caster.target.GetType() == caster.GetType()) {
            bool sameTeam = caster.target.team == caster.team;

            // self / team / enemy?
            if (canBuffTeam && sameTeam)
                return caster.target;
            else if (canBuffEnemies && !sameTeam)
                return caster.target;
            else if (canBuffSelf)
                return caster;
            else
                return null;
        }

        // no valid target? try to cast on self or don't cast at all
        return canBuffSelf ? caster : null;
    }

    public override bool CheckTarget(Entity caster) {
        // correct the target
        caster.target = CorrectedTarget(caster);

        // can only buff the target if it's not dead
        return caster.target != null && caster.target.health > 0;
    }

    // (has corrected target already)
    public override bool CheckDistance(Entity caster, int skillLevel, out Vector3 destination) {
        // target still around?
        if (caster.target != null) {
            destination = caster.target.collider.ClosestPointOnBounds(caster.transform.position);
            return Utils.ClosestDistance(caster.collider, caster.target.collider) <= castRange.Get(skillLevel);
        }
        destination = caster.transform.position;
        return false;
    }

    // (has corrected target already)
    public override void Apply(Entity caster, int skillLevel) {
        // note: caster already has the corrected target because we returned it in StartCast
        // can't buff dead people
        if (caster.target != null && caster.target.health > 0) {
            // add buff or replace if already in there
            caster.target.AddOrRefreshBuff(new Buff(this, skillLevel));

            // show effect on target
            SpawnEffect(caster, caster.target);
        }
    }
}
