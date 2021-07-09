// All player logic was put into this class. We could also split it into several
// smaller components, but this would result in many GetComponent calls and a
// more complex syntax.
//
// The default Player class takes care of the basic player logic like the state
// machine and some properties like damage and defense.
//
// The Player class stores the maximum experience for each level in a simple
// array. So the maximum experience for level 1 can be found in expMax[0] and
// the maximum experience for level 2 can be found in expMax[1] and so on. The
// player's health and mana are also level dependent in most MMORPGs, hence why
// there are hpMax and mpMax arrays too. We can find out a players's max health
// in level 1 by using hpMax[0] and so on.
//
// The class also takes care of selection handling, which detects 3D world
// clicks and then targets/navigates somewhere/interacts with someone.
//
// Animations are not handled by the NetworkAnimator because it's still very
// buggy and because it can't really react to movement stops fast enough, which
// results in moonwalking. Not synchronizing animations over the network will
// also save us bandwidth.
//
// Note: unimportant commands should use the Unreliable channel to reduce load.
// (it doesn't matter if a player has to click something twice under heavy load)
using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using Mirror;

[RequireComponent(typeof(NetworkName))]
[RequireComponent(typeof(Chat))]
[RequireComponent(typeof(Animator))]
public class Player : Entity {
    // localPlayer singleton for easier access from UI scripts etc.
    public static Player localPlayer;

    [Header("Components")]
    public Chat chat;

    // health
    public override int healthMax {
        get {
            // calculate item bonus
            int itemBonus = (from item in inventory
                             where item.valid
                             select item.equipHealthBonus).Sum();

            // base (health + buff) + items
            return base.healthMax + itemBonus;
        }
    }

    // mana
    public override int manaMax {
        get {
            // calculate item bonus
            int itemBonus = (from item in inventory
                             where item.valid
                             select item.equipManaBonus).Sum();

            // base (mana + buff) + items
            return base.manaMax + itemBonus;
        }
    }

    // damage
    public override int damage {
        get {
            // calculate item bonus
            int itemBonus = (from item in inventory
                             where item.valid
                             select item.equipDamageBonus).Sum();

            // base (damage + buff) + items
            return base.damage + itemBonus;
        }
    }

    // defense
    public override int defense {
        get {
            // calculate item bonus
            int itemBonus = (from item in inventory
                             where item.valid
                             select item.equipDefenseBonus).Sum();

            // base (defense + buff) + items
            return base.defense + itemBonus;
        }
    }

    // block
    public override float blockChance {
        get {
            // calculate item bonus
            float itemBonus = (from item in inventory
                               where item.valid
                               select item.equipBlockChanceBonus).Sum();

            // base (block + buff) + items
            return base.blockChance + itemBonus;
        }
    }

    // crit
    public override float criticalChance {
        get {
            // calculate item bonus
            float itemBonus = (from item in inventory
                               where item.valid
                               select item.equipCriticalChanceBonus).Sum();

            // base (critical + buff) + items
            return base.criticalChance + itemBonus;
        }
    }

    [Header("Experience")] // note: int is not enough (can have > 2 mil. easily)
    public int maxLevel = 1;
    [SyncVar, SerializeField] long _experience = 0;
    public long experience {
        get { return _experience; }
        set {
            if (value <= _experience) {
                // decrease
                _experience = Math.Max(value, 0);
            } else {
                // increase with level ups
                // set the new value (which might be more than expMax)
                _experience = value;

                // now see if we leveled up (possibly more than once too)
                // (can't level up if already max level)
                while (_experience >= experienceMax && level < maxLevel) {
                    // subtract current level's required exp, then level up
                    _experience -= experienceMax;
                    ++level;
                }

                // set to expMax if there is still too much exp remaining
                if (_experience > experienceMax) _experience = experienceMax;
            }
        }
    }
    [SerializeField] protected LevelBasedLong _experienceMax = new LevelBasedLong{baseValue=10, bonusPerLevel=10};
    public long experienceMax { get { return _experienceMax.Get(level); } }

    [Header("Indicator")]
    public GameObject indicatorPrefab;
    GameObject indicator;

    [Header("Inventory")]
    public int inventorySize = 30;
    public SyncListItem inventory = new SyncListItem();
    public ItemTemplate[] defaultItems;
    public KeyCode[] inventoryHotkeys = new KeyCode[] {KeyCode.Alpha1, KeyCode.Alpha2, KeyCode.Alpha3, KeyCode.Alpha4, KeyCode.Alpha5, KeyCode.Alpha6};
    public KeyCode[] inventorySplitKeys = {KeyCode.LeftShift, KeyCode.RightShift};

    [Header("Gold")] // note: int is not enough (can have > 2 mil. easily)
    [SerializeField, SyncVar] long _gold = 0;
    public long gold { get { return _gold; } set { _gold = Math.Max(value, 0); } }

    // 'skillTemplates' holds all skillTemplates and can be modified in the
    // Inspector 'skills' holds the dynamic skills that were based on the
    // skillTemplates (with cooldown, learned, etc.)
    // -> 1 default attack and 4 learnable skills
    [Header("Skillbar")]
    public KeyCode[] skillHotkeys = new KeyCode[] {KeyCode.Q, KeyCode.W, KeyCode.E, KeyCode.R};

    [Header("Interaction")]
    public float interactionRange = 4;
    public KeyCode focusKey = KeyCode.Space;
    public bool localPlayerClickThrough = true; // click selection goes through localplayer. feels best.

    [Header("Popups")]
    public GameObject goldPopupPrefab;

    [Header("Respawn")]
    public float deathTime = 10; // enough for animation
    double deathTimeEnd; // server time. double for long term precision.
    public float respawnTime = 10;
    [SyncVar, HideInInspector] public double respawnTimeEnd; // syncvar for UI
    public float deathExperienceLossPercent = 0.05f;
    public float deathGoldLossPercent = 0.3f;

    [Header("Other")]
    public Sprite portrait;

    // the next skill to be set if we try to set it while casting
    int nextSkill = -1;

    // the next target to be set if we try to set it while casting
    Entity nextTarget;

    // the selected skill that is pending while the player selects a target
    [HideInInspector] public int wantedSkill = -1; // client sided

    // networkbehaviour ////////////////////////////////////////////////////////
    protected override void Awake() {
        // cache base components
        base.Awake();
    }

    public override void OnStartLocalPlayer() {
        // set singleton
        localPlayer = this;

        // setup camera target
        Camera.main.GetComponent<CameraScrolling>().FocusOn(transform.position);
    }

    public override void OnStartServer() {
        base.OnStartServer();

        // all players should spawn with full health and mana
        health = healthMax;
        mana = manaMax;

        // load inventory
        for (int i = 0; i < inventorySize; ++i)
            if (i < defaultItems.Length)
                inventory.Add(new Item(defaultItems[i]));
            else
                inventory.Add(new Item());
    }

    [ClientCallback] // no need to do animations on the server
    void LateUpdate() {
        // pass parameters to animation state machine
        // => passing the states directly is the most reliable way to avoid all
        //    kinds of glitches like movement sliding, attack twitching, etc.
        // => only play moving animation while the agent is actually moving. the
        //    MOVING state might be delayed to due latency or we might be in
        //    MOVING while a path is still pending, etc.
        // => skill names are assumed to be boolean parameters in animator
        //    so we don't need to worry about an animation number etc.
        animator.SetBool("MOVING", state == "MOVING" && agent.velocity != Vector3.zero);
        animator.SetBool("CASTING", state == "CASTING");
        animator.SetBool("DEAD", state == "DEAD");
        foreach (Skill skill in skills)
            if (skill.learned)
                animator.SetBool(skill.name, skill.CastTimeRemaining() > 0);
    }

    // note: this function isn't called if it has a [ClientCallback] tag, so we
    //       have to use isLocalPlayer instead
    void OnDestroy() {
        if (isLocalPlayer) // requires at least Unity 5.5.1 bugfix to work
            Destroy(indicator);
    }

    // finite state machine events - status based //////////////////////////////
    // status based events
    bool EventDied() {
        return health == 0;
    }

    bool EventDeathTimeElapsed() {
        return state == "DEAD" && NetworkTime.time >= deathTimeEnd;
    }

    bool EventRespawnTimeElapsed() {
        return state == "DEAD" && NetworkTime.time >= respawnTimeEnd;
    }

    bool EventTargetDisappeared() {
        return target == null;
    }

    bool EventTargetDied() {
        return target != null && target.health == 0;
    }

    bool EventSkillRequest() {
        return 0 <= currentSkill && currentSkill < skills.Count;
    }

    bool EventSkillFinished() {
        return 0 <= currentSkill && currentSkill < skills.Count &&
               skills[currentSkill].CastTimeRemaining() == 0;
    }

    bool EventMoveEnd() {
        return state == "MOVING" && !IsMoving();
    }

    // finite state machine events - command based /////////////////////////////
    // client calls command, command sets a flag, event reads and resets it
    // => we use a set so that we don't get ultra long queues etc.
    // => we use set.Return to read and clear values
    HashSet<string> cmdEvents = new HashSet<string>();

    [Command]
    public void CmdCancelAction() { cmdEvents.Add("CancelAction"); }
    bool EventCancelAction() { return cmdEvents.Remove("CancelAction"); }

    Vector3 navigatePos = Vector3.zero;
    float navigateStop = 0;
    [Command]
    public void CmdNavigateTo(Vector3 position, float stoppingDistance) {
        navigatePos = position; navigateStop = stoppingDistance;
        cmdEvents.Add("NavigateTo");
    }
    bool EventNavigateTo() { return cmdEvents.Remove("NavigateTo"); }

    // finite state machine - server ///////////////////////////////////////////
    [Server]
    string UpdateServer_IDLE() {
        // events sorted by priority (e.g. target doesn't matter if we died)
        if (EventDied()) {
            // we died.
            OnDeath();
            currentSkill = nextSkill = -1; // in case we died while trying to cast
            return "DEAD";
        }
        if (EventCancelAction()) {
            // the only thing that we can cancel is the target
            target = null;
            return "IDLE";
        }
        if (EventNavigateTo()) {
            // cancel casting (if any) and start moving
            currentSkill = nextSkill = -1;
            // move
            agent.stoppingDistance = navigateStop;
            agent.destination = navigatePos;
            return "MOVING";
        }
        if (EventSkillRequest()) {
            // user wants to cast a skill.
            // check self (alive, mana, weapon etc.) and target
            var skill = skills[currentSkill];
            nextTarget = target; // return to this one after any corrections by CastCheckTarget
            if (CastCheckSelf(skill) && CastCheckTarget(skill)) {
                // check distance between self and target
                Vector3 destination;
                if (CastCheckDistance(skill, out destination)) {
                    // start casting and set the casting end time
                    skill.castTimeEnd = NetworkTime.time + skill.castTime;
                    skills[currentSkill] = skill;
                    return "CASTING";
                } else {
                    // move to the target first
                    // (use collider point(s) to also work with big entities)
                    agent.stoppingDistance = skill.castRange;
                    agent.destination = destination;
                    return "MOVING";
                }
            } else {
                // checks failed. stop trying to cast.
                currentSkill = nextSkill = -1;
                return "IDLE";
            }
        }
        if (EventSkillFinished()) {} // don't care
        if (EventMoveEnd()) {} // don't care
        if (EventDeathTimeElapsed()) {} // don't care
        if (EventRespawnTimeElapsed()) {} // don't care
        if (EventTargetDied()) {} // don't care
        if (EventTargetDisappeared()) {} // don't care

        return "IDLE"; // nothing interesting happened
    }

    [Server]
    string UpdateServer_MOVING() {
        // events sorted by priority (e.g. target doesn't matter if we died)
        if (EventDied()) {
            // we died.
            OnDeath();
            currentSkill = nextSkill = -1; // in case we died while trying to cast
            return "DEAD";
        }
        if (EventMoveEnd()) {
            // finished moving. do whatever we did before.
            return "IDLE";
        }
        if (EventCancelAction()) {
            // cancel casting (if any) and stop moving
            currentSkill = nextSkill = -1;
            agent.ResetPath();
            return "IDLE";
        }
        if (EventNavigateTo()) {
            // cancel casting (if any) and start moving
            currentSkill = nextSkill = -1;
            agent.stoppingDistance = navigateStop;
            agent.destination = navigatePos;
            return "MOVING";
        }
        if (EventSkillRequest()) {
            // if and where we keep moving depends on the skill and the target
            // check self (alive, mana, weapon etc.) and target
            var skill = skills[currentSkill];
            nextTarget = target; // return to this one after any corrections by CastCheckTarget
            if (CastCheckSelf(skill) && CastCheckTarget(skill)) {
                // check distance between self and target
                Vector3 destination;
                if (CastCheckDistance(skill, out destination)) {
                    // stop moving, start casting and set the casting end time
                    agent.ResetPath();
                    skill.castTimeEnd = NetworkTime.time + skill.castTime;
                    skills[currentSkill] = skill;
                    return "CASTING";
                } else {
                    // keep moving towards the target
                    // (use collider point(s) to also work with big entities)
                    agent.stoppingDistance = skill.castRange;
                    agent.destination = destination;
                    return "MOVING";
                }
            } else {
                // invalid target. stop trying to cast, but keep moving.
                currentSkill = nextSkill = -1;
                return "MOVING";
            }
        }
        if (EventSkillFinished()) {} // don't care
        if (EventDeathTimeElapsed()) {} // don't care
        if (EventRespawnTimeElapsed()) {} // don't care
        if (EventTargetDied()) {} // don't care
        if (EventTargetDisappeared()) {} // don't care

        return "MOVING"; // nothing interesting happened
    }

    void UseNextTargetIfAny() {
        // use next target if the user tried to target another while casting
        // (target is locked while casting so skill isn't applied to an invalid
        //  target accidentally)
        if (nextTarget != null) {
            target = nextTarget;
            nextTarget = null;
        }
    }

    [Server]
    string UpdateServer_CASTING() {
        // keep looking at the target for server & clients (only Y rotation)
        if (target) LookAtY(target.transform.position);

        // events sorted by priority (e.g. target doesn't matter if we died)
        //
        // IMPORTANT: nextTarget might have been set while casting, so make sure
        // to handle it in any case here. it should definitely be null again
        // after casting was finished.
        if (EventDied()) {
            // we died.
            OnDeath();
            currentSkill = nextSkill = -1; // in case we died while trying to cast
            UseNextTargetIfAny(); // if user selected a new target while casting
            return "DEAD";
        }
        if (EventNavigateTo()) {
            // cancel casting and start moving
            currentSkill = nextSkill = -1;
            agent.stoppingDistance = navigateStop;
            agent.destination = navigatePos;
            UseNextTargetIfAny(); // if user selected a new target while casting
            return "MOVING";
        }
        if (EventCancelAction()) {
            // cancel casting
            currentSkill = nextSkill = -1;
            UseNextTargetIfAny(); // if user selected a new target while casting
            return "IDLE";
        }
        if (EventTargetDisappeared()) {
            // cancel if the target matters for this skill
            if (skills[currentSkill].cancelCastIfTargetDied) {
                currentSkill = nextSkill = -1;
                UseNextTargetIfAny(); // if user selected a new target while casting
                return "IDLE";
            }
        }
        if (EventTargetDied()) {
            // cancel if the target matters for this skill
            if (skills[currentSkill].cancelCastIfTargetDied) {
                currentSkill = nextSkill = -1;
                UseNextTargetIfAny(); // if user selected a new target while casting
                return "IDLE";
            }
        }
        if (EventSkillFinished()) {
            // apply the skill after casting is finished
            // note: we don't check the distance again. it's more fun if players
            //       still cast the skill if the target ran a few steps away
            Skill skill = skills[currentSkill];

            // apply the skill on the target
            CastSkill(skill);

            // casting finished for now. user pressed another skill button?
            if (nextSkill != -1) {
                currentSkill = nextSkill;
                nextSkill = -1;
            // skill should be followed with default attack? otherwise clear
            } else currentSkill = skill.followupDefaultAttack ? 0 : -1;

            // use next target if the user tried to target another while casting
            UseNextTargetIfAny();

            // go back to IDLE
            return "IDLE";
        }
        if (EventMoveEnd()) {} // don't care
        if (EventDeathTimeElapsed()) {} // don't care
        if (EventRespawnTimeElapsed()) {} // don't care
        if (EventSkillRequest()) {} // don't care

        return "CASTING"; // nothing interesting happened
    }

    [Server]
    string UpdateServer_DEAD() {
        // events sorted by priority (e.g. target doesn't matter if we died)
        if (EventRespawnTimeElapsed()) {
            // find team's spawn point and go there; restore health; go to idle
            Show(); // Hide was called before, Show again now.
            var spawn = FindObjectsOfType<PlayerSpawn>().Where(g => g.team == team).First();
            agent.Warp(spawn.transform.position); // recommended over transform.position
            Revive();
            return "IDLE";
        }
        if (EventDeathTimeElapsed()) {
            // we were lying around dead for long enough now.
            // hide while respawning, or disappear forever
            Hide();
            return "DEAD";
        }
        if (EventMoveEnd()) {} // don't care
        if (EventSkillFinished()) {} // don't care
        if (EventDied()) {} // don't care
        if (EventCancelAction()) {} // don't care
        if (EventTargetDisappeared()) {} // don't care
        if (EventTargetDied()) {} // don't care
        if (EventNavigateTo()) {} // don't care
        if (EventSkillRequest()) {} // don't care

        return "DEAD"; // nothing interesting happened
    }

    [Server]
    protected override string UpdateServer() {
        if (state == "IDLE")    return UpdateServer_IDLE();
        if (state == "MOVING")  return UpdateServer_MOVING();
        if (state == "CASTING") return UpdateServer_CASTING();
        if (state == "DEAD")    return UpdateServer_DEAD();
        Debug.LogError("invalid state:" + state);
        return "IDLE";
    }

    // finite state machine - client ///////////////////////////////////////////
    [Client]
    protected override void UpdateClient() {
        // pressing/holding space bar makes camera focus on the player
        // (not while typing in chat etc.)
        if (isLocalPlayer) {
            if (Input.GetKey(focusKey) && !UIUtils.AnyInputActive()) {
                // focus on it once, then disable scrolling while holding the
                // button, otherwise camera gets shaky when moving cursor to the
                // edge of the screen
                Camera.main.GetComponent<CameraScrolling>().FocusOn(transform.position);
                Camera.main.GetComponent<CameraScrolling>().enabled = false;
            } else {
                Camera.main.GetComponent<CameraScrolling>().enabled = true;
            }
        }

        if (state == "IDLE" || state == "MOVING") {
            if (isLocalPlayer) {
                // simply accept input
                SelectionHandling();

                // canel action if escape key was pressed, clear wantedSkill
                if (Input.GetKeyDown(KeyCode.Escape)) {
                    wantedSkill = -1;
                    CmdCancelAction();
                }
            }
        } else if (state == "CASTING") {
            // keep looking at the target for server & clients (only Y rotation)
            if (target) LookAtY(target.transform.position);

            if (isLocalPlayer) {
                // simply accept input
                SelectionHandling();

                // canel action if escape key was pressed, clear wantedSkill
                if (Input.GetKeyDown(KeyCode.Escape)) {
                    wantedSkill = -1;
                    CmdCancelAction();
                }
            }
        } else if (state == "DEAD") {

        } else Debug.LogError("invalid state:" + state);
    }

    // combat //////////////////////////////////////////////////////////////////
    // no need to instantiate gold popups on the server
    // -> passing the GameObject and calculating the position on the client
    //    saves server computations and takes less bandwidth (4 instead of 12 byte)
    [TargetRpc]
    public void TargetShowGoldPopup(GameObject goldReceiver, int amount) {
        // spawn the gold popup (if any) and set the text
        if (goldReceiver != null) { // still around?
            Entity receiverEntity = goldReceiver.GetComponent<Entity>();
            if (receiverEntity != null && goldPopupPrefab != null) {
                // showing it above their head looks best, and we don't have to use
                // a custom shader to draw world space UI in front of the entity
                var bounds = receiverEntity.collider.bounds;
                Vector3 position = new Vector3(bounds.center.x, bounds.max.y, bounds.center.z);

                var popup = (GameObject)Instantiate(goldPopupPrefab, position, Quaternion.identity);
                popup.GetComponentInChildren<TextMesh>().text = "+" + amount.ToString();
            }
        }
    }

    // custom DealDamageAt function that also rewards experience if we killed
    // the monster
    [Server]
    public override void DealDamageAt(Entity entity, int amount) {
        // deal damage with the default function. get all entities that were hit
        // in the AoE radius
        base.DealDamageAt(entity, amount);

        // did we kill it?
        if (entity.health == 0) {
            // any exp or gold rewards? (depends on type)
            long deathExperience = 0;
            int deathGold = 0;

            if (entity is Monster) {
                deathExperience = ((Monster)entity).rewardExperience;
                deathGold = ((Monster)entity).rewardGold;
            } else if (entity is Tower) {
                deathExperience = ((Tower)entity).rewardExperience;
                deathGold = ((Tower)entity).rewardGold;
            } else if (entity is Barrack) {
                deathExperience = ((Barrack)entity).rewardExperience;
                deathGold = ((Barrack)entity).rewardGold;
            }

            // gain experience reward
            experience += BalanceExpReward(deathExperience, level, entity.level);

            // gain gold reward
            gold += deathGold;

            // show gold popup in client
            // showing them above their head looks best, and we don't have to
            // use a custom shader to draw world space UI in front of the entity
            // note: we send the RPC to ourselves because whatever we killed
            //       might disappear before the rpc reaches it
            // note: we use a TargetRpc because others don't have to see it
            if (deathGold > 0)
                TargetShowGoldPopup(entity.gameObject, deathGold);
        }
    }

    // experience //////////////////////////////////////////////////////////////
    public float ExperiencePercent() {
        return (experience != 0 && experienceMax != 0) ? (float)experience / (float)experienceMax : 0;
    }

    // players gain exp depending on their level. if a player has a lower level
    // than the monster, then he gains more exp (up to 100% more) and if he has
    // a higher level, then he gains less exp (up to 100% less)
    // -> test with monster level 20 and expreward of 100:
    //   BalanceExpReward( 1, 20, 100)); => 200
    //   BalanceExpReward( 9, 20, 100)); => 200
    //   BalanceExpReward(10, 20, 100)); => 200
    //   BalanceExpReward(11, 20, 100)); => 190
    //   BalanceExpReward(12, 20, 100)); => 180
    //   BalanceExpReward(13, 20, 100)); => 170
    //   BalanceExpReward(14, 20, 100)); => 160
    //   BalanceExpReward(15, 20, 100)); => 150
    //   BalanceExpReward(16, 20, 100)); => 140
    //   BalanceExpReward(17, 20, 100)); => 130
    //   BalanceExpReward(18, 20, 100)); => 120
    //   BalanceExpReward(19, 20, 100)); => 110
    //   BalanceExpReward(20, 20, 100)); => 100
    //   BalanceExpReward(21, 20, 100)); =>  90
    //   BalanceExpReward(22, 20, 100)); =>  80
    //   BalanceExpReward(23, 20, 100)); =>  70
    //   BalanceExpReward(24, 20, 100)); =>  60
    //   BalanceExpReward(25, 20, 100)); =>  50
    //   BalanceExpReward(26, 20, 100)); =>  40
    //   BalanceExpReward(27, 20, 100)); =>  30
    //   BalanceExpReward(28, 20, 100)); =>  20
    //   BalanceExpReward(29, 20, 100)); =>  10
    //   BalanceExpReward(30, 20, 100)); =>   0
    //   BalanceExpReward(31, 20, 100)); =>   0
    public static long BalanceExpReward(long reward, int attackerLevel, int victimLevel) {
        int levelDifference = Mathf.Clamp(victimLevel - attackerLevel, -10, 10);
        float multiplier = 1 + levelDifference * 0.1f;
        return Convert.ToInt64(reward * multiplier);
    }

    // death ///////////////////////////////////////////////////////////////////
    [Server]
    void OnDeath() {
        // stop any movement and buffs, clear target
        agent.ResetPath();
        buffs.Clear();
        target = null;

        // lose experience
        long loseExperience = Convert.ToInt64(experienceMax * deathExperienceLossPercent);
        experience -= loseExperience;

        // lose gold
        long loseGold = Convert.ToInt64(gold * deathGoldLossPercent);
        gold -= loseGold;

        // set death and respawn end times. we set both of them now to make sure
        // that everything works fine even if a player isn't updated for a
        // while. so as soon as it's updated again, the death/respawn will
        // happen immediately if current time > end time.
        deathTimeEnd = NetworkTime.time + deathTime;
        respawnTimeEnd = deathTimeEnd + respawnTime; // after death time ended

        // send an info chat message
        string message = "You died and lost " + loseExperience + " experience and " + loseGold + " gold.";
        chat.TargetMsgInfo(message);
    }

    // inventory ///////////////////////////////////////////////////////////////
    public int InventorySlotsFree() {
        return inventory.Count(item => !item.valid);
    }

    // helper function to check if the inventory has space for 'n' items of type
    // -> the easiest solution would be to check for enough free item slots
    // -> it's better to try to add it onto existing stacks of the same type
    //    first though
    // -> it could easily take more than one slot too
    public bool InventoryCanAddAmount(ItemTemplate item, int amount) {
        // go through each slot
        for (int i = 0; i < inventory.Count; ++i) {
            // empty? then subtract maxstack
            if (!inventory[i].valid)
                amount -= item.maxStack;
            // not empty and same type? then subtract free amount (max-amount)
            else if (inventory[i].valid && inventory[i].name == item.name)
                amount -= (inventory[i].maxStack - inventory[i].amount);

            // were we able to fit the whole amount already?
            if (amount <= 0) return true;
        }

        // if we got here than amount was never <= 0
        return false;
    }

    // helper function to put 'n' items of a type into the inventory, while
    // trying to put them onto existing item stacks first
    // -> this is better than always adding items to the first free slot
    // -> function will only add them if there is enough space for all of them
    public bool InventoryAddAmount(ItemTemplate item, int amount) {
        // we only want to add them if there is enough space for all of them, so
        // let's double check
        if (InventoryCanAddAmount(item, amount)) {
            // go through each slot
            for (int i = 0; i < inventory.Count; ++i) {
                // empty? then fill slot with as many as possible
                if (!inventory[i].valid) {
                    int add = Mathf.Min(amount, item.maxStack);
                    inventory[i] = new Item(item, add);
                    amount -= add;
                }
                // not empty and same type? then add free amount (max-amount)
                else if (inventory[i].valid && inventory[i].name == item.name) {
                    int space = inventory[i].maxStack - inventory[i].amount;
                    int add = Mathf.Min(amount, space);
                    var temp = inventory[i];
                    temp.amount += add;
                    inventory[i] = temp;
                    amount -= add;
                }

                // were we able to fit the whole amount already?
                if (amount <= 0) return true;
            }
            // we should have been able to add all of them
            if (amount != 0) Debug.LogError("inventory add failed: " + item.name + " " + amount);
        }
        return false;
    }

    [Command]
    public void CmdSwapInventoryInventory(int fromIndex, int toIndex) {
        // note: should never send a command with complex types!
        // validate: make sure that the slots actually exist in the inventory
        // and that they are not equal
        if ((state == "IDLE" || state == "MOVING" || state == "CASTING") &&
            0 <= fromIndex && fromIndex < inventory.Count &&
            0 <= toIndex && toIndex < inventory.Count &&
            fromIndex != toIndex) {
            // swap them
            var temp = inventory[fromIndex];
            inventory[fromIndex] = inventory[toIndex];
            inventory[toIndex] = temp;
        }
    }

    [Command]
    public void CmdInventorySplit(int fromIndex, int toIndex) {
        // note: should never send a command with complex types!
        // validate: make sure that the slots actually exist in the inventory
        // and that they are not equal
        if ((state == "IDLE" || state == "MOVING" || state == "CASTING") &&
            0 <= fromIndex && fromIndex < inventory.Count &&
            0 <= toIndex && toIndex < inventory.Count &&
            fromIndex != toIndex) {
            // slotFrom has to have an entry, slotTo has to be empty
            if (inventory[fromIndex].valid && !inventory[toIndex].valid) {
                // from entry needs at least amount of 2
                if (inventory[fromIndex].amount >= 2) {
                    // split them serversided (has to work for even and odd)
                    var itemFrom = inventory[fromIndex];
                    var itemTo = inventory[fromIndex]; // copy the value
                    //inventory[toIndex] = inventory[fromIndex]; // copy value type
                    itemTo.amount = itemFrom.amount / 2;
                    itemFrom.amount -= itemTo.amount; // works for odd too

                    // put back into the list
                    inventory[fromIndex] = itemFrom;
                    inventory[toIndex] = itemTo;
                }
            }
        }
    }

    [Command]
    public void CmdInventoryMerge(int fromIndex, int toIndex) {
        if ((state == "IDLE" || state == "MOVING" || state == "CASTING") &&
            0 <= fromIndex && fromIndex < inventory.Count &&
            0 <= toIndex && toIndex < inventory.Count &&
            fromIndex != toIndex) {
            // both items have to be valid
            if (inventory[fromIndex].valid && inventory[toIndex].valid) {
                // make sure that items are the same type
                if (inventory[fromIndex].name == inventory[toIndex].name) {
                    // merge from -> to
                    var itemFrom = inventory[fromIndex];
                    var itemTo = inventory[toIndex];
                    int stack = Mathf.Min(itemFrom.amount + itemTo.amount, itemTo.maxStack);
                    int put = stack - itemFrom.amount;
                    itemFrom.amount = itemTo.amount - put;
                    itemTo.amount = stack;
                    // 'from' empty now? then clear it
                    if (itemFrom.amount == 0) itemFrom.valid = false;
                    // put back into the list
                    inventory[fromIndex] = itemFrom;
                    inventory[toIndex] = itemTo;
                }
            }
        }
    }

    [Command]
    public void CmdUseInventoryItem(int index) {
        // validate
        if ((state == "IDLE" || state == "MOVING" || state == "CASTING") &&
            0 <= index && index < inventory.Count && inventory[index].valid) {
            // what we have to do depends on the category
            //print("use item:" + index);
            var item = inventory[index];
            if (item.category.StartsWith("Potion")) {
                // use
                health += item.usageHealth;
                mana += item.usageMana;

                // decrease amount or destroy
                if (item.usageDestroy) {
                    --item.amount;
                    if (item.amount == 0) item.valid = false;
                    inventory[index] = item; // put new values in there
                }
            }
        }
    }

    // skills //////////////////////////////////////////////////////////////////
    public override bool CanAttack(Entity entity) {
        return health > 0 &&
               entity != this &&
               entity.health > 0 &&
               entity.team != team &&
               (entity is Monster || entity is Player || entity is Tower || entity is Barrack || entity is Base);
    }

    [Command]
    public void CmdUseSkill(int skillIndex) {
        // validate
        if ((state == "IDLE" || state == "MOVING" || state == "CASTING") &&
            0 <= skillIndex && skillIndex < skills.Count) {
            // can the skill be casted?
            if (skills[skillIndex].learned && skills[skillIndex].IsReady()) {
                // add as current or next skill, unless casting same one already
                // (some players might hammer the key multiple times, which
                //  doesn't mean that they want to cast it afterwards again)
                // => also: always set currentSkill when moving or idle or whatever
                //  so that the last skill that the player tried to cast while
                //  moving is the first skill that will be casted when attacking
                //  the enemy.
                if (currentSkill == -1 || state != "CASTING")
                    currentSkill = skillIndex;
                else if (currentSkill != skillIndex)
                    nextSkill = skillIndex;
            }
        }
    }

    public int SkillpointsSpendable() {
        // calculate the amount of skill points that can still be spent
        // -> one point per level
        // -> we don't need to store the points in an extra variable, we can
        //    simply decrease the spent points from the current skills
        // -> and +1 because players should still be able to assign one point in
        //    level 1, even though they did learn Normal Attack automatically
        int spent = skills.Where(s => s.learned).Sum(s => s.level);
        return level - spent + 1;
    }

    // helper function for command and UI
    public bool CanLearnSkill(Skill skill) {
        return !skill.learned &&
               level >= skill.requiredLevel &&
               SkillpointsSpendable() > 0;
    }

    [Command]
    public void CmdLearnSkill(int skillIndex) {
        // validate
        if ((state == "IDLE" || state == "MOVING" || state == "CASTING") &&
            0 <= skillIndex && skillIndex < skills.Count) {
            Skill skill = skills[skillIndex];

            // not learned already? enough skill exp, required level?
            if (CanLearnSkill(skill)) {
                // learn skill
                skill.learned = true;
                skills[skillIndex] = skill;
            }
        }
    }

    // helper function for command and UI
    public bool CanUpgradeSkill(Skill skill) {
        return skill.learned &&
               skill.level < skill.maxLevel &&
               level >= skill.upgradeRequiredLevel &&
               SkillpointsSpendable() > 0;
    }

    [Command]
    public void CmdUpgradeSkill(int skillIndex) {
        // validate
        if ((state == "IDLE" || state == "MOVING" || state == "CASTING") &&
            0 <= skillIndex && skillIndex < skills.Count) {
            Skill skill = skills[skillIndex];

            // already learned and required level for upgrade?
            // and can be upgraded?
            if (CanUpgradeSkill(skill)) {
                // upgrade
                ++skill.level;
                skills[skillIndex] = skill;
            }
        }
    }

    // npc trading /////////////////////////////////////////////////////////////
    [Command]
    public void CmdNpcBuyItem(int index, int amount) {
        // validate: close enough, npc alive and valid index?
        // use collider point(s) to also work with big entities
        if (state == "IDLE" &&
            target != null &&
            target.health > 0 &&
            target is Npc &&
            target.team == team &&
            Utils.ClosestDistance(collider, target.collider) <= interactionRange &&
            0 <= index && index < ((Npc)target).saleItems.Length)
        {
            var npcItem = ((Npc)target).saleItems[index];

            // valid amount?
            if (1 <= amount && amount <= npcItem.maxStack) {
                long price = npcItem.buyPrice * amount;

                // enough gold and enough space in inventory?
                if (gold >= price && InventoryCanAddAmount(npcItem, amount)) {
                    // pay for it, add to inventory
                    gold -= price;
                    InventoryAddAmount(npcItem, amount);
                }
            }
        }
    }

    [Command]
    public void CmdNpcSellItem(int index, int amount) {
        // validate: close enough, npc alive and valid index and valid item?
        // use collider point(s) to also work with big entities
        if (state == "IDLE" &&
            target != null &&
            target.health > 0 &&
            target is Npc &&
            target.team == team &&
            Utils.ClosestDistance(collider, target.collider) <= interactionRange &&
            0 <= index && index < inventory.Count &&
            inventory[index].valid)
        {
            var item = inventory[index];

            // valid amount?
            if (1 <= amount && amount <= item.amount) {
                // sell the amount
                long price = item.sellPrice * amount;
                gold += price;
                item.amount -= amount;
                if (item.amount == 0) item.valid = false;
                inventory[index] = item;
            }
        }
    }

    // selection handling //////////////////////////////////////////////////////
    public void SetIndicatorViaParent(Transform parent) {
        if (!indicator) indicator = Instantiate(indicatorPrefab);
        indicator.transform.SetParent(parent, true);
        indicator.transform.position = parent.position + Vector3.up * 0.01f;
        indicator.transform.up = Vector3.up;
    }

    public void SetIndicatorViaPosition(Vector3 pos, Vector3 normal) {
        if (!indicator) indicator = Instantiate(indicatorPrefab);
        indicator.transform.parent = null;
        indicator.transform.position = pos + Vector3.up * 0.01f;
        indicator.transform.up = normal; // adjust to terrain normal
    }

    [Command]
    void CmdSetTarget(NetworkIdentity ni) {
        // validate
        if (ni != null) {
            // can directly change it, or change it after casting?
            if (state == "IDLE" || state == "MOVING")
                target = ni.GetComponent<Entity>();
            else if (state == "CASTING")
                nextTarget = ni.GetComponent<Entity>();
        }
    }

    [Client]
    void SelectionHandling() {
        bool left = Input.GetMouseButtonDown(0);
        bool right = Input.GetMouseButtonDown(1);

        // mobile: everything via 'left click' aka touch
        if (!Input.mousePresent) right = left;

        if ((left || right) &&
            !Utils.IsCursorOverUserInterface()) {
            var ray = Camera.main.ScreenPointToRay(Input.mousePosition);

            RaycastHit hit;
            if (Physics.Raycast(ray, out hit)) {
                // valid target?
                var entity = hit.transform.GetComponent<Entity>();
                if (entity) {
                    // set indicator
                    SetIndicatorViaParent(hit.transform);

                    // player/monster/tower etc.? => interact
                    if (entity is Player || entity is Monster || entity is Tower || entity is Barrack || entity is Base) {
                        int skillIndex = -1;

                        // trying to cast a skill?
                        if (left && 0 <= wantedSkill && wantedSkill < skills.Count)
                            skillIndex = wantedSkill;
                        // or normal attack?
                        else if (right)
                            skillIndex = 0;

                        if (0 <= skillIndex && skillIndex < skills.Count) {
                            // cast the wanted skill (if ready)
                            if (skills[skillIndex].IsReady()) {
                                CmdSetTarget(entity.netIdentity);
                                CmdUseSkill(skillIndex);
                            // otherwise walk there if still on cooldown etc
                            // use collider point(s) to also work with big entities
                            } else {
                                CmdNavigateTo(entity.collider.ClosestPointOnBounds(transform.position),
                                              skills[skillIndex].castRange);
                            }
                        }
                    // npc & alive => talk
                    } else if (left && entity is Npc && entity.health > 0) {
                        // close enough to talk?
                        // use collider point(s) to also work with big entities
                        if (Utils.ClosestDistance(collider, entity.collider) <= interactionRange) {
                            CmdSetTarget(entity.netIdentity);
                            FindObjectOfType<UINpcTrading>().Show();
                        // otherwise walk there
                        // use collider point(s) to also work with big entities
                        } else {
                            CmdNavigateTo(entity.collider.ClosestPointOnBounds(transform.position), interactionRange);
                        }
                    // not interesting or dead? => walk there
                    } else if (left) {
                        // walk there
                        // use collider point(s) to also work with big entities
                        CmdNavigateTo(entity.collider.ClosestPointOnBounds(transform.position), 0);
                    }
                // otherwise it's a movement target
                } else {
                    if (right) {
                        // set indicator and navigate to the nearest walkable
                        // destination. this prevents twitching when destination is
                        // accidentally in a room without a door etc.
                        var bestDestination = agent.NearestValidDestination(hit.point);
                        SetIndicatorViaPosition(bestDestination, hit.normal);
                        CmdNavigateTo(bestDestination, 0);
                    }
                }

                // reset wanted skill in any case (feels best)
                wantedSkill = -1;
            }
        }
    }

    // drag and drop ///////////////////////////////////////////////////////////
    void OnDragAndDrop_InventorySlot_InventorySlot(int[] slotIndices) {
        // slotIndices[0] = slotFrom; slotIndices[1] = slotTo
        // merge? (just check the name, rest is done server sided)
        if (inventory[slotIndices[0]].valid && inventory[slotIndices[1]].valid &&
            inventory[slotIndices[0]].name == inventory[slotIndices[1]].name) {
            CmdInventoryMerge(slotIndices[0], slotIndices[1]);
        // split?
        } else if (Utils.AnyKeyPressed(inventorySplitKeys)) {
            CmdInventorySplit(slotIndices[0], slotIndices[1]);
        // swap?
        } else {
            CmdSwapInventoryInventory(slotIndices[0], slotIndices[1]);
        }
    }

    void OnDragAndDrop_InventorySlot_NpcSellSlot(int[] slotIndices) {
        // slotIndices[0] = slotFrom; slotIndices[1] = slotTo
        FindObjectOfType<UINpcTrading>().sellIndex = slotIndices[0];
        FindObjectOfType<UINpcTrading>().sellAmountInput.text = inventory[slotIndices[0]].amount.ToString();
    }

    void OnDragAndClear_NpcSellSlot(int slotIndex) {
        FindObjectOfType<UINpcTrading>().sellIndex = -1;
    }
}
