using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UIElements;

public class Shop : NetworkBehaviour
{
    [SerializeField] int townId;

    [SerializeField] TMP_Text shopText;
    [SerializeField] GameObject customerChair;
    [SerializeField] GameObject workerChair;

    [SerializeField] ItemData.ItemTier soldItemTier;
    [SerializeField] ItemData.ItemType soldItemType;

    //server side variables (not used on client)
    [SerializeField] int price;
    bool isSomeoneWorking = false; //is someone working in this shop
    GameObject buyingPlayerReference; //this refrence is needed to force buyingPlayer to stop sitting if workingPlayer no longer works
    public ItemData.ItemProperties ItemToSell { private set; get; } = new();

    public override void OnNetworkSpawn()
    {
        if(soldItemType != ItemData.ItemType.Null )
            ItemToSell = new ItemData.ItemProperties() { itemTier = soldItemTier, itemType = soldItemType };

        shopText.text = $"{ItemToSell.itemTier}\n{ItemToSell.itemType}\nShop";

    }

    public async void BuyFromShop(GameObject buyingPlayer)
    {
        if (!IsServer) { throw new Exception("Client cannot buy anything by himself, call this method on server!"); } 
        PlayerData playerData = buyingPlayer.GetComponent<PlayerData>();
        Movement playerMovement = buyingPlayer.GetComponent<Movement>();
        if (playerMovement == null || playerData == null)
            Debug.LogWarning("Client does not have playerMovement or playerData script and is trying to buy something!");

        if (playerData.Money.Value >= price && playerData.FindFreeInventorySlot() >= 0 && isSomeoneWorking && buyingPlayerReference == null)
        {
            buyingPlayerReference = buyingPlayer;
            playerMovement.TeleportPlayerToPosition(customerChair.transform.position + new Vector3(0, 1, 0.5f));
            bool didBuy = await playerMovement.MakePlayerSit(5);
            buyingPlayerReference = null;
            if (!didBuy)
            {
                return;
            }
            bool didAddToInventory = playerData.AddItemToInventory(ItemToSell);
            if(didAddToInventory)
                playerData.ChangeMoney(-price);
        }
    }

    public async void WorkInShop(GameObject workingPlayer)
    {
        if (!IsServer) { throw new Exception("Client cannot do anything by himself, call this method on server!"); }
        PlayerData playerData = workingPlayer.GetComponent<PlayerData>();
        Movement playerMovement = workingPlayer.GetComponent<Movement>();
        if (playerMovement == null || playerData == null)
            Debug.LogWarning("Client does not have playerMovement or playerData script and is trying to buy something!");

        if (isSomeoneWorking)
        {
            return;
        }

        playerMovement.TeleportPlayerToPosition(workerChair.transform.position + workerChair.transform.localRotation * new Vector3(0, 1, 0.5f));
        isSomeoneWorking = true;
        while (true)
        {
            bool isStillSelling = await playerMovement.MakePlayerSit(1);
            if (!isStillSelling)
            {
                isSomeoneWorking = false;
                if(buyingPlayerReference != null)
                {
                    Movement buyingPlayerMovement = buyingPlayerReference.GetComponent<Movement>();
                    buyingPlayerMovement.sittingCourutineCancellationToken.Cancel();
                    buyingPlayerMovement.IsSitting.Value = false;
                }
                    
                return;
            }
            playerData.ChangeMoney(0.1f);
        }
    }


}
