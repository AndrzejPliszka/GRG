using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Unity.Netcode;
using UnityEngine;


[RequireComponent(typeof(ObjectInteraction))]
[RequireComponent(typeof(PlayerData))]
[RequireComponent(typeof(Movement))]
//This script should be on every player (despite the name), alternative (if you want to modify script to fit it) would be to add it only to guards dynamically, but this makes the job done
public class Guard : NetworkBehaviour
{
    PlayerData playerData;
    ObjectInteraction objectInteraction;

    private void Awake()
    {
        playerData = GetComponent<PlayerData>();
        objectInteraction = GetComponent<ObjectInteraction>();
    }

    void Update()
    {
        if (playerData.Role.Value != PlayerData.PlayerRole.Guard)
            return;

        if (Input.GetKeyDown(KeyCode.F))
        {
            float cameraXRotation = GameObject.Find("Camera").transform.rotation.eulerAngles.x;
            if (IsHost)
                TryToArrestPlayerServerRpc(cameraXRotation, NetworkManager.Singleton.ServerTime.Tick);
            else
                //I subtract 3 from here, because it doesn't work without it: server simulates time which was too early for some reason
                TryToArrestPlayerServerRpc(cameraXRotation, NetworkManager.Singleton.ServerTime.Tick - 3);
        }
    }

    [Rpc(SendTo.Server)]
    void TryToArrestPlayerServerRpc(float cameraXRotation, int currentTick)
    {
        if (playerData.Role.Value != PlayerData.PlayerRole.Guard)
            throw new System.Exception("Function called by player which is not guard!");

        GameObject objectInFrontOfCamera = objectInteraction.GetObjectInFrontOfCamera(cameraXRotation, currentTick);
        PlayerData hitPlayerData = objectInFrontOfCamera.GetComponent<PlayerData>();
        Movement hitPlayerMovement = objectInFrontOfCamera.GetComponent<Movement>();
        PlayerUI playerUI = GetComponent<PlayerUI>();
        if (!hitPlayerData && !hitPlayerMovement)
            return;
        if (hitPlayerData.Role.Value == PlayerData.PlayerRole.Leader)
        {
            if (playerUI)
                playerUI.DisplayErrorOwnerRpc("Leader cannot be arrested!");
            return;
        }
            
        if (hitPlayerData.IsCriminal.Value && !hitPlayerData.IsInJail.Value) //This being in jail is temporary, because it should never make you criminal in the first place!
        {
            if (playerData.TownPlayerIsIn.Value != playerData.TownId.Value)
            {
                if (playerUI)
                    playerUI.DisplayErrorOwnerRpc("To arrest criminal, you need to be in town!");
                return;
            }

            //Put here code related to arresting player
            GameManager.Instance.TownData[playerData.TownId.Value].OnPlayerArrest.Invoke(hitPlayerData.transform);
            GameManager.Instance.ChangePlayerAffiliation(objectInFrontOfCamera, PlayerData.PlayerRole.Peasant, -1);
            hitPlayerData.IsCriminal.Value = false;
            hitPlayerData.JailCooldown.Value = 10;
            hitPlayerData.IsInJail.Value = true;
            playerData.ChangeMoney(10);
            if(playerUI)
                playerUI.DisplayErrorOwnerRpc("Arrested criminal!");
        }
    }
}
