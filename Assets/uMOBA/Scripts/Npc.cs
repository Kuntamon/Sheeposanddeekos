// The Npc class is rather simple. It contains state Update functions that do
// nothing at the moment, because Npcs are supposed to stand around all day.
//
// Npcs first show the welcome text and then have options for item trading and
// quests.
using UnityEngine;
using Mirror;

public class Npc : Entity {
    [Header("Items for Sale")]
    public ItemTemplate[] saleItems;

    // networkbehaviour ////////////////////////////////////////////////////////
    public override void OnStartServer() {
        base.OnStartServer();

        // all npcs should spawn with full health and mana
        health = healthMax;
        mana = manaMax;
    }

    // finite state machine states /////////////////////////////////////////////
    [Server] protected override string UpdateServer() { return state; }
    [Client] protected override void UpdateClient() {}

    // skills //////////////////////////////////////////////////////////////////
    public override bool CanAttack(Entity entity) { return false; }
}
