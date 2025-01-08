using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class LagCompensation : NetworkBehaviour
{
    [SerializeField] GameObject simplifiedPlayer; //game object with collider same as player and no other data
    List<GameObject> spawnedPlayers = new();
    public void SimulatePlayersOnGivenTime(float time)
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
                TimeData pastData = pastDataTracker.GetPastData(time);
                GameObject instantiatedPlayer = Instantiate(simplifiedPlayer, pastData.position, pastData.rotation);
                instantiatedPlayer.GetComponent<NetworkObject>().Spawn();
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
            spawnedPlayers[i].GetComponent<Collider>().enabled = false;
            spawnedPlayers.RemoveAt(i);
        }
    }
}
