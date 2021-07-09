// The Entity class is rather simple. It contains a few basic entity properties
// like health, mana and level that all inheriting classes like Players and
// Monsters can use.
//
// Entities also have a _target_ Entity that can't be synchronized with a
// SyncVar. Instead we created a EntityTargetSync component that takes care of
// that for us.
//
// Entities use a deterministic finite state machine to handle IDLE/MOVING/DEAD/
// CASTING etc. states and events. Using a deterministic FSM means that we react
// to every single event that can happen in every state (as opposed to just
// taking care of the ones that we care about right now). This means a bit more
// code, but it also means that we avoid all kinds of weird situations like 'the
// monster doesn't react to a dead target when casting' etc.
// The next state is always set with the return value of the UpdateServer
// function. It can never be set outside of it, to make sure that all events are
// truly handled in the state machine and not outside of it. Otherwise we may be
// tempted to set a state in CmdBeingTrading etc., but would likely forget of
// special things to do depending on the current state.
//
// Entities also need a kinematic Rigidbody so that OnTrigger functions can be
// called. Note that there is currently a Unity bug that slows down the agent
// when having lots of FPS(300+) if the Rigidbody's Interpolate option is
// enabled. So for now it's important to disable Interpolation - which is a good
// idea in general to increase performance.
using System.Collections.Generic;
using System.Linq;
using System;
using UnityEngine;
#if UNITY_5_5_OR_NEWER // for people that didn't upgrade to 5.5. yet
using UnityEngine.AI;
#endif
using Mirror;

// Team Enum (we use an enum instead of a string so that changing a team name
// will force us to update the code in all places)
public enum Team {Good, Evil, Neutral};

// note: no animator required, towers, dummies etc. may not have one
[RequireComponent(typeof(Rigidbody))] // kinematic, only needed for OnTrigger
//[RequireComponent(typeof(NetworkProximityChecker))]
//[RequireComponent(typeof(NavMeshAgent))] // towers don't need them
//[RequireComponent(typeof(NetworkNavMeshAgent))] // towers don't need them
public abstract class Entity : NetworkBehaviour {
    [Header("Components")]
    public NavMeshAgent agent;
    public NetworkProximityChecker proxchecker;
    public Animator animator;
    new public Collider collider;

    // finite state machine
    // -> state only writable by entity class to avoid all kinds of confusion
    [Header("State")]
    [SyncVar, SerializeField] string _state = "IDLE";
    public string state { get { return _state; } }

    // 'Entity' can't be SyncVar and NetworkIdentity causes errors when null,
    // so we use [SyncVar] GameObject and wrap it for simplicity
    [Header("Target")]
    [SyncVar] GameObject _target;
    public Entity target {
        get { return _target != null  ? _target.GetComponent<Entity>() : null; }
        set { _target = value != null ? value.gameObject : null; }
    }

    [Header("Level")]
    [SyncVar] public int level = 1;

    [Header("Health")]
    [SerializeField] protected LevelBasedInt _healthMax = new LevelBasedInt{baseValue=100};
    public virtual int healthMax {
        get {
            // base + buffs
            int buffBonus = buffs.Sum(buff => buff.buffsHealthMax);
            return _healthMax.Get(level) + buffBonus;
        }
    }
    [SyncVar] int _health = 1;
    public int health {
        get { return Mathf.Min(_health, healthMax); } // min in case hp>hpmax after buff ends etc.
        set { _health = Mathf.Clamp(value, 0, healthMax); }
    }

    public Entity[] invincibleWhileAllAlive; // base invincible while barracks exist etc.
    public bool healthRecovery = true; // can be disabled in combat etc.
    [SerializeField] protected LevelBasedInt _healthRecoveryRate = new LevelBasedInt{baseValue=1};
    public virtual int healthRecoveryRate {
        get {
            // base + buffs
            float buffPercent = buffs.Sum(buff => buff.buffsHealthPercentPerSecond);
            return _healthRecoveryRate.Get(level) + Convert.ToInt32(buffPercent * healthMax);
        }
    }

    [Header("Mana")]
    [SerializeField] protected LevelBasedInt _manaMax = new LevelBasedInt{baseValue=100};
    public virtual int manaMax {
        get {
            // base + buffs
            int buffBonus = buffs.Sum(buff => buff.buffsManaMax);
            return _manaMax.Get(level) + buffBonus;
        }
    }
    [SyncVar] int _mana = 1;
    public int mana {
        get { return Mathf.Min(_mana, manaMax); } // min in case hp>hpmax after buff ends etc.
        set { _mana = Mathf.Clamp(value, 0, manaMax); }
    }

    public bool manaRecovery = true; // can be disabled in combat etc.
    [SerializeField] protected LevelBasedInt _manaRecoveryRate = new LevelBasedInt{baseValue=1};
    public int manaRecoveryRate {
        get {
            // base + buffs
            float buffPercent = buffs.Sum(buff => buff.buffsManaPercentPerSecond);
            return _manaRecoveryRate.Get(level) + Convert.ToInt32(buffPercent * manaMax);
        }
    }

    [Header("Damage")]
    [SerializeField] protected LevelBasedInt _damage = new LevelBasedInt{baseValue=1};
    public virtual int damage {
        get {
            // base + buffs
            int buffBonus = buffs.Sum(buff => buff.buffsDamage);
            return _damage.Get(level) + buffBonus;
        }
    }

    [Header("Defense")]
    [SerializeField] protected LevelBasedInt _defense = new LevelBasedInt{baseValue=1};
    public virtual int defense {
        get {
            // base + buffs
            int buffBonus = buffs.Sum(buff => buff.buffsDefense);
            return _defense.Get(level) + buffBonus;
        }
    }

    [Header("Block")]
    [SerializeField] protected LevelBasedFloat _blockChance;
    public virtual float blockChance {
        get {
            // base + buffs
            float buffBonus = buffs.Sum(buff => buff.buffsBlockChance);
            return _blockChance.Get(level) + buffBonus;
        }
    }

    [Header("Critical")]
    [SerializeField] protected LevelBasedFloat _criticalChance;
    public virtual float criticalChance {
        get {
            // base + buffs
            float buffBonus = buffs.Sum(buff => buff.buffsCriticalChance);
            return _criticalChance.Get(level) + buffBonus;
        }
    }

    // speed wrapper
    public float speed { get { return agent.speed; } }

    [Header("Team")] // to only attack different team etc.
    [SyncVar] public Team team = Team.Good;

    [Header("Popups")]
    public GameObject damagePopupPrefab;

    // skill system for all entities (players, monsters, npcs, towers, ...)
    // 'skillTemplates' are the available skills (first one is default attack)
    // 'skills' are the loaded skills with cooldowns etc.
    [Header("Skills & Buffs")]
    public SkillTemplate[] skillTemplates;
    public SyncListSkill skills = new SyncListSkill();
    public SyncListBuff buffs = new SyncListBuff(); // active buffs
    // current skill (synced because we need it as an animation parameter)
    [SyncVar] protected int currentSkill = -1;

    // effect mount is where the arrows/fireballs/etc. are spawned
    // -> can be overwritten, e.g. for mages to set it to the weapon's effect
    //    mount
    // -> assign to right hand if in doubt!
    [SerializeField] Transform _effectMount;
    public virtual Transform effectMount { get { return _effectMount; } }

    // cache team members to avoid FindObjectsOfType usage
    // for NetworkProximityCheckerTeam and FogOfWar, which would be very costly
    public static Dictionary<Team, HashSet<Entity>> teams = new Dictionary<Team, HashSet<Entity>>() {
        {Team.Good, new HashSet<Entity>()},
        {Team.Evil, new HashSet<Entity>()},
        {Team.Neutral, new HashSet<Entity>()}
    };

    // networkbehaviour ////////////////////////////////////////////////////////
    protected virtual void Awake() {}

    public override void OnStartServer() {
        // health recovery every second
        InvokeRepeating("Recover", 1, 1);

        // dead if spawned without health
        if (health == 0) _state = "DEAD";

        // load skills based on skill templates
        foreach (var t in skillTemplates)
            skills.Add(new Skill(t));
    }

    protected virtual void Start() {
        teams[team].Add(this);

        // disable animator on server. this is a huge performance boost and
        // definitely worth one line of code (1000 monsters: 22 fps => 32 fps)
        // (!isClient because we don't want to do it in host mode either)
        // (OnStartServer doesn't know isClient yet, Start is the only option)
        if (!isClient) animator.enabled = false;
    }

    // called on the server
    void OnDestroy() {
        teams[team].Remove(this);
    }

    // called on the client
    public override void OnNetworkDestroy() {
        teams[team].Remove(this);
    }

    // entity logic will be implemented with a finite state machine. we will use
    // UpdateIDLE etc. so that we have a 100% guarantee that it works properly
    // and we never miss a state or update two states after another
    // note: can still use LateUpdate for Updates that should happen in any case
    // -> we can even use parameters if we need them some day.
    void Update() {
        // unlike uMMORPG, all entities have to be updated at all times in uMOBA
        // because monster's must always run towards the enemy base, even if no
        // one is around.
        if (isClient) UpdateClient();
        if (isServer) {
            CleanupBuffs();
            // clear target if it's hidden to avoid all kinds of weird cases
            // where we might still be targeting respawning monsters etc.
            if (target != null && target.IsHidden()) target = null;
            _state = UpdateServer();
        }
    }

    // update for server. should return the new state.
    protected abstract string UpdateServer();

    // update for client.
    protected abstract void UpdateClient();

    // visibility //////////////////////////////////////////////////////////////
    // hide a entity
    // note: using SetActive won't work because its not synced and it would
    //       cause inactive objects to not receive any info anymore
    // note: this won't be visible on the server as it always sees everything.
    [Server]
    public void Hide() {
        proxchecker.forceHidden = true;
    }

    [Server]
    public void Show() {
        proxchecker.forceHidden = false;
    }

    // is the entity currently hidden?
    // note: usually the server is the only one who uses forceHidden, the
    //       client usually doesn't know about it and simply doesn't see the
    //       GameObject.
    public bool IsHidden() {
        return proxchecker.forceHidden;
    }

    public float VisRange() {
        return proxchecker.visRange;
    }

    // look at a transform while only rotating on the Y axis (to avoid weird
    // tilts)
    public void LookAtY(Vector3 position) {
        transform.LookAt(new Vector3(position.x, transform.position.y, position.z));
    }

    // note: client can find out if moving by simply checking the state!
    [Server] // server is the only one who has up-to-date NavMeshAgent
    public bool IsMoving() {
        // -> agent.hasPath will be true if stopping distance > 0, so we can't
        //    really rely on that.
        // -> pathPending is true while calculating the path, which is good
        // -> remainingDistance is the distance to the last path point, so it
        //    also works when clicking somewhere onto a obstacle that isn'
        //    directly reachable.
        return agent.pathPending ||
               agent.remainingDistance > agent.stoppingDistance ||
               agent.velocity != Vector3.zero;
    }

    // health & mana ///////////////////////////////////////////////////////////
    public float HealthPercent() {
        return (health != 0 && healthMax != 0) ? (float)health / (float)healthMax : 0;
    }

    [Server]
    public void Revive() {
        health = healthMax;
    }

    public float ManaPercent() {
        return (mana != 0 && manaMax != 0) ? (float)mana / (float)manaMax : 0;
    }

    // bases have the ability to be invincible while ALL of the 3 barracks exist
    // users can also make barracks invincible while a tower exists etc.
    [Server]
    public bool IsInvincible() {
        // return true if all are still alive, false otherwise
        // (also false if the list is empty)
        return invincibleWhileAllAlive.Length > 0 &&
               invincibleWhileAllAlive.All(e => e != null && e.health > 0);
    }

    // combat //////////////////////////////////////////////////////////////////
    // no need to instantiate damage popups on the server
    // -> passing the GameObject and calculating the position on the client
    //    saves server computations and takes less bandwidth (4 instead of 12 byte)
    enum PopupType { Normal, Block, Crit };

    [ClientRpc]
    void RpcShowDamagePopup(GameObject damageReceiver, PopupType popupType, int amount) {
        // spawn the damage popup (if any) and set the text
        // (-1 = block)
        if (damageReceiver != null) { // still around?
            Entity receiverEntity = damageReceiver.GetComponent<Entity>();
            if (receiverEntity != null && receiverEntity.damagePopupPrefab != null) {
                // showing it above their head looks best, and we don't have to use
                // a custom shader to draw world space UI in front of the entity
                var bounds = receiverEntity.collider.bounds;
                Vector3 position = new Vector3(bounds.center.x, bounds.max.y, bounds.center.z);

                var popup = Instantiate(receiverEntity.damagePopupPrefab, position, Quaternion.identity);
                if (popupType == PopupType.Normal)
                    popup.GetComponentInChildren<TextMesh>().text = amount.ToString();
                else if (popupType == PopupType.Block)
                    popup.GetComponentInChildren<TextMesh>().text = "<i>Block!</i>";
                else if (popupType == PopupType.Crit)
                    popup.GetComponentInChildren<TextMesh>().text = amount + " Crit!";
            }
        }
    }

    // deal damage at another entity
    // (can be overwritten for players etc. that need custom functionality)
    [Server]
    public virtual void DealDamageAt(Entity entity, int amount) {
        int damageDealt = 0;
        var popupType = PopupType.Normal;

        // don't deal any damage if entity is invincible
        if (!entity.IsInvincible()) {
            // block? (we use < not <= so that block rate 0 never blocks)
            if (UnityEngine.Random.value < entity.blockChance) {
                popupType = PopupType.Block;
            // deal damage
            } else {
                // subtract defense (but leave at least 1 damage, otherwise
                // it may be frustrating for weaker players)
                damageDealt = Mathf.Max(amount - entity.defense, 1);

                // critical hit?
                if (UnityEngine.Random.value < criticalChance) {
                    damageDealt *= 2;
                    popupType = PopupType.Crit;
                }

                // deal the damage
                entity.health -= damageDealt;
            }
        }

        // show damage popup in observers via ClientRpc
        RpcShowDamagePopup(entity.gameObject, popupType, damageDealt);

        // let's make sure to pull aggro in any case so that archers
        // are still attacked if they are outside of the aggro range
        entity.OnAggro(this);
    }

    // recovery ////////////////////////////////////////////////////////////////
    // receover health and mana once a second
    // note: when stopping the server with the networkmanager gui, it will
    //       generate warnings that Recover was called on client because some
    //       entites will only be disabled but not destroyed. let's not worry
    //       about that for now.
    [Server]
    public virtual void Recover() {
        if (enabled && health > 0) {
            if (healthRecovery) health += healthRecoveryRate;
            if (manaRecovery) mana += manaRecoveryRate;
        }
    }

    // aggro ///////////////////////////////////////////////////////////////////
    // this function is called by the AggroArea (if any) on clients and server
    public virtual void OnAggro(Entity entity) {}

    // skill system ////////////////////////////////////////////////////////////
    // we need an abstract function to check if an entity can attack another,
    // e.g. if player can attack monster / pet / npc, ...
    // => we don't just compare the type because other things like 'is own pet'
    //    etc. matter too
    public abstract bool CanAttack(Entity entity);

    // the first check validates the caster
    // (the skill won't be ready if we check self while casting it. so the
    //  checkSkillReady variable can be used to ignore that if needed)
    public bool CastCheckSelf(Skill skill, bool checkSkillReady = true) {
        // no cooldown, hp, mp?
        return (!checkSkillReady || skill.IsReady()) &&
               health > 0 &&
               mana >= skill.manaCosts;
    }

    // the second check validates the target and corrects it for the skill if
    // necessary (e.g. when trying to heal an npc, it sets target to self first)
    // (skill shots that don't need a target will just return true if the user
    //  wants to cast them at a valid position)
    public bool CastCheckTarget(Skill skill) {
        return skill.CheckTarget(this);
    }

    // the third check validates the distance between the caster and the target
    // (target entity or target position in case of skill shots)
    // note: castchecktarget already corrected the target (if any), so we don't
    //       have to worry about that anymore here
    public bool CastCheckDistance(Skill skill, out Vector3 destination) {
        return skill.CheckDistance(this, out destination);
    }

    // casts the skill. casting and waiting has to be done in the state machine
    public void CastSkill(Skill skill) {
        // * check if we can currently cast a skill (enough mana etc.)
        // * check if we can cast THAT skill on THAT target
        // note: we don't check the distance again. the skill will be cast even
        //   if the target walked a bit while we casted it (it's simply better
        //   gameplay and less frustrating)
        if (CastCheckSelf(skill, false) && CastCheckTarget(skill)) {
            // let the skill template handle the action
            skill.Apply(this);

            // decrease mana in any case
            mana -= skill.manaCosts;

            // start the cooldown (and save it in the struct)
            skill.cooldownEnd = NetworkTime.time + skill.cooldown;

            // save any skill modifications in any case
            skills[currentSkill] = skill;
        } else {
            // not all requirements met. no need to cast the same skill again
            currentSkill = -1;
        }
    }

    // helper function to add or refresh a buff
    public void AddOrRefreshBuff(Buff buff) {
        // reset if already in buffs list, otherwise add
        int index = buffs.FindIndex(b => b.name == buff.name);
        if (index != -1) buffs[index] = buff;
        else buffs.Add(buff);
    }

    // helper function to remove all buffs that ended
    void CleanupBuffs() {
        for (int i = 0; i < buffs.Count; ++i) {
            if (buffs[i].BuffTimeRemaining() == 0) {
                buffs.RemoveAt(i);
                --i;
            }
        }
    }
}
