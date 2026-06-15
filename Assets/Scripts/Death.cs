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
    [SerializeField] GameObject moneyObject;

    bool isDying = false;

    public override void OnNetworkSpawn()
    {
        menuScript = GameObject.Find("Canvas").GetComponent<Menu>();
        playerData = GetComponent<PlayerData>();
        playerMovement = GetComponent<Movement>();

        if(IsOwner) {
            Debug.Log($"Subscribed {name} Owner={OwnerClientId}");
            NetworkManager.OnConnectionEvent += HandleDisconnectedPlayers; //Without this, game would froze when theres no connection with server
        }
        if (IsServer)
        {
            playerData.OnDeath += () => { Die(true); };
            NetworkManager.OnClientDisconnectCallback += clientId =>
            {
                if (!IsServer || !SceneManager.GetActiveScene().isLoaded || NetworkManager == null || NetworkManager.ShutdownInProgress || !NetworkManager.Singleton.IsListening)
                    return;
                if (clientId == OwnerClientId)
                    Die();
            };
        }
    }
    //Spawns ragdoll, throws away all items in inventory and destroys player side local model
    void Die(bool sendDeathRpcToOwner = false)
    {
        if (!IsServer) { throw new Exception("Client cannot decide to kill himself, only server can do that!"); };
        if (isDying) return;
        isDying = true;
        ItemTypeData itemData = GameManager.Instance.ItemTypeData;
        //drop items from inventory
        for (int i = 0; i < playerData.Inventory.Count; i++)
        {
            ItemData.ItemProperties itemProperties = playerData.RemoveItemFromInventory(i, false);

            if (itemProperties.itemType == ItemData.ItemType.Null) { continue; } //if null do not spawn object, because there was no item in the first place

            //maybe encapsulate into function, currently same code is used in objectInteraction
            GameObject itemPrefab = itemData.GetDataOfItemType(itemProperties.itemType).droppedItemPrefab;
            GameObject newItem = Instantiate(itemPrefab, transform.position + transform.forward, new Quaternion());
            newItem.GetComponent<NetworkObject>().Spawn();
            newItem.GetComponent<ItemData>().itemProperties.Value = itemProperties;
        }

        if (playerData.Role.Value == PlayerData.PlayerRole.Leader)
        {
            int townId = playerData.TownId.Value;
            GameManager.Instance.RemovePlayerFromRegistry(gameObject);
            GameManager.Instance.ChangeLeader(gameObject, townId);
        }
        else
            GameManager.Instance.RemovePlayerFromRegistry(gameObject);

        playerMovement.sittingCourutineCancellationToken?.Cancel(); //If player is killed during sitting in shop, stop using it before dying
        if(playerData.Money.Value > 0)
        {
            GameObject spawnedMoneyObject = Instantiate(moneyObject, transform.position + transform.forward, new Quaternion());
            spawnedMoneyObject.GetComponent<NetworkObject>().Spawn();
            spawnedMoneyObject.GetComponent<MoneyObject>().moneyAmount.Value = playerData.Money.Value;
        }
        

        //instantainte ragdoll
        GameObject ragdollObject = Instantiate(ragdoll, transform.position, transform.rotation);
        ragdollObject.GetComponent<RagdollData>().Role.Value = playerData.Role.Value;
        PlayerAppearance ragdollAppearance = ragdollObject.GetComponent<PlayerAppearance>();
        PlayerAppearance playerAppearance = GetComponent<PlayerAppearance>();
        ragdollObject.GetComponent<NetworkObject>().Spawn();
        ragdollAppearance.hatId.Value = playerAppearance.hatId.Value;
        ragdollAppearance.inprintId.Value = playerAppearance.inprintId.Value;
        ragdollAppearance.skinId.Value = playerAppearance.skinId.Value;
        ragdollAppearance.faceId.Value = playerAppearance.faceId.Value;

        if (sendDeathRpcToOwner)
            DeathOwnerRpc();

        if(IsSpawned)
            NetworkObject.Despawn(true);
    }

    [Rpc(SendTo.Owner)]
    void DeathOwnerRpc()
    {
        if (IsServer)
            menuScript.PauseGame();
        else
            menuScript.QuitServer();
    }

    //This function just calls QuitServer if player calling method is owner
    void HandleDisconnectedPlayers(NetworkManager networkManager, ConnectionEventData connectionData)
    {

        if (connectionData.EventType == ConnectionEvent.ClientDisconnected && IsClient && connectionData.ClientId == NetworkManager.Singleton.LocalClientId && menuScript != null)
        {
            menuScript.QuitServer();
        }
    }
    
    public override void OnNetworkDespawn()
    {
        NetworkManager.OnConnectionEvent -= HandleDisconnectedPlayers;
    }
}
