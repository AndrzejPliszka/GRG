using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(PlayerData))]
[RequireComponent(typeof(Movement))]
public class Death : NetworkBehaviour
{
    PlayerData playerData;
    Movement playerMovement;
    Menu menuScript;
    [SerializeField] GameObject ragdoll;
    [SerializeField] ItemTypeData itemTypeData;
    [SerializeField] GameObject moneyObject;

    void Start()
    {
        menuScript = GameObject.Find("Canvas").GetComponent<Menu>();
        playerData = GetComponent<PlayerData>();
        playerMovement = GetComponent<Movement>();

        if(IsOwner) {
            NetworkManager.OnConnectionEvent += HandleDisconnectedPlayers; //Without this, game would froze when theres no connection with server
        }
        if (IsServer)
        {
            playerData.OnDeath += () => { Destroy(gameObject); };
        }
            
        
    }
    //Spawns ragdoll, throws away all items in inventory and destroys player side local model
    void Die()
    {
        if (!IsServer) { throw new Exception("Client cannot decide to kill himself, only server can do that!"); };

        if(playerData.Role.Value == PlayerData.PlayerRole.Leader)
        {
            int townId = playerData.TownId.Value;
            GameManager.Instance.RemovePlayerFromRegistry(gameObject);
            GameManager.Instance.ChangeLeader(gameObject, townId);
        }
        else
            GameManager.Instance.RemovePlayerFromRegistry(gameObject);

        DestroyLocalPlayerModelOwnerRpc();
        playerMovement.sittingCourutineCancellationToken?.Cancel(); //If player is killed during sitting in shop, stop using it before dying
        //drop items from inventory
        for (int i = 0; i < playerData.Inventory.Count; i++)
        {
            ItemData.ItemProperties itemProperties = playerData.RemoveItemFromInventory(i);

            if (itemProperties.itemType == ItemData.ItemType.Null) { continue; } //if null do not spawn object, because there was no item in the first place

            //maybe encapsulate into function, currently same code is used in objectInteraction
            GameObject itemPrefab = itemTypeData.GetDataOfItemType(itemProperties.itemType).droppedItemPrefab;
            GameObject newItem = Instantiate(itemPrefab, transform.position + transform.forward, new Quaternion());
            newItem.GetComponent<NetworkObject>().Spawn();
            newItem.GetComponent<ItemData>().itemProperties.Value = itemProperties;
        }
        if(playerData.Money.Value > 0)
        {
            GameObject spawnedMoneyObject = Instantiate(moneyObject, transform.position + transform.forward, new Quaternion());
            spawnedMoneyObject.GetComponent<NetworkObject>().Spawn();
            spawnedMoneyObject.GetComponent<MoneyObject>().moneyAmount.Value = playerData.Money.Value;
        }
        

        //instantainte ragdoll
        GameObject ragdollObject = Instantiate(ragdoll, transform.position, transform.rotation);
        ragdollObject.GetComponent<NetworkObject>().Spawn();
    }

    [Rpc(SendTo.Owner)]
    void DestroyLocalPlayerModelOwnerRpc()
    {
        if (this == null) { Debug.LogWarning("DestroyLocalPlayerModelOwnerRpc called on destroyed object."); return; }
        //when you die you want to display menu that lets you respawn/exit the server
        if (menuScript != null)
            menuScript.PauseGame();
        if(playerMovement != null && playerMovement.LocalPlayerModel != null)
            Destroy(playerMovement.LocalPlayerModel);
    }

    //This function just calls QuitServer if player calling method is owner
    void HandleDisconnectedPlayers(NetworkManager networkManager, ConnectionEventData connectionData)
    {
        if (connectionData.EventType == ConnectionEvent.ClientDisconnected && IsClient && connectionData.ClientId == NetworkManager.Singleton.LocalClientId && menuScript != null)
            menuScript.QuitServer();
    }

    //Always when deleting player with this script Die() function will be called!
    public override void OnNetworkDespawn()
    {
        NetworkManager.OnConnectionEvent -= HandleDisconnectedPlayers;
        //without it on host there will be errors on him quiting server (also spawning corpses on server that is about to turn off is weird)
        if (!IsServer || !SceneManager.GetActiveScene().isLoaded || NetworkManager == null || NetworkManager.ShutdownInProgress || !NetworkManager.Singleton.IsListening) 
            return;
        Die();
    }
}
