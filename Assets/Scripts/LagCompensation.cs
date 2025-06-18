using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(PastDataTracker))]
public class LagCompensation : NetworkBehaviour
{
    [SerializeField] GameObject simplifiedPlayer; //game object with collider same as player and no other data
    readonly List<GameObject> spawnedPlayers = new(); //readonly here complies with best practises
    public void SimulatePlayersOnGivenTime(int tick)
    {
        if (!IsServer) { throw new Exception("Client cannot do lag compensation, dum dum!"); }
        float detectionRadius = 15f;
        GameObject[] players = GameObject.FindGameObjectsWithTag("Player");

        foreach (GameObject player in players)
        {
            float distance = Vector3.Distance(transform.position, player.transform.position);
            if (distance <= detectionRadius && player != gameObject)
            {
                PastDataTracker pastDataTracker = player.GetComponent<PastDataTracker>();
                TickData pastData = pastDataTracker.GetPastData(tick);
                GameObject instantiatedPlayer = Instantiate(simplifiedPlayer, pastData.position, pastData.rotation);
                instantiatedPlayer.layer = LayerMask.NameToLayer("ServerSimulation");
                //We need to differentiate these prefabs, so we know which player they are meant to "represent"
                instantiatedPlayer.name = player.GetComponent<NetworkObject>().NetworkObjectId.ToString();
                spawnedPlayers.Add(instantiatedPlayer);
            }
        }
    }
    public void DestroySimulatedPlayers()
    {
        if (!IsServer) { throw new Exception("Client cannot do lag compensation, dum dum!"); }
        for (int i = spawnedPlayers.Count - 1; i >= 0; i--) //for loop going down, so when we delete elements, indexes of next ones stay the same
        {
            Destroy(spawnedPlayers[i]);
            spawnedPlayers.RemoveAt(i);
        }
    }
}
