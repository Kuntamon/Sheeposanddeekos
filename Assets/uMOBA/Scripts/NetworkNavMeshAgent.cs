// UNET's current NetworkTransform is really laggy, so we make it smooth by
// simply synchronizing the agent's destination. We could also lerp between
// the transform positions, but this is much easier and saves lots of bandwidth.
//
// Using a NavMeshAgent also has the benefit that no rotation has to be synced
// while moving.
//
// Notes:
//
// - Teleportations have to be detected and synchronized properly
// - Caching the agent won't work because serialization sometimes happens
//   before awake/start
// - We also need the stopping distance, otherwise entities move too far.
using UnityEngine;
#if UNITY_5_5_OR_NEWER // for people that didn't upgrade to 5.5. yet
using UnityEngine.AI;
#endif
using Mirror;

[RequireComponent(typeof(NavMeshAgent))]
public class NetworkNavMeshAgent : NetworkBehaviour {
    public NavMeshAgent agent; // assign in Inspector (instead of GetComponent)
    Vector3 lastDestination; // for dirty bit
    bool hadPath = false; // had path since last time? for warp detection

    // find out if destination changed on server
    [ServerCallback]
    void Update() {
        if (agent.hasPath || agent.pathPending) hadPath = true;
        if (agent.destination != lastDestination)
            SetDirtyBit(1);
    }

    // server-side serialization
    //
    // I M P O R T A N T
    //
    // always read and write the same amount of bytes. never let any errors
    // happen. otherwise readstr/readbytes out of range bugs happen.
    public override bool OnSerialize(NetworkWriter writer, bool initialState) {
        writer.Write(transform.position); // for rubberbanding
        writer.Write(agent.destination);
        writer.Write(agent.speed);
        writer.Write(agent.stoppingDistance);
        writer.Write(agent.destination != lastDestination && !hadPath); // warped? avoid sliding to respawn point etc.

        // area costs (important for monster lane costs)
        // -> we can't get a name from an index, hence we can't filter out non-
        //    empty areas. we have to sync all of them all the time.
        // -> only syncing the current one isn't enough either if we want to
        //    calculate the same path on clients and servers
        // -> 32 because Window->Navigation->Areas has 32 entries
        for (int i = 0; i < 32; ++i) writer.Write(agent.GetAreaCost(i));

        // reset helpers
        lastDestination = agent.destination;
        hadPath = false;

        return true;
    }

    // client-side deserialization
    //
    // I M P O R T A N T
    //
    // always read and write the same amount of bytes. never let any errors
    // happen. otherwise readstr/readbytes out of range bugs happen.
    public override void OnDeserialize(NetworkReader reader, bool initialState) {
        var position           = reader.ReadVector3();
        var dest               = reader.ReadVector3();
        agent.speed            = reader.ReadSingle();
        agent.stoppingDistance = reader.ReadSingle();
        bool warped            = reader.ReadBoolean();

        // area costs (see OnSerialize comment)
        for (int i = 0; i < 32; ++i) agent.SetAreaCost(i, reader.ReadSingle());

        // OnDeserialize must always return so that next one is called too
        try {
            // only try to set the destination if the agent is on a navmesh already
            // (it might not when falling from the sky after joining)
            if (agent.isOnNavMesh) {
                // warp if necessary. distance check to filter out false positives
                if (warped && Vector3.Distance(transform.position, position) > agent.radius)
                    agent.Warp(position); // to pos is always smoother

                // rubberbanding: if we are too far off because of a rapid position
                // change or latency, then warp
                // -> agent moves 'speed' meter per seconds
                // -> if we are 2 speed units behind, then we teleport
                //    (using speed is better than using a hardcoded value)
                if (Vector3.Distance(transform.position, position) > agent.speed * 2)
                    agent.Warp(position);

                // set destination afterwards, so that we never stop going there
                // even after being warped etc.
                agent.destination = dest;
            }
        } catch {}
    }
}
