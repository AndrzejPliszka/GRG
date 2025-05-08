using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

//This script exists so we can handle arrest by events
public class Jail : NetworkBehaviour
{
    [SerializeField] int townId;
    [SerializeField] Vector3 prisonSpawnOffset;
    public override void OnNetworkSpawn()
    {

        Debug.Log("Set up 123");
        if (!IsServer) { return; }
        Debug.Log("Set up succesfull");
        GameManager.Instance.TownData[townId].OnPlayerArrest += TeleportPlayerToJail;
    }

    void TeleportPlayerToJail(Transform player)
    {
        if (!IsServer) { throw new System.Exception("Teleporting on client?"); }
        Debug.Log(player + "  Teleporting"  + transform.position);
        player.GetComponent<Movement>().TeleportPlayerToPosition(transform.position + prisonSpawnOffset);
    }
}
