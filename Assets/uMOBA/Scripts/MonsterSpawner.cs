// Used to spawn monsters repeatedly. The 'monsterGoals' will be passed to the
// monster after spawning it, so that the monster knows where to move (which
// lane to walk along).
using UnityEngine;
using UnityEngine.AI;
using Mirror;

public class MonsterSpawner : NetworkBehaviour {
    // the monster to spawn
    public Monster monster;
    public float interval = 5;
    public Transform monsterGoal; // passed to monsters
    public string NavMeshAreaPreferred = ""; // MidLane, etc.

    public override void OnStartServer() {
        InvokeRepeating("Spawn", interval, interval);
    }

    [Server]
    void Spawn() {
        var go = (GameObject)Instantiate(monster.gameObject, transform.position, Quaternion.identity);
        go.name = monster.name; // remove "(Clone)" suffix
        go.GetComponent<Monster>().goal = monsterGoal;

        // temporary workaround for bug #950004
        go.GetComponent<NavMeshAgent>().Warp(transform.position);

        // set preferred navmesh area costs to 1
        int index = NavMesh.GetAreaFromName(NavMeshAreaPreferred);
        if (index != -1)
            go.GetComponent<NavMeshAgent>().SetAreaCost(index, 1);

        NetworkServer.Spawn(go);
    }
}
