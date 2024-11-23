using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

public class PlayerData : NetworkBehaviour
{
    public NetworkVariable<FixedString32Bytes> Nickname { get;  private set; } = new();

    public NetworkList<ItemData.ItemProperties> Inventory { get; private set; }

    public void Awake()
    {
        //we need to do this before connection (so before Start()/OnNetworkSpawn()), but not on declaration, because there will be memory leak
        Inventory = new NetworkList<ItemData.ItemProperties>();
    }

    private void Start()
    {
        if (IsServer)
        {
            for (int i = 0; i < 3; i++) //we want to have 3 inventory slots in the beginning
            {
                Inventory.Add(new ItemData.ItemProperties());
            }
            Inventory[0] = new ItemData.ItemProperties { itemRarity=ItemData.ItemRarity.Wood, itemType = ItemData.ItemType.Sample};
        }
        //Reset inventory on server
        if (!IsOwner) { return; }
        ChangeNicknameServerRpc(GameObject.Find("Canvas").GetComponent<Menu>().Nickname);
    }

    //[WARNING !] This is unsafe, because it makes that nickname is de facto Owner controlled and can be changed any time by client by calling this method
    //It is that way, because otherwise nickname would need to be send to server on player join and server would need to assign it only one time and I don't really know how to do this and this will be assigned by client anyways, so I don't care
    [Rpc(SendTo.Server)]
    private void ChangeNicknameServerRpc(FixedString32Bytes setNickname)
    {
        Nickname.Value = setNickname;
    }

    //tries to add ItemData.itemProperties of GameObject to inventory and returns true if adding it to inventory succeded
    public bool AddItemToInventory(GameObject item)
    {
        if (!IsServer) throw new Exception("Trying to add item to inventory as a client");
        //TO DO: MAKE ADDING TO INVENTORY LOGIC
        ItemData itemData = item.GetComponent<ItemData>();
        Debug.Log(itemData.itemProperties.Value);
        Inventory[0] = itemData.itemProperties.Value;
        return true;
    }

    //Removes item in specified inventory slot and returnes it (so it can be spawned as an gameObject)
    public ItemData.ItemProperties RemoveItemFromInventory(int inventorySlot)
    {
        if (!IsServer) throw new Exception("Trying to remove item from inventory as a client");
        if (Inventory[inventorySlot].itemType != ItemData.ItemType.Null)
        {
            ItemData.ItemProperties item = Inventory[inventorySlot];
            Debug.Log(IsSpawned);
            Inventory[inventorySlot] = new ItemData.ItemProperties { itemType = ItemData.ItemType.Null }; //deleting item from inventory
            return item; //returnng item so it can be spawned on scene as gameObject
        }
        else
            throw new Exception("Trying to remove item from item slot, where there is no item [remember Inventory indexing starts at 0]");
    }
}
