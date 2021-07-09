// The Base entity type, used for buildings that can be destroyed.
using UnityEngine;
using Mirror;

[RequireComponent(typeof(Animator))]
public class Base : Entity {
    [Header("Death")]
    public GameObject deathEffect;

    // networkbehaviour ////////////////////////////////////////////////////////
    public override void OnStartServer() {
        base.OnStartServer();

        // all buildings should spawn with full health and mana
        health = healthMax;
        mana = manaMax;
    }

    [ClientCallback] // no need to do animations on the server
    void LateUpdate() {
        // pass parameters to animation state machine
        // => passing the states directly is the most reliable way to avoid all
        //    kinds of glitches like movement sliding, attack twitching, etc.
        animator.SetBool("DEAD", state == "DEAD");
    }

    // finite state machine states /////////////////////////////////////////////
    [Server] protected override string UpdateServer() {
        return health > 0 ? "IDLE" : "DEAD";
    }
    [Client] protected override void UpdateClient() {
        // did we just die? then load the death effect once
        if (health == 0 && deathEffect) {
            Instantiate(deathEffect, transform.position, Quaternion.identity);
            deathEffect = null;
        }
    }

    // skills //////////////////////////////////////////////////////////////////
    public override bool CanAttack(Entity entity) { return false; }
}
