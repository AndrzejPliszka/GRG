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
    [SerializeField] TMP_Text workerInfoText;
    [SerializeField] GameObject customerChair;
    [SerializeField] GameObject workerChair;

    [SerializeField] ItemData.ItemTier soldItemTier;
    [SerializeField] ItemData.ItemType soldItemType;

    [SerializeField] bool isUsingFood; //Change into enum if you want to add more raw materials (or maybe dictionary with different materials and amounts?)
    [SerializeField] int amountOfFoodNeeded;

    //server side variables (not used on client)
    [SerializeField] int price;
    readonly NetworkVariable<bool> isSomeoneWorking = new(false); //readonly, because it is best practise here
    GameObject buyingPlayerReference; //this refrence is needed to force buyingPlayer to stop sitting if workingPlayer no longer works

    public ItemData.ItemProperties ItemToSell { private set; get; } = new();

    public override void OnNetworkSpawn()
    {
        if(soldItemType != ItemData.ItemType.Null )
            ItemToSell = new ItemData.ItemProperties() { itemTier = soldItemTier, itemType = soldItemType };

        shopText.text = $"{ItemToSell.itemTier} {ItemToSell.itemType}\n for {price}$";
        ChangeCurretInfoTextRpc(isSomeoneWorking.Value, isSomeoneWorking.Value);
        isSomeoneWorking.OnValueChanged += ChangeCurretInfoTextRpc;

    }

    public async void BuyFromShop(GameObject buyingPlayer)
    {
        if (!IsServer) { throw new Exception("Client cannot buy anything by himself, call this method on server!"); } 
        PlayerData playerData = buyingPlayer.GetComponent<PlayerData>();
        Movement playerMovement = buyingPlayer.GetComponent<Movement>();
        PlayerUI playerUI = buyingPlayer.GetComponent<PlayerUI>();

        if (playerMovement == null || playerData == null)
            Debug.LogWarning("Client does not have playerMovement or playerData script and is trying to buy something!");

        if(playerData.Money.Value < price)
        {
            if (playerUI)
                playerUI.DisplayErrorOwnerRpc("Go get some money first!");
            return;
        }

        if (playerData.FindFreeInventorySlot() == -1)
        {
            if (playerUI)
                playerUI.DisplayErrorOwnerRpc("Inventory full!");
            return;
        }

        if (!isSomeoneWorking.Value)
        {
            if (playerUI)
                playerUI.DisplayErrorOwnerRpc("Nobody is working in the shop!");
            return;
        }

        if (buyingPlayerReference != null)
        {
            if (playerUI)
                playerUI.DisplayErrorOwnerRpc("Somebody is already buying stuff here!");
            return;
        }

        if (isUsingFood && GameManager.Instance.TownData[townId].foodSupply - amountOfFoodNeeded < 0)
        {
            if (playerUI)
                playerUI.DisplayErrorOwnerRpc("There is no food in town storage!");
            return;
        }

        buyingPlayerReference = buyingPlayer;
        playerMovement.TeleportPlayerToPosition(customerChair.transform.position + new Vector3(0, 1, 0.5f));
        bool didBuy = await playerMovement.MakePlayerSit(5);
        buyingPlayerReference = null;
        if (!didBuy || (isUsingFood && GameManager.Instance.TownData[townId].foodSupply - amountOfFoodNeeded < 0)) //if there was food when started buying, but someone used it and there is no food in storage you wont be able to buy
            return;
        bool didAddToInventory = playerData.AddItemToInventory(ItemToSell);
        if (didAddToInventory)
        {
            if (isUsingFood)
                GameManager.Instance.ChangeFoodSupply(-amountOfFoodNeeded);
            playerData.ChangeMoney(-price);
        }
            
        
    }

    public async void WorkInShop(GameObject workingPlayer)
    {
        if (!IsServer) { throw new Exception("Client cannot do anything by himself, call this method on server!"); }
        PlayerData playerData = workingPlayer.GetComponent<PlayerData>();
        Movement playerMovement = workingPlayer.GetComponent<Movement>();
        PlayerUI playerUI = workingPlayer.GetComponent<PlayerUI>();
        if (playerMovement == null || playerData == null)
            Debug.LogWarning("Client does not have playerMovement or playerData script and is trying to buy something!");

        if (isSomeoneWorking.Value)
        {
            if (playerUI)
                playerUI.DisplayErrorOwnerRpc("Somebody is already working here!");
            return;
        }

        playerMovement.TeleportPlayerToPosition(workerChair.transform.position + workerChair.transform.localRotation * new Vector3(0, 1, 0.5f));
        isSomeoneWorking.Value = true;
        while (true)
        {
            bool isStillSelling = await playerMovement.MakePlayerSit(1);
            if (!isStillSelling)
            {
                isSomeoneWorking.Value = false;
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

    [Rpc(SendTo.Everyone)]
    private void ChangeCurretInfoTextRpc(bool wasOpen, bool isOpen)
    {
        if (isOpen)
        {
            workerInfoText.color = Color.green;
            workerInfoText.text = "Open";
        }
        else
        {
            workerInfoText.color = Color.red;
            workerInfoText.text = "Closed";
        }

    }
}
