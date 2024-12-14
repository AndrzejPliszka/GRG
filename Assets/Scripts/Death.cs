using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(PlayerData))]
[RequireComponent(typeof(Movement))]
public class Death : NetworkBehaviour
{
    PlayerData playerData;
    Movement playerMovement;
    [SerializeField] GameObject Ragdoll;
    [SerializeField] ItemTypeData itemTypeData;
    void Start()
    {
        if(!IsServer) { return; }
        playerData = GetComponent<PlayerData>();
        playerMovement = GetComponent<Movement>();
        playerData.OnDeath += Die;
    }

    void Die()
    {
        if (!IsServer) { throw new Exception("Client cannot decide to kill himself, only server can do that!"); };
        DestroyLocalPlayerModelOwnerRpc();
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
        Destroy(gameObject);

        //instantainte ragdoll
        GameObject ragdoll = Instantiate(Ragdoll, transform.position, transform.rotation);
        ragdoll.GetComponent<NetworkObject>().Spawn();
    }

    [Rpc(SendTo.Owner)]
    void DestroyLocalPlayerModelOwnerRpc()
    {
        GameObject.Find("Canvas").GetComponent<Menu>().PauseGame();
        if(playerMovement)
            Destroy(playerMovement.LocalPlayerModel);
    }
}
