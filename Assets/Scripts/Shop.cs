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

    [SerializeField] bool isBuyingRole;
    [SerializeField] PlayerData.PlayerRole playerRole; //is not used when isChangingRole = true

    [SerializeField] bool noWorkerRequiredOnEmptyTown; //makes that if there is no people in town, you can still buy things (used admission building, so first player can join town)

    public string HoverText { get; private set; }
    //server side variables (not used on client)
    [SerializeField] float _price;
    public float Price {
        get => _price;
        set
        {
            if (_price != value)
            {
                _price = value < 0 ? 0 : value;
                _price = MathF.Round(_price, 2);
                OnShopChange.Invoke(_price, SoldItem);
            }
        }
    }
    Action<float, ItemData.ItemProperties> OnShopChange;
    [SerializeField] bool isPriceChangable;

    readonly NetworkVariable<bool> isShopOpen = new(); //readonly, because it is best practise here
    GameObject buyingPlayerReference; //this refrence is needed to force buyingPlayer to stop sitting if workingPlayer no longer works

    public ItemData.ItemProperties SoldItem { private set; get; } = new();

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            isShopOpen.Value = noWorkerRequiredOnEmptyTown;
            if (noWorkerRequiredOnEmptyTown)
                GameManager.Instance.OnPlayerTownChange += UpdateIsShopOpen;
            if (isPriceChangable)
                GameManager.Instance.TownData[townId].shopsControlledByLeader.Add(this);

            OnShopChange += ChangeCurrentItemTextRpc;
        }

        if (soldItemType != ItemData.ItemType.Null )
            SoldItem = new ItemData.ItemProperties() { itemTier = soldItemTier, itemType = soldItemType };

        if ((SoldItem.itemType != ItemData.ItemType.Null || Price != 0) || isBuyingRole)
        {
            ChangeCurrentItemTextRpc(Price, SoldItem);
            ChangeCurretOpenInfoTextRpc(isShopOpen.Value, isShopOpen.Value);
        }
        isShopOpen.OnValueChanged += ChangeCurretOpenInfoTextRpc;

    }

    //this function exists, so if noWorkerRequiredOnEmptyTown == true and there is empty town, isShopOpen will always be true (making this is less complicated then setter getter)
    public void UpdateIsShopOpen(GameObject player, int oldPlayerTownId,int currentPlayerTownId)
    {
        if (currentPlayerTownId != townId)
            return;

        if (GameManager.Instance.TownData[townId].townMembers.Count == 0)
            isShopOpen.Value = true;
        else
            isShopOpen.Value = false;
    }


    public void SetUpShop(ItemData.ItemProperties itemToSell)
    {
        if (!IsServer) { throw new Exception("Only server can modify shops!"); }

        GameManager.TownProperties townData = GameManager.Instance.TownData[townId];
        SoldItem = itemToSell; //used to set up global var used elsewhere 
        ChangeCurrentItemTextRpc(Price, itemToSell);

        if (!townData.itemPrices.ContainsKey(itemToSell))
        {
            townData.itemPrices.Add(itemToSell, 0);
            LeaderMenu leaderMenu = townData.townMembers[0].GetComponent<LeaderMenu>();
            if(leaderMenu)
                leaderMenu.AddItemToShopManagmentPanelClientRpc(itemToSell, townData.itemPrices.Count);
            
        }
        else
            Price = townData.itemPrices[itemToSell];

        townData.shopsControlledByLeader.Add(this);
    }

    public async void BuyFromShop(GameObject buyingPlayer)
    {
        if (!IsServer) { throw new Exception("Client cannot buy anything by himself, call this method on server!"); } 
        PlayerData playerData = buyingPlayer.GetComponent<PlayerData>();
        Movement playerMovement = buyingPlayer.GetComponent<Movement>();
        PlayerUI playerUI = buyingPlayer.GetComponent<PlayerUI>();

        if (playerMovement == null || playerData == null)
            Debug.LogWarning("Client does not have playerMovement or playerData script and is trying to buy something!");

        if (playerMovement.IsSitting.Value)
        {
            if (playerUI)
                playerUI.DisplayErrorOwnerRpc("You cannot buy things\nwhen you are working!");
            return;
        }

        if(playerData.Money.Value < Price)
        {
            if (playerUI)
                playerUI.DisplayErrorOwnerRpc("Go get some money first!");
            return;
        }

        if (playerData.FindFreeInventorySlot() == -1  && SoldItem.itemType != ItemData.ItemType.Null)
        {
            if (playerUI)
                playerUI.DisplayErrorOwnerRpc("Inventory full!");
            return;
        }
        if (!isShopOpen.Value && !(noWorkerRequiredOnEmptyTown && GameManager.Instance.TownData[townId].townMembers.Count == 0))
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

        if (isUsingFood && GameManager.Instance.TownData[townId].FoodSupply - amountOfFoodNeeded < 0)
        {
            if (playerUI)
                playerUI.DisplayErrorOwnerRpc("There is no food in town storage!");
            return;
        }

        if(isBuyingRole && (buyingPlayer.GetComponent<PlayerData>().Role.Value >= playerRole))
        {
            if (playerUI)
                playerUI.DisplayErrorOwnerRpc("You already have this or better role!");
            return;
        }

        buyingPlayerReference = buyingPlayer; //used only in checking if someone is buying in the shop, use buyingPlayer otherwise
        playerMovement.TeleportPlayerToPosition(customerChair.transform.position + transform.localRotation * customerChair.transform.localRotation * new Vector3(0, 0.75f, 0.5f));
        if(playerUI)
            playerUI.DisplayProgressBarOwnerRpc(5);
        bool didBuy = await playerMovement.MakePlayerSit(5);
        buyingPlayerReference = null;
        //if there was food when started buying, but someone used it and there is no food in storage you wont be able to buy
        if (!didBuy || (isUsingFood && GameManager.Instance.TownData[townId].FoodSupply - amountOfFoodNeeded < 0))
        {
            if (playerUI)
                playerUI.ForceStopProgressBarOwnerRpc();
            return;
        }
            

        if (SoldItem.itemType != ItemData.ItemType.Null)
            didBuy = playerData.AddItemToInventory(SoldItem);

        //did buy is true by default at this point, so I dont need to check it
        if (isBuyingRole) 
            GameManager.Instance.ChangePlayerAffiliation(buyingPlayer, playerRole, townId); 

        if (didBuy)
        {
            if (isUsingFood)
                GameManager.Instance.ChangeFoodSupply(-amountOfFoodNeeded, townId);

            playerData.ChangeMoney(-Price);
            GameManager.Instance.TownData[townId].townMembers[0].GetComponent<PlayerData>().ChangeMoney(Price);
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

        if (isShopOpen.Value)
        {
            if (playerUI)
                playerUI.DisplayErrorOwnerRpc("Somebody is already working here!");
            return;
        }

        playerMovement.TeleportPlayerToPosition(workerChair.transform.position + transform.localRotation * workerChair.transform.localRotation * new Vector3(0, 0.75f, 0.5f));
        isShopOpen.Value = true;
        while (true)
        {
            bool isStillSelling = await playerMovement.MakePlayerSit(1);
            if (!isStillSelling)
            {
                isShopOpen.Value = false;
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
    private void ChangeCurrentItemTextRpc(float newPrice, ItemData.ItemProperties newItem)
    {
        if (newItem.itemType != ItemData.ItemType.Null)
        {
            shopText.text = $"{newItem.itemTier} {newItem.itemType}\n for {newPrice}$";
            HoverText = $"{newItem.itemTier} {newItem.itemType} Shop";
        }
        else if (isBuyingRole == true)
        {
            shopText.text = $"Become {playerRole}\n for {Price}$";
            HoverText = $"{playerRole} Admission";
        }
        else
        {
            shopText.text = $"This shop sells nothing\n for {Price}";
            HoverText = $"Sigma Ligma Skibidi Shop";
        }
    }

    [Rpc(SendTo.Everyone)]
    private void ChangeCurretOpenInfoTextRpc(bool wasOpen, bool isOpen)
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
