using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(PlayerData))]
public class LeaderMenu : NetworkBehaviour
{
    [SerializeField] GameObject SingularShopItemUI;
    [SerializeField] GameObject SingularLandUnitUI;

    PlayerData playerData;
    Menu menuManager;

    GameObject leaderMenu;

    GameObject shopManagmentItemsContainer;
    GameObject landManagmentItemsContainer;
    int numberOfShopItems;

    Button taxRateApproveButton;
    TMP_InputField taxRateInputField;
    public override void OnNetworkSpawn()
    {
        playerData = GetComponent<PlayerData>();
        menuManager = GameObject.Find("Canvas").GetComponent<Menu>();
        shopManagmentItemsContainer = GameObject.Find("ShopManagmentItemsContainer");
        landManagmentItemsContainer = GameObject.Find("LandManagmentItemsContainer");
        if (!IsOwner) { return; }
        taxRateApproveButton = GameObject.Find("TaxApproveButton").GetComponent<Button>();
        taxRateInputField = GameObject.Find("TaxInputField").GetComponent<TMP_InputField>();

        leaderMenu = GameObject.Find("LeaderMenu");
        taxRateApproveButton.onClick.AddListener(OnTaxRateApproveButtonClick);

        leaderMenu.SetActive(false);
    }

    void SetUpLandManagmentPanel()
    {
        if (!IsServer) { throw new Exception("This function uses server side only TownData. It needs to be called on server"); }
        List<LandScript> landInTown = GameManager.Instance.TownData[playerData.TownId.Value].landInTown;
        foreach (LandScript land in landInTown)
        {
            AddItemToLandManagmentPanelClientRpc(land.menuXPos, land.menuYPos, land.menuDisplayText);
        }
    }

    [Rpc(SendTo.Owner)]
    void AddItemToLandManagmentPanelClientRpc(int xPos, int yPos, string text) //numberOfItemsBefore used for upper padding
    {
        int offset = 100; //Offset between items
        int firstItemXOffset = 50;
        string textName = "Text";
        GameObject singularLandTileUI = Instantiate(SingularLandUnitUI, landManagmentItemsContainer.transform);
        singularLandTileUI.transform.position = new Vector2(xPos * offset + firstItemXOffset, yPos * offset);
        singularLandTileUI.transform.Find(textName).GetComponent<TMP_Text>().text = text;

    }

    void SetUpShopManagmentPanel()
    {
        if(!IsServer) { throw new Exception("This function uses server side only TownData. It needs to be called on server"); }
        List<Shop> shopsControlledByLeader = GameManager.Instance.TownData[playerData.TownId.Value].shopsControlledByLeader;
        List<ItemData.ItemProperties> itemsSoldByShops = new();
        foreach(Shop shop in shopsControlledByLeader)
        {
            ItemData.ItemProperties itemProperties = shop.SoldItem;
            if (itemsSoldByShops.Contains(itemProperties))
                continue;
            itemsSoldByShops.Add(itemProperties);
            AddItemToShopManagmentPanelClientRpc(itemProperties, numberOfShopItems);
            numberOfShopItems++;
        }
    }

    [Rpc(SendTo.Owner)]
    void AddItemToShopManagmentPanelClientRpc(ItemData.ItemProperties soldItemProperties, int numberOfItemsBefore) //numberOfItemsBefore used for upper padding
    {
        int offset = -100; //Offset between items
        string textName = "Text";
        string inputFieldName = "InputField";
        string buttonName = "Button";
        GameObject singularShopItemUI = Instantiate(SingularShopItemUI, shopManagmentItemsContainer.transform);
        singularShopItemUI.GetComponent<RectTransform>().anchoredPosition = new Vector3(0, offset * numberOfItemsBefore, 0);
        singularShopItemUI.transform.Find(textName).GetComponent<TMP_Text>().text = $"{soldItemProperties.itemTier} {soldItemProperties.itemType}";
        TMP_InputField inputField = singularShopItemUI.transform.Find(inputFieldName).GetComponent<TMP_InputField>();
        singularShopItemUI.transform.Find(buttonName).GetComponent<Button>().onClick.AddListener(() => 
            ChangePriceOfItemServerRpc(soldItemProperties, inputField.text != "" ? float.Parse(inputField.text) : 0));
    }

    //TODO: INSTEAD OF THIS MAYBE USE EVENT
    [Rpc(SendTo.Server)]
    void ChangePriceOfItemServerRpc(ItemData.ItemProperties soldItem, float newPrice)
    {
        Debug.Log("Cena jest zmianiana na " + newPrice);
        if (playerData.Role.Value != PlayerData.PlayerRole.Leader)
            throw new Exception("Sus behaviour, non leader is trying to change item prices");

        List<Shop> shopList = GameManager.Instance.TownData[playerData.TownId.Value].shopsControlledByLeader;
        foreach (Shop shop in shopList)
            if (shop.SoldItem.Equals(soldItem)) //using Equals() == is not defined for ItemProperties
                shop.Price = newPrice;
    }

    public void Update()
    {
        if (IsServer && numberOfShopItems == 0 && Input.GetKeyDown(KeyCode.F) && playerData.Role.Value == PlayerData.PlayerRole.Leader)
        {
            SetUpShopManagmentPanel();
            SetUpLandManagmentPanel();
        }
        if (!IsOwner) { return; }

        if (Input.GetKeyDown(KeyCode.F) && playerData.Role.Value == PlayerData.PlayerRole.Leader) {
            bool shouldDisplay = !leaderMenu.activeSelf;

            if (shouldDisplay)
            {
                Cursor.visible = true;
                Cursor.lockState = CursorLockMode.None;
                GetComponent<Movement>().blockRotation = true;
                menuManager.amountOfDisplayedMenus++;
            }
            else
            {
                menuManager.ResumeGame(false);
                GetComponent<Movement>().blockRotation = false;
            }

            leaderMenu.SetActive(shouldDisplay);
        }
    }

    void OnTaxRateApproveButtonClick()
    {
        float targetTaxRate = Convert.ToInt16(taxRateInputField.text) / 100f;
        ChangeTaxServerRpc(targetTaxRate);
    }

    //This method is here because it is directly related to functions of menu, if needed elsewhere move it from here to GameManager or to dedicated function.
    [Rpc(SendTo.Server)]
    void ChangeTaxServerRpc(float tax)
    {
        if (playerData.Role.Value != PlayerData.PlayerRole.Leader)
            throw new Exception("Sus behaviour, non leader is trying to change tax");

        GameManager.Instance.TownData[playerData.TownId.Value].TaxRate = tax;
    }
}
