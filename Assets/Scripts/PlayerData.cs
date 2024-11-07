using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

#nullable enable //I need to enable this, because I need be able to have null in inventory array if there is no items in it
public class PlayerData : NetworkBehaviour
{
    public NetworkVariable<FixedString32Bytes> Nickname { get;  private set; } = new();
    //This thing just creates network variable array of three things type ItemProperties called inventory (? means that array can store null value, apart ItemProperties)
    public NetworkVariable<ItemData.ItemProperties>?[] Inventory { get; private set; } = new NetworkVariable<ItemData.ItemProperties>[3];
    private void Start()
    {
        if(!IsOwner) { return; }
        ChangeNicknameServerRpc(GameObject.Find("Canvas").GetComponent<Menu>().Nickname);
    }
    //[WARNING !] This is unsafe, because it makes that nickname is de facto Owner controlled and can be changed any time by client by calling this method
    //It is that way, because otherwise nickname would need to be send to server on player join and server would need to assign it only one time and I don't really know how to do this and this will be assigned by client anyways, so I don't care
    [Rpc(SendTo.Server)]
    private void ChangeNicknameServerRpc(FixedString32Bytes setNickname)
    {
        Nickname.Value = setNickname;
        Debug.Log(Nickname.Value);
    }

    //returns true if adding item to inventory succeded
    public bool AddItemToInventory(GameObject item)
    {
        if (!IsServer) { return false; }
        Inventory[0] = item.GetComponent<ItemData>().itemProperties;
        Debug.Log(Inventory[0]);
        return true;
    }
}
