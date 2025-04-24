using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.Collections;
using Unity.Netcode;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static LandScript;

[RequireComponent(typeof(PlayerData))]
public class LeaderMenu : NetworkBehaviour
{
    [SerializeField] GameObject singularShopItemUI;
    [SerializeField] GameObject singularLandUnitUI;
    [SerializeField] GameObject buildMenuUI;
    [SerializeField] GameObject upgradeShopMenuUI;

    PlayerData playerData;
    Menu menuManager;

    GameObject leaderMenu;

    GameObject shopManagmentItemsContainer;
    GameObject landManagmentItemsContainer;
    GameObject currentPopUpMenu;

    Transform buildShopMenuUI;
    Transform buildHouseMenuUI;

    int numberOfShopItems;
    bool isMenuSetUp = false;
    Button taxRateApproveButton;
    TMP_InputField taxRateInputField;

    readonly NetworkVariable<ItemData.ItemProperties> selectedShopSoldItemProperties = new(); //Defined to make BuildOnSingularPlotServerRpc not take this as argument and to (in future) make, so this settings saves between building menus
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
            AddItemToLandManagmentPanelClientRpc(land.menuXPos.Value, land.menuYPos.Value, land.menuDisplayText.Value, landObjectId, land.BuildingType);
        }
    }

    //TODO: MERGE THIS FUNCTION WITH UpdateLandTileUIOwnerRpc();
    [Rpc(SendTo.Owner)]
    void AddItemToLandManagmentPanelClientRpc(int xPos, int yPos, FixedString128Bytes text, ulong landObjectId, Building building)
    {
        int offset = 100; //Offset between items
        int firstItemXOffset = 50;
        string textName = "Text";
        string buildButtonName = "BuildButton";
        string destroyButtonName = "DestroyButton";
        string upgradeButtonName = "UpgradeButton";
        GameObject singularLandTileUI = Instantiate(singularLandUnitUI, landManagmentItemsContainer.transform);
        singularLandTileUI.name = "Land Tile " + xPos.ToString() + " " + yPos.ToString();
        singularLandTileUI.transform.position = new Vector2(-xPos * offset - firstItemXOffset, yPos * offset);
        singularLandTileUI.transform.Find(textName).GetComponent<TMP_Text>().text = text.ToString();

        Button buildButton = singularLandTileUI.transform.Find(buildButtonName).GetComponent<Button>();
        buildButton.onClick.AddListener(() =>
            DisplayBuildMenu(landObjectId)
        );

        Button destroyButton = singularLandTileUI.transform.Find(destroyButtonName).GetComponent<Button>();
        destroyButton.onClick.AddListener(() =>
        {
            DestroyBuildingOnPlotServerRpc(landObjectId);
        });

        Button upgradeButton = singularLandTileUI.transform.Find(upgradeButtonName).GetComponent<Button>();
        upgradeButton.onClick.AddListener(() =>
            DisplayShopUpgradeMenu(landObjectId)
        );

        DisplayLandUIButtons(singularLandTileUI, building);
    }

    void DisplayLandUIButtons(GameObject landTileUI, Building building)
    {
        string buildButtonName = "BuildButton";
        string destroyButtonName = "DestroyButton";
        string upgradeButtonName = "UpgradeButton";
        Button buildButton = landTileUI.transform.Find(buildButtonName).GetComponent<Button>();
        Button destroyButton = landTileUI.transform.Find(destroyButtonName).GetComponent<Button>();
        Button upgradeButton = landTileUI.transform.Find(upgradeButtonName).GetComponent<Button>();
        if (building == Building.Null)
        {
            buildButton.gameObject.SetActive(true);
            destroyButton.gameObject.SetActive(false);
            upgradeButton.gameObject.SetActive(false);
        }
        else if(building == Building.Shop)
        {
            buildButton.gameObject.SetActive(false);
            destroyButton.gameObject.SetActive(true);
            upgradeButton.gameObject.SetActive(true);
        }
        else //for building that cannot be upgraded
        {
            buildButton.gameObject.SetActive(false);
            destroyButton.gameObject.SetActive(true);
            upgradeButton.gameObject.SetActive(false);
        }
    }

    void DisplayBuildMenu(ulong landObjectId) //I need id, so I can use it in button listener
    {

        string buildShopPanelName = "BuildShopPanel";
        string buildHousePanelName = "BuildHousePanel";
        string buildShopUIActivationButtonName = "ShopButton";
        string buildHouseUIActivationButtonName = "HouseButton";

        string itemTypeDropdownName = "SoldItemTypeDropdown";
        string itemTierDropdownName = "SoldItemTierDropdown";
        string buildShopButtonName = "BuildButton";
        //Parents are different in this object (then in case above)
        string buildHouseButtonName = "BuildButton";
        if (currentPopUpMenu != null)
            Destroy(currentPopUpMenu);

        //get mouse pos
        RectTransformUtility.ScreenPointToLocalPointInRectangle(leaderMenu.transform as RectTransform, Input.mousePosition, null, out Vector2 mousePosition);

        currentPopUpMenu = Instantiate(buildMenuUI, leaderMenu.transform);
        currentPopUpMenu.GetComponent<RectTransform>().anchoredPosition = mousePosition;

        buildShopMenuUI = currentPopUpMenu.transform.Find(buildShopPanelName);
        buildHouseMenuUI = currentPopUpMenu.transform.Find(buildHousePanelName);

        //Shop menu set up (maybe move to switch, but then you neet to delete listeners on building change)
        TMP_Dropdown itemTierDropdown = buildShopMenuUI.transform.Find(itemTierDropdownName).GetComponent<TMP_Dropdown>();
        foreach (ItemData.ItemTier itemTier in Enum.GetValues(typeof(ItemData.ItemTier)))
            itemTierDropdown.options.Add(new TMP_Dropdown.OptionData(itemTier.ToString()));

        TMP_Dropdown itemTypeDropdown = buildShopMenuUI.transform.Find(itemTypeDropdownName).GetComponent<TMP_Dropdown>();
        foreach (ItemData.ItemType itemType in Enum.GetValues(typeof(ItemData.ItemType)))
            if(itemType != ItemData.ItemType.Null)
                itemTypeDropdown.options.Add(new TMP_Dropdown.OptionData(itemType.ToString()));

        Button buildShopButton = buildShopMenuUI.transform.Find(buildShopButtonName).GetComponent<Button>();
        buildShopButton.onClick.AddListener(() =>
        {
            selectedShopSoldItemProperties.Value = new()
            {
                itemTier = (ItemData.ItemTier)itemTierDropdown.value,
                itemType = (ItemData.ItemType)itemTypeDropdown.value + 1 //there exists null type on index 0
            };
            BuildOnSingularPlotServerRpc(landObjectId, Building.Shop);
            Destroy(currentPopUpMenu);
        });

        Button buildHouseButton = buildHouseMenuUI.transform.Find(buildHouseButtonName).GetComponent<Button>();
        buildHouseButton.onClick.AddListener(() =>
        {
            BuildOnSingularPlotServerRpc(landObjectId, Building.House);
            Destroy(currentPopUpMenu);
        });


        currentPopUpMenu.transform.Find(buildShopUIActivationButtonName).GetComponent<Button>().onClick.AddListener(
            () => ChangeBuildingBuildMenu(Building.Shop));

        currentPopUpMenu.transform.Find(buildHouseUIActivationButtonName).GetComponent<Button>().onClick.AddListener(
            () => ChangeBuildingBuildMenu(Building.House));

        buildShopMenuUI.gameObject.SetActive(false);
        buildHouseMenuUI.gameObject.SetActive(false);
    }

    void ChangeBuildingBuildMenu(Building targetBuilding)
    {
        switch (targetBuilding)
        {
            case Building.Null:
                throw new Exception("Somehow targetBuilding is null, fix that");
            case Building.Shop:
                buildShopMenuUI.gameObject.SetActive(true);
                buildHouseMenuUI.gameObject.SetActive(false);
                break;
            case Building.House:
                buildShopMenuUI.gameObject.SetActive(false);
                buildHouseMenuUI.gameObject.SetActive(true);
                break;
        }

    }

    void DisplayShopUpgradeMenu(ulong landPlotId)
    {
        string itemTierDropdownName = "SoldItemTierDropdown";
        string buildButtonName = "BuildButton";

        if (currentPopUpMenu != null)
            Destroy(currentPopUpMenu);

        //get mouse pos
        RectTransformUtility.ScreenPointToLocalPointInRectangle(leaderMenu.transform as RectTransform, Input.mousePosition, null, out Vector2 mousePosition);

        currentPopUpMenu = Instantiate(upgradeShopMenuUI, leaderMenu.transform);
        currentPopUpMenu.GetComponent<RectTransform>().anchoredPosition = mousePosition;

        TMP_Dropdown itemTierDropdown = currentPopUpMenu.transform.Find(itemTierDropdownName).GetComponent<TMP_Dropdown>();
        foreach (ItemData.ItemTier itemTier in Enum.GetValues(typeof(ItemData.ItemTier)))
        {
            itemTierDropdown.options.Add(new TMP_Dropdown.OptionData(itemTier.ToString()));
        }

        Button buildButton = currentPopUpMenu.transform.Find(buildButtonName).GetComponent<Button>();
        buildButton.onClick.AddListener(() =>
        {
            ModifyShopOnSingularPlotServerRpc(landPlotId, (ItemData.ItemTier)itemTierDropdown.value);
            Destroy(currentPopUpMenu);
        });
    }

    [Rpc(SendTo.Server)]
    void DestroyBuildingOnPlotServerRpc(ulong landPlotId)
    {
        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(landPlotId, out NetworkObject foundObject)) //find land object
            foundObject.transform.GetComponent<LandScript>().DestroyBuilding();
    }

    [Rpc(SendTo.Server)]
    void ModifyShopOnSingularPlotServerRpc(ulong landPlotId, ItemData.ItemTier itemTier)
    {
        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(landPlotId, out NetworkObject landObject)) //find land object
        {
            LandScript landScript = landObject.transform.GetComponent<LandScript>();
            Shop shop = landScript.BuildingOnLand.GetComponent<Shop>();
            ItemData.ItemProperties newItem = shop.SoldItem;
            newItem.itemTier = itemTier;
            GameManager.Instance.TownData[playerData.TownId.Value].OnLandChange.Invoke(landScript.menuXPos.Value, landScript.menuYPos.Value, LandScript.Building.Shop, $"{newItem.itemTier} {newItem.itemType} Shop");
            shop.SetUpShop(newItem);
        }
    }

    [Rpc(SendTo.Server)]
    void BuildOnSingularPlotServerRpc(ulong landPlotId, Building buildingToBuild) //TO DO: CHANGE SO THIS SUPPORTS OTHER BUILDINGS (probably by setting building specific vars outside this function)
    {
        if (playerData.Role.Value != PlayerData.PlayerRole.Leader)
            throw new Exception("Sus behaviour, non leader is trying to manage land plot" + playerData.Role.Value.ToString() + " " + playerData.TownId.Value.ToString() + " " + landPlotId.ToString());

        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(landPlotId, out NetworkObject foundObject))
        {
            LandScript landScript = foundObject.GetComponent<LandScript>();
            if (playerData.TownId.Value != landScript.townId.Value)
                throw new Exception("Sus behaviour, leader from different town is trying to modify other towns plot");

            switch (buildingToBuild)
            {
                case Building.Null:
                    throw new Exception("You want to build Null dum dum, fix that");
                case Building.Shop:
                    //TODO: Make logic to how much money building costs
                    /*
                    bool didBuy = playerData.ChangeMoney(-10);
                    if (!didBuy)
                    {
                        transform.GetComponent<PlayerUI>().DisplayErrorOwnerRpc("Get more money before building this shop!");
                        return;
                    }*/
                    landScript.BuildShopOnLand(selectedShopSoldItemProperties.Value);
                    break;
                case Building.House:
                    landScript.BuildHouseOnLand();
                    break;
                default:
                    throw new Exception("You want to build " + buildingToBuild.ToString() + ", but it is not coded yet");
            }
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
                GetComponent<ObjectInteraction>().canInteract = false;
                menuManager.amountOfDisplayedMenus++;
                if (currentPopUpMenu != null)
                    Destroy(currentPopUpMenu);
            }
            else
            {
                menuManager.ResumeGame(false);
                GetComponent<Movement>().blockRotation = false;
                GetComponent<ObjectInteraction>().canInteract = true;
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

        DisplayLandUIButtons(landUIElement.gameObject, building);
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
