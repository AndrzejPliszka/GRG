using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.Collections;
using Unity.Netcode;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(PlayerData))]
public class LeaderMenu : NetworkBehaviour
{
    [SerializeField] GameObject singularShopItemUI;
    [SerializeField] GameObject singularLandUnitUI;
    [SerializeField] GameObject buildMenuUI;

    PlayerData playerData;
    Menu menuManager;

    GameObject leaderMenu;

    GameObject shopManagmentItemsContainer;
    GameObject landManagmentItemsContainer;
    GameObject currentPopUpMenu;
    int numberOfShopItems;
    bool isMenuSetUp = false;
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

    [Rpc(SendTo.Server)]
    void SetUpLandManagmentPanelServerRpc()
    {
        if (!IsServer) { throw new Exception("This function uses server side only TownData. It needs to be called on server"); }
        GameManager.Instance.TownData[playerData.TownId.Value].OnLandChange += UpdateLandTileUIOwnerRpc;
        List<LandScript> landInTown = GameManager.Instance.TownData[playerData.TownId.Value].landInTown;
        foreach (LandScript land in landInTown)
        {
            ulong landObjectId = land.gameObject.GetComponent<NetworkObject>().NetworkObjectId;
            AddItemToLandManagmentPanelClientRpc(land.menuXPos.Value, land.menuYPos.Value, land.menuDisplayText.Value, landObjectId);
        }
    }

    [Rpc(SendTo.Owner)]
    void AddItemToLandManagmentPanelClientRpc(int xPos, int yPos, FixedString128Bytes text, ulong landObjectId) //numberOfItemsBefore used for upper padding
    {
        int offset = 100; //Offset between items
        int firstItemXOffset = 50;
        string textName = "Text";
        string buttonName = "Button";
        GameObject singularLandTileUI = Instantiate(singularLandUnitUI, landManagmentItemsContainer.transform);
        singularLandTileUI.name = "Land Tile " + xPos.ToString() + " " + yPos.ToString();
        singularLandTileUI.transform.position = new Vector2(-xPos * offset - firstItemXOffset, yPos * offset);
        singularLandTileUI.transform.Find(textName).GetComponent<TMP_Text>().text = text.ToString();
        singularLandTileUI.transform.Find(buttonName).GetComponent<Button>().onClick.AddListener(() =>
            DisplayBuildMenu(landObjectId)
        );
    }

    void DisplayBuildMenu(ulong landObjectId) //I need id, so I can use it in button listener
    {
        string itemTypeDropdownName = "SoldItemTypeDropdown";
        string itemTierDropdownName = "SoldItemTierDropdown";
        string buildButtonName = "BuildButton";

        if (currentPopUpMenu != null)
            Destroy(currentPopUpMenu);

        //get mouse pos
        RectTransformUtility.ScreenPointToLocalPointInRectangle(leaderMenu.transform as RectTransform, Input.mousePosition, null, out Vector2 mousePosition);

        currentPopUpMenu = Instantiate(buildMenuUI, leaderMenu.transform);
        currentPopUpMenu.GetComponent<RectTransform>().anchoredPosition = mousePosition;

        TMP_Dropdown itemTierDropdown = currentPopUpMenu.transform.Find(itemTierDropdownName).GetComponent<TMP_Dropdown>();
        foreach (ItemData.ItemTier itemTier in Enum.GetValues(typeof(ItemData.ItemTier)))
            itemTierDropdown.options.Add(new TMP_Dropdown.OptionData(itemTier.ToString()));

        TMP_Dropdown itemTypeDropdown = currentPopUpMenu.transform.Find(itemTypeDropdownName).GetComponent<TMP_Dropdown>();
        foreach (ItemData.ItemType itemType in Enum.GetValues(typeof(ItemData.ItemType)))
            if(itemType != ItemData.ItemType.Null)
                itemTypeDropdown.options.Add(new TMP_Dropdown.OptionData(itemType.ToString()));

        Button buildButton = currentPopUpMenu.transform.Find(buildButtonName).GetComponent<Button>();
        buildButton.onClick.AddListener(() =>
        {
            ItemData.ItemProperties itemProperties = new()
            {
                itemTier = (ItemData.ItemTier)itemTierDropdown.value,
                itemType = (ItemData.ItemType)itemTypeDropdown.value + 1 //there exists null type on index 0
            };
            ManageSingularPlotServerRpc(landObjectId, itemProperties);
            Destroy(currentPopUpMenu);
        });

    }

    [Rpc(SendTo.Server)]
    void ManageSingularPlotServerRpc(ulong landPlotId, ItemData.ItemProperties itemProperties) //TO DO: CHANGE SO THIS SUPPORTS OTHER BUILDINGS (probably by setting building specific vars outside this function)
    {
        if (playerData.Role.Value != PlayerData.PlayerRole.Leader)
            throw new Exception("Sus behaviour, non leader is trying to manage land plot" + playerData.Role.Value.ToString() + " " + playerData.TownId.Value.ToString() + " " + landPlotId.ToString());

        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(landPlotId, out NetworkObject foundObject))
        {
            LandScript landScript = foundObject.GetComponent<LandScript>();
            if (playerData.TownId.Value != landScript.townId.Value)
                throw new Exception("Sus behaviour, leader from different town is trying to modify other towns plot");
            //TODO: Make logic to how much money building costs
            /*
            bool didBuy = playerData.ChangeMoney(-10);
            if (!didBuy)
            {
                transform.GetComponent<PlayerUI>().DisplayErrorOwnerRpc("Get more money before building this shop!");
                return;
            }*/
            landScript.BuildShopOnLand(itemProperties);
        }
    }
    [Rpc(SendTo.Server)]
    void SetUpShopManagmentPanelServerRpc()
    {
        if(!IsServer) { throw new Exception("This function uses server side only TownData. It needs to be called on server"); }
        List<Shop> shopsControlledByLeader = GameManager.Instance.TownData[playerData.TownId.Value].shopsControlledByLeader;
        List<ItemData.ItemProperties> itemsSoldByShops = new();
        foreach(Shop shop in shopsControlledByLeader)
        {
            ItemData.ItemProperties itemProperties = shop.SoldItem;
            if (itemsSoldByShops.Contains(itemProperties) || itemProperties.itemType == ItemData.ItemType.Null)
                continue;
            itemsSoldByShops.Add(itemProperties);
            AddItemToShopManagmentPanelClientRpc(itemProperties, numberOfShopItems);
            numberOfShopItems++;
        }
    }

    [Rpc(SendTo.Owner)]
    public void AddItemToShopManagmentPanelClientRpc(ItemData.ItemProperties soldItemProperties, int numberOfItemsBefore) //numberOfItemsBefore used for upper padding
    {
        int offset = -100; //Offset between items
        string textName = "Text";
        string inputFieldName = "InputField";
        string buttonName = "Button";
        GameObject singularShopUIElement = Instantiate(singularShopItemUI, shopManagmentItemsContainer.transform);
        singularShopUIElement.GetComponent<RectTransform>().anchoredPosition = new Vector3(0, offset * numberOfItemsBefore, 0);
        singularShopUIElement.transform.Find(textName).GetComponent<TMP_Text>().text = $"{soldItemProperties.itemTier} {soldItemProperties.itemType}";
        TMP_InputField inputField = singularShopUIElement.transform.Find(inputFieldName).GetComponent<TMP_InputField>();
        singularShopUIElement.transform.Find(buttonName).GetComponent<Button>().onClick.AddListener(() => 
            ChangePriceOfItemServerRpc(soldItemProperties, inputField.text != "" ? float.Parse(inputField.text) : 0));
    }

    //TODO: INSTEAD OF THIS MAYBE USE EVENT
    [Rpc(SendTo.Server)]
    void ChangePriceOfItemServerRpc(ItemData.ItemProperties soldItem, float newPrice)
    {
        Debug.Log("Cena jest zmianiana na " + newPrice);
        if (playerData.Role.Value != PlayerData.PlayerRole.Leader)
            throw new Exception("Sus behaviour, non leader is trying to change item prices");
        GameManager.TownProperties townData = GameManager.Instance.TownData[playerData.TownId.Value];

        if (!townData.itemPrices.ContainsKey(soldItem))
            townData.itemPrices.Add(soldItem, newPrice);
        else
            townData.itemPrices[soldItem] = newPrice;

        List<Shop> shopList = townData.shopsControlledByLeader;
        foreach (Shop shop in shopList)
            if (shop.SoldItem.Equals(soldItem)) //using Equals() == is not defined for ItemProperties
                shop.Price = newPrice;
    }

    public void Update()
    {
        if (!isMenuSetUp && Input.GetKeyDown(KeyCode.F) && playerData.Role.Value == PlayerData.PlayerRole.Leader)
        {
            SetUpShopManagmentPanelServerRpc();
            SetUpLandManagmentPanelServerRpc();
            isMenuSetUp = true;
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
                if (currentPopUpMenu != null)
                    Destroy(currentPopUpMenu);
            }
            else
            {
                menuManager.ResumeGame(false);
                GetComponent<Movement>().blockRotation = false;
            }

            leaderMenu.SetActive(shouldDisplay);
        }
    }

    [Rpc(SendTo.Owner)]
    void UpdateLandTileUIOwnerRpc(int landTileXPos, int landTileYPos, LandScript.Building building, FixedString128Bytes updatedText)
    {
        string textName = "Text";
        Transform landUIElement = landManagmentItemsContainer.transform.Find($"Land Tile {landTileXPos} {landTileYPos}");
        landUIElement.Find(textName).GetComponent<TMP_Text>().text = updatedText.ToString();
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

    public override void OnDestroy()
    {
        GameManager.Instance.TownData[playerData.TownId.Value].OnLandChange -= UpdateLandTileUIOwnerRpc;
    }
}
