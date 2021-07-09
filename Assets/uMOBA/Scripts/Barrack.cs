// The Barrack entity type.
using UnityEngine;
using Mirror;

public class Barrack : Entity {
    [Header("Reward")]
    public long rewardExperience = 40;
    public int rewardGold = 40;

    // networkbehaviour ////////////////////////////////////////////////////////
    public override void OnStartServer() {
        base.OnStartServer();

        // all barracks should spawn with full health and mana
        health = healthMax;
        mana = manaMax;
    }

    // finite state machine events /////////////////////////////////////////////
    bool EventDied() {
        return health == 0;
    }

    // finite state machine - server ///////////////////////////////////////////
    [Server]
    string UpdateServer_IDLE() {
        // events sorted by priority (e.g. target doesn't matter if we died)
        if (EventDied()) {
            // we died.
            OnDeath();
            return "DEAD";
        }

        return "IDLE"; // nothing interesting happened
    }

    [Server]
    string UpdateServer_DEAD() {
        // events sorted by priority (e.g. target doesn't matter if we died)
        if (EventDied()) {} // don't care
        return "DEAD";
    }

    [Server]
    protected override string UpdateServer() {
        if (state == "IDLE")    return UpdateServer_IDLE();
        if (state == "DEAD")    return UpdateServer_DEAD();
        Debug.LogError("invalid state:" + state);
        return "IDLE";
    }

    // finite state machine - client ///////////////////////////////////////////
    [Client]
    protected override void UpdateClient() {}

    [Server]
    void OnDeath() {
        // disappear forever
        NetworkServer.Destroy(gameObject);
    }

    // skills //////////////////////////////////////////////////////////////////
    public override bool CanAttack(Entity entity) { return false; }
}
