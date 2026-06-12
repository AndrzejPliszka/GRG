using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using TMPro;
using Unity.Collections;
using Unity.Netcode;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using static ItemData;
[RequireComponent(typeof(PlayerData))]
[RequireComponent(typeof(ObjectInteraction))] //this is because we will use function that says what are we looking at
public class PlayerUI : NetworkBehaviour
{
    ObjectInteraction objectInteraction;
    BuildModeController buildMode;
    VoiceChat voiceChat;
    // Start is called before the first frame update
    [SerializeField] Sprite unusedInventorySlot;
    [SerializeField] Sprite usedInventorySlot;

    [SerializeField] GameObject storageTradePanel;
    [SerializeField] GameObject storageManagmentPanel;

    [SerializeField] GameObject materialDeliveryToUnbuiltBuildingPanel;
    [SerializeField] GameObject materialDeliveryPaymentManagmentPanel;

    [SerializeField] GameObject workshopWorkPanel;
    [SerializeField] GameObject workshopUpgradePanel;

    TMP_Text centerText;
    TMP_Text errorText;
    TMP_Text hungerBarText;
    TMP_Text healthBarText;
    TMP_Text moneyCount;
    TMP_Text taxRate;
    TMP_Text criminalText;
    TMP_Text woodMaterialText;
    TMP_Text foodMaterialText;
    TMP_Text stoneMaterialText;
    TMP_Text tooltipText;
    TMP_Text selectedBuildingText;
    Image hitmark;
    Image cooldownMarker;
    Image micActivityIcon;
    Image mainBuildingSlot;
    Image previousBuildingSlot;
    Image nextBuildingSlot;
    Slider hungerBar;
    Slider healthBar;
    Slider progressBar;
    Transform buildMenu;
    readonly List<GameObject> inventorySlots = new();
    Coroutine progressBarCoroutine;

    GameObject inventorySlotsContainer;
    PlayerData playerData;
    Transform playerUI;
    Menu menuManager;
    InputAction pauseInput;
    bool isPlayerSelling = true; //Used in storage trade menu, global so it saves between menus, change name when more menus are added
    public override void OnNetworkSpawn()
    {
        buildMode = GetComponent<BuildModeController>();
        playerData = GetComponent<PlayerData>();
        objectInteraction = GetComponent<ObjectInteraction>();

        playerUI = GameObject.Find("Canvas").transform.Find("PlayerUI");

        centerText = GameObject.Find("CenterText").GetComponent<TMP_Text>();
        errorText = GameObject.Find("ErrorText").GetComponent<TMP_Text>();
        hungerBarText = GameObject.Find("HungerBarText").GetComponent<TMP_Text>();
        healthBarText = GameObject.Find("HealthBarText").GetComponent<TMP_Text>();
        moneyCount = GameObject.Find("MoneyCount").GetComponent<TMP_Text>();
        taxRate = GameObject.Find("TaxRate").GetComponent<TMP_Text>();
        criminalText = GameObject.Find("CriminalText").GetComponent<TMP_Text>();
        tooltipText = GameObject.Find("Tooltips").GetComponent<TMP_Text>();

        woodMaterialText = GameObject.Find("WoodMaterialData").GetComponent<TMP_Text>();
        foodMaterialText = GameObject.Find("FoodMaterialData").GetComponent<TMP_Text>();
        stoneMaterialText = GameObject.Find("StoneMaterialData").GetComponent<TMP_Text>();

        hitmark = GameObject.Find("Hitmark").GetComponent<Image>();
        cooldownMarker = GameObject.Find("CooldownMarker").GetComponent<Image>();
        micActivityIcon = GameObject.Find("MicActivityIcon").GetComponent<Image>();

        hungerBar = GameObject.Find("HungerBar").GetComponent<Slider>();
        healthBar = GameObject.Find("HealthBar").GetComponent<Slider>();

        errorText.enabled = false; //We do this so DisplayError() works (see function)

        inventorySlotsContainer = GameObject.Find("InventorySlots");
        for (int i = 0; i < inventorySlotsContainer.transform.childCount; i++) {
            inventorySlots.Add(inventorySlotsContainer.transform.GetChild(i).gameObject);
        }

        if (IsOwner)
        {
            mainBuildingSlot = GameObject.Find("SelectedBuildingSlot").transform.GetChild(0).GetComponent<Image>();
            previousBuildingSlot = GameObject.Find("PreviousBuildingSlot").transform.GetChild(0).GetComponent<Image>();
            nextBuildingSlot = GameObject.Find("NextBuildingSlot").transform.GetChild(0).GetComponent<Image>();
            progressBar = GameObject.Find("ProgressBar").GetComponent<Slider>();
            buildMenu = GameObject.Find("BuildUI").transform;
            selectedBuildingText = buildMenu.Find("SelectedBuildingText").GetComponent<TMP_Text>();
            if (buildMode)
                selectedBuildingText.text = buildMode.CurrentBuildingType.ToString();
            buildMenu.gameObject.SetActive(false);
            progressBar.gameObject.SetActive(false);
            playerData.SelectedInventorySlot.OnValueChanged += ChangeInventorySlot;
            playerData.Inventory.OnListChanged += DisplayInventory;
            playerData.Hunger.OnValueChanged += ModifyHungerBar;
            playerData.Health.OnValueChanged += ModifyHealthBar;
            playerData.Money.OnValueChanged += ModifyMoneyCount;
            playerData.CriminalCooldown.OnValueChanged += DisplayIsCriminalText;
            playerData.JailCooldown.OnValueChanged += DisplayInPrisonText;
            playerData.OwnedMaterials.OnListChanged += DisplayMaterialText;
            buildMode.IsBuildModeActive.OnValueChanged += SetUpBuildMenu;
        }

        if (IsServer)
        {
            //Here are only server side events which call RPCs
            objectInteraction.OnHittingSomething += hitGameObject => { DisplayHitmarkOwnerRpc(); };
            objectInteraction.OnPunch += DisplayCooldownCircleOwnerRpc;
            playerData.TownId.OnValueChanged += (oldTownId, newTownId) => { //MOVE TO OTHER FUNCTION IF IT GROWS TOO MUCH !!!
                if(oldTownId >= 0 && oldTownId < GameManager.Instance.TownData.Count)
                    GameManager.Instance.TownData[oldTownId].OnTaxRateChange -= ModifyTaxRateTextOwnerRpc;
                if (newTownId >= 0 && newTownId < GameManager.Instance.TownData.Count)
                {
                    ModifyTaxRateTextOwnerRpc(GameManager.Instance.TownData[newTownId].TaxRate);
                    GameManager.Instance.TownData[newTownId].OnTaxRateChange += ModifyTaxRateTextOwnerRpc;
                }
            };
        }
        

        voiceChat = gameObject.GetComponent<VoiceChat>();
        menuManager = GameManager.Instance.MenuManager;
        pauseInput = InputSystem.actions.FindAction("Pause", true);
    }

    private void FixedUpdate()
    {
        if (pauseInput.WasPressedThisFrame())
        {
            if (menuManager.isGamePaused)
                menuManager.ResumeGame(true);
            else
                menuManager.PauseGame();
        }
    }

    private void Update()
    {
        if (!IsOwner) {  return; }
        UpdateLookedAtObjectText();
        if (voiceChat)
            ModifyVoiceChatIcon(!voiceChat.IsMuted);

    }

    void DisplayMaterialText(NetworkListEvent<PlayerData.ExtendedMaterialData> listChange)
    {
        PlayerData.ExtendedMaterialData changedMaterialData = listChange.Value;
        TMP_Text modifiedText;
        switch(changedMaterialData.MaterialType)
        {
            case PlayerData.RawMaterial.Wood:
                woodMaterialText.text = $"{changedMaterialData.Amount}";
                modifiedText = woodMaterialText;
                break;
            case PlayerData.RawMaterial.Food:
                foodMaterialText.text = $"{changedMaterialData.Amount}";
                modifiedText = foodMaterialText;
                break;
            case PlayerData.RawMaterial.Stone:
                stoneMaterialText.text = $"{changedMaterialData.Amount}";
                modifiedText = stoneMaterialText;
                break;
            default:
                return;
        }
        if(changedMaterialData.Amount == changedMaterialData.MaxAmount)
            modifiedText.color = new Color(0, 255, 0, 1);
        else
            modifiedText.color = new Color(0, 0, 0, 1);
    }

    //This function will update text which tells player what is he looking at. It needs X Camera Rotation from client (in "Vector3 form") (server doesn't have camera - it is only on client)
    void UpdateLookedAtObjectText()
    {
        GameObject targetObject = objectInteraction.GetObjectInFrontOfCamera(GameObject.Find("Camera").transform.rotation.eulerAngles.x);
        UpdateTooltipText(targetObject);
        if (targetObject == null || string.IsNullOrEmpty(targetObject.tag))
        {
            centerText.text = "";
            return;
        }
        if (objectInteraction && objectInteraction.canInteract == false)
        {
            centerText.text = "";
            return;
        }

        string targetObjectTag = targetObject.tag;
        //declare these here to avoid scope issues
        Shop shopScript;
        BreakableStructure breakableStructure;
        Storage storage;
        MoneyObject moneyObject;
        House house;
        GatherableMaterial materialItem;
        UnbuiltBuilding unbuiltBuilding;
        Workshop workshop;
        int currentHealth, maxHealth;

        //If object with tag has reference to another object we want to get data from referenced object instead
        if (targetObject.TryGetComponent<ObjectReference>(out ObjectReference objectReference))
            targetObject = objectReference.objectReference;

        switch (targetObjectTag)
        {
            case "Player":
                PlayerData playerData = targetObject.GetComponent<PlayerData>();
                centerText.text = $"{playerData.Nickname.Value}\n{playerData.Health.Value}/100";
                break;
            case "Item":
                centerText.text = targetObject.GetComponent<ItemData>().itemProperties.Value.itemType.ToString();;
                break;
            case "Tree":
                breakableStructure = targetObject.GetComponent<BreakableStructure>();
                currentHealth = breakableStructure.Health.Value;
                maxHealth = breakableStructure.MaximumHealth.Value;
                centerText.text = $"Tree\n{currentHealth}/{maxHealth}";
                break;
            case "Crop":
                breakableStructure = targetObject.GetComponent<BreakableStructure>();
                currentHealth = breakableStructure.Health.Value;
                maxHealth = breakableStructure.MaximumHealth.Value;
                centerText.text = $"Crop\n{currentHealth}/{maxHealth}";
                break;
            case "Buy":
                shopScript = targetObject.transform.parent.GetComponent<Shop>();
                centerText.text = $"Buy {shopScript.HoverText[..shopScript.HoverText.LastIndexOf(' ')].TrimEnd()}"; //Display without last word which is either shop or admission
                break;
            case "Work":
                shopScript = targetObject.transform.parent.GetComponent<Shop>();
                centerText.text = $"Work in {shopScript.HoverText}";
                break;
            case "Shop":
                shopScript = targetObject.GetComponent<Shop>();
                breakableStructure = targetObject.GetComponent<BreakableStructure>();
                currentHealth = breakableStructure.Health.Value;
                maxHealth = breakableStructure.MaximumHealth.Value;
                centerText.text = $"{shopScript.HoverText}\n{currentHealth}/{maxHealth}";
                break;
            case "Storage":
                storage = targetObject.GetComponent<Storage>();
                if (storage.gameObject.TryGetComponent<Workshop>(out Workshop attachedToWorkshop))
                    centerText.text = $"{attachedToWorkshop.ItemType} Workshop Storage:";
                else
                    centerText.text = $"{PlayerData.GetNicknameOfPlayer(storage.OwnerId.Value)}'s Storage:";

                foreach (PlayerData.ExtendedMaterialData materialData in storage.StoredMaterialData)
                    centerText.text += $"\n {materialData.Amount}/{materialData.MaxAmount} of {materialData.MaterialType}";
                if (targetObject.TryGetComponent<BreakableStructure>(out breakableStructure))
                    centerText.text += $"\nHP: {breakableStructure.Health.Value}/{breakableStructure.MaximumHealth.Value}";
                break;
            case "Money":
                moneyObject = targetObject.GetComponent<MoneyObject>();
                centerText.text = $"{moneyObject.moneyAmount.Value}$";
                break;
            case "House":
                house = targetObject.GetComponent<House>();
                centerText.text = $"{house.displayedText.Value}";
                break;
            case "Parliament":
                centerText.text = $"Parliament";
                break;
            case "Rock":
                breakableStructure = targetObject.GetComponent<BreakableStructure>();
                currentHealth = breakableStructure.Health.Value;
                maxHealth = breakableStructure.MaximumHealth.Value;
                centerText.text = $"Rock\n{currentHealth}/{maxHealth}";
                break;
            case "GatherableMaterial":
                materialItem = targetObject.GetComponent<GatherableMaterial>();
                switch (materialItem.Material.Value.MaterialType)
                {
                    case PlayerData.RawMaterial.Wood:
                        centerText.text = $"Stick";
                        break;
                    case PlayerData.RawMaterial.Stone:
                        centerText.text = $"Pebble";
                        break;
                }
                break;
            case "BerryBush":
                if(targetObject.GetComponent<BerryBush>().HasBerries.Value)
                    centerText.text = $"Berry Bush";
                else
                    centerText.text = $"Bush";
                break;
            default:
                centerText.text = "";
                break;
            case "MaterialObject":
                materialItem = targetObject.GetComponent<GatherableMaterial>();
                centerText.text = materialItem.Material.Value.MaterialType switch
                {
                    PlayerData.RawMaterial.Wood => $"Wood Material",
                    PlayerData.RawMaterial.Stone => $"Stone Material",
                    PlayerData.RawMaterial.Food => $"Food Material",
                    _ => "",
                };
                break;
            case "Unbuilt":
                unbuiltBuilding = targetObject.GetComponent<UnbuiltBuilding>();
                centerText.text = $"{PlayerData.GetNicknameOfPlayer(unbuiltBuilding.OwnerId.Value)}'s \n Unfinished {unbuiltBuilding.ObjectStringDescription.Value}";
                if (targetObject.TryGetComponent<BreakableStructure>(out breakableStructure) && breakableStructure.enabled)
                    centerText.text += $"\nHP: {breakableStructure.Health.Value}/{breakableStructure.MaximumHealth.Value}";
                break;
            case "Workshop":
                workshop = targetObject.GetComponent<Workshop>();
                centerText.text = $"{PlayerData.GetNicknameOfPlayer(workshop.OwnerId.Value)}'s {workshop.ItemTier} {workshop.ItemType} Workshop";
                if (workshop.ItemMaterialCost.Count != 0)
                    centerText.text += $"\n{workshop.ItemType} Cost:";
                foreach (PlayerData.MaterialData material in workshop.ItemMaterialCost)
                    centerText.text += $"\n{material.Amount} of {material.MaterialType}";
                if (targetObject.TryGetComponent<BreakableStructure>(out breakableStructure) && breakableStructure.enabled)
                    centerText.text += $"\nHP: {breakableStructure.Health.Value}/{breakableStructure.MaximumHealth.Value}";
                break;
        }
        ;
    }

    void UpdateTooltipText(GameObject lookedAtObject)
    {
        ItemData.ItemType heldItem = playerData.Inventory[playerData.SelectedInventorySlot.Value].itemType;
        ReplaceTextWithLineStartingWith(tooltipText, "F", "");
        ReplaceTextWithLineStartingWith(tooltipText, "E", "");
        ReplaceTextWithLineStartingWith(tooltipText, "Click", "");
        ReplaceTextWithLineStartingWith(tooltipText, "Scroll", "");
        ReplaceTextWithLineStartingWith(tooltipText, "T", "");
        ReplaceTextWithLineStartingWith(tooltipText, "R", "");
        ReplaceTextWithLineStartingWith(tooltipText, "X", "");
        if (buildMode.IsBuildModeActive.Value)
        {
            ReplaceTextWithLineStartingWith(tooltipText, "Scroll", "Scroll - Change Building");
            ReplaceTextWithLineStartingWith(tooltipText, "Click", "Click - Place Building");
            ReplaceTextWithLineStartingWith(tooltipText, "R", "R - Rotate Building");
            ReplaceTextWithLineStartingWith(tooltipText, "E", "E - Change Subtype");
            ReplaceTextWithLineStartingWith(tooltipText, "B", "B - Disable Build Mode");
        }
        else
        {
            ReplaceTextWithLineStartingWith(tooltipText, "B", "B - Enable Build Mode");
            if (lookedAtObject)
            {
                
                string objectTag = lookedAtObject.tag;

                //If object with tag has reference to another object we want to get data from referenced object instead
                if (lookedAtObject.TryGetComponent<ObjectReference>(out ObjectReference objectReference))
                    lookedAtObject = objectReference.objectReference;


                switch (objectTag)
                {
                    case "Player":
                        ReplaceTextWithLineStartingWith(tooltipText, "Click", "Click - Punch");
                        break;
                    case "Item":
                        ReplaceTextWithLineStartingWith(tooltipText, "E", "E - Pick up");
                        break;
                    case "Tree":
                        if (heldItem == ItemType.Axe)
                            ReplaceTextWithLineStartingWith(tooltipText, "Click", "Click - Cut tree");
                        break;
                    case "Crop":
                        if (heldItem == ItemType.Sickle)
                            ReplaceTextWithLineStartingWith(tooltipText, "Click", "Click - Cut crop");
                        break;
                    case "Buy":
                        ReplaceTextWithLineStartingWith(tooltipText, "E", "E - buy");
                        break;
                    case "Work":
                        ReplaceTextWithLineStartingWith(tooltipText, "E", "E - sell");
                        break;
                    case "Storage":
                        if(heldItem == ItemType.Sword)
                            ReplaceTextWithLineStartingWith(tooltipText, "Click", "Click - Destroy Building");
                        if (lookedAtObject.GetComponent<Storage>().OwnerId.Value == NetworkManager.Singleton.LocalClientId) //If player is owner of storage
                            ReplaceTextWithLineStartingWith(tooltipText, "E", "E - Manage storage");
                        else
                            ReplaceTextWithLineStartingWith(tooltipText, "E", "E - Open selling menu");
                        ReplaceTextWithLineStartingWith(tooltipText, "F", "F - Sell all");
                        break;
                    case "Money":
                        ReplaceTextWithLineStartingWith(tooltipText, "E", "E - Pick up");
                        break;
                    case "Rock":
                        if (heldItem == ItemType.Pickaxe)
                            ReplaceTextWithLineStartingWith(tooltipText, "Click", "Click - Break rock");
                        break;
                    case "GatherableMaterial":
                        ReplaceTextWithLineStartingWith(tooltipText, "E", "E - Pick up");
                        break;
                    case "BerryBush":
                        ReplaceTextWithLineStartingWith(tooltipText, "E", "E - Eat");
                        ReplaceTextWithLineStartingWith(tooltipText, "F", "F - Pick up");
                        break;
                    case "MaterialObject":
                        ReplaceTextWithLineStartingWith(tooltipText, "E", "E - Pick up");
                        break;
                    case "Unbuilt":
                        UnbuiltBuilding building = lookedAtObject.GetComponent<UnbuiltBuilding>();
                        if(building.OwnerId.Value == NetworkManager.Singleton.LocalClientId)
                        {
                            if(building.IsCompletelyUnbuilt())
                                ReplaceTextWithLineStartingWith(tooltipText, "X", "X - Delete building");
                            else if(heldItem == ItemType.Sword)
                                ReplaceTextWithLineStartingWith(tooltipText, "Click", "Click - Destroy Building");
                            ReplaceTextWithLineStartingWith(tooltipText, "E", "E - Manage Construction");
                            ReplaceTextWithLineStartingWith(tooltipText, "F", "F - Open Delivery Menu");
                        }
                        else
                        {
                            ReplaceTextWithLineStartingWith(tooltipText, "E", "E - Open Delivery Menu");
                            ReplaceTextWithLineStartingWith(tooltipText, "F", "F - Deliver All Materials");
                        }
                        break;
                    case "Workshop":
                        Workshop workshop = lookedAtObject.GetComponent<Workshop>();
                        ReplaceTextWithLineStartingWith(tooltipText, "E", $"E - Create {workshop.ItemTier} {workshop.ItemType}");
                        if (workshop.OwnerId.Value == NetworkManager.Singleton.LocalClientId)
                            ReplaceTextWithLineStartingWith(tooltipText, "F", "F - Upgrade workshop");
                        break;
                    default:
                        break;

                }
            }

            if (heldItem == ItemType.Medkit)
                ReplaceTextWithLineStartingWith(tooltipText, "E", "E - Use medkit");
            if (heldItem == ItemType.Food)
                ReplaceTextWithLineStartingWith(tooltipText, "E", "E - Eat food");
            if (heldItem != ItemType.Null)
                ReplaceTextWithLineStartingWith(tooltipText, "T", "T - Throw item");

            tooltipText.text = tooltipText.text.Trim();
        }
    }
    void SetUpBuildMenu(bool wasBuildMenuActive, bool isBuildMenuActive)
    {
        if (!TryGetComponent<BuildModeController>(out var buildMode))
            return;
        if (isBuildMenuActive)
        {
            inventorySlotsContainer.SetActive(false);
            buildMenu.gameObject.SetActive(true);
            buildMode.OnSelectedBuildingChanged += UpdateSelectedBuilding;
        }
        else
        {
            inventorySlotsContainer.SetActive(true);
            buildMenu.gameObject.SetActive(false);
            buildMode.OnSelectedBuildingChanged -= UpdateSelectedBuilding;
        }
    }
    void UpdateSelectedBuilding(BuildingData.BuildingType buildingType, string subtype)
    {
        BuildingData buildingData = GameManager.Instance.BuildingData;
        mainBuildingSlot.sprite = buildingData.GetDataOfBuildingType(buildingType).buildingSprite;
        BuildingData.BuildingType previousBuilding = BuildModeController.GetAdjacentBuildingType(false, buildingType);
        previousBuildingSlot.sprite = buildingData.GetDataOfBuildingType(previousBuilding).buildingSprite;
        BuildingData.BuildingType nextBuilding = BuildModeController.GetAdjacentBuildingType(true, buildingType);
        nextBuildingSlot.sprite = buildingData.GetDataOfBuildingType(nextBuilding).buildingSprite;

        selectedBuildingText.text = subtype + " " + buildingType.ToString();


    }
    //Used so tooltips can be dynamic
    void ReplaceTextWithLineStartingWith(TMP_Text text, string startingCharacters, string replacement)
    {
        string[] lines = text.text.Split('\n');
        bool replaced = false;

        for (int i = 0; i < lines.Length; i++)
        {
            if (lines[i].StartsWith(startingCharacters))
            {
                lines[i] = replacement;
                replaced = true;
            }
        }

        if (replaced)
        {
            text.text = string.Join("\n", lines);
        }
        else if (!string.IsNullOrWhiteSpace(replacement))
        {
            string trimmedText = text.text.Trim();
            if (string.IsNullOrEmpty(trimmedText))
                text.text = replacement;
            else
                text.text = trimmedText + "\n" + replacement;
        }

    }

    [Rpc(SendTo.Owner)]
    void DisplayHitmarkOwnerRpc()
    {
        hitmark.color = new Color(255, 255, 255, 1);
        hitmark.enabled = true;
        StartCoroutine(DecreaseVisibilityOfHitmark());
    }

    [Rpc(SendTo.Owner)]
    void DisplayCooldownCircleOwnerRpc(float maximumCooldownValue)
    {
        cooldownMarker.enabled = true;
        StartCoroutine(ChangeCooldownCircle(maximumCooldownValue));
    }

    IEnumerator ChangeCooldownCircle(float maximumCooldownValue)
    {
        float currentCooldownValue = maximumCooldownValue;
        float updateTime = 0.01f;
        while (cooldownMarker.fillAmount != 0)
        {
            currentCooldownValue -= updateTime;
            yield return new WaitForSeconds(updateTime * 1.001f);
            cooldownMarker.fillAmount = currentCooldownValue / maximumCooldownValue;
        }
        cooldownMarker.enabled = false;
        cooldownMarker.fillAmount = 1;
    }
    IEnumerator DecreaseVisibilityOfHitmark() {
        float fadeDuration = 0.5f;
        float timeDifferenceBetweenFades = 0.05f;
        float fadeTime = 0f;
        while (hitmark.enabled == true)
        {
            fadeTime += timeDifferenceBetweenFades;
            float alpha = Mathf.Clamp01(1 - fadeTime / fadeDuration); 
            Color newColor = hitmark.color;
            newColor.a = alpha;
            hitmark.color = newColor;
            if (alpha <= 0)
            {
                hitmark.enabled = false;
                hitmark.color = new Color(255, 255, 255, 1);
            }
            yield return new WaitForSeconds(timeDifferenceBetweenFades);
        }
    }

    [Rpc(SendTo.Owner)]
    public void DisplayErrorOwnerRpc(string error) {
        if(errorText.enabled == true) //if not this, coroutine could be called couple times, which results in bugged behaviour
            return;

        errorText.text = error;
        errorText.enabled = true;
        StartCoroutine(DecreaseVisibilityOfErrorText());
    }

    IEnumerator DecreaseVisibilityOfErrorText()
    {
        float fadeDuration = 1f;
        float timeDifferenceBetweenFades = 0.05f;
        float fadeTime = 0f;
        while (errorText.enabled == true)
        {
            fadeTime += timeDifferenceBetweenFades;
            float alpha = Mathf.Clamp01(1 - fadeTime / fadeDuration);
            Color newColor = errorText.color;
            newColor.a = alpha;
            errorText.color = newColor;
            if (alpha <= 0)
            {
                errorText.enabled = false;
                errorText.color = new Color(255, 0, 0, 1);
            }
            yield return new WaitForSeconds(timeDifferenceBetweenFades);
        }
    }

    public void DisplayInventory(NetworkListEvent<ItemData.ItemProperties> e)
    {
        if (!IsOwner) return;
        ItemTypeData itemTypeData = GameManager.Instance.ItemTypeData;
        ItemTierData itemTierData = GameManager.Instance.ItemTierData;
        //Inventory slots are numbered 1, 2, 3, but Inventory is 0-indexed, e.Value is changed element, e.Index is it's index
        GameObject inventorySlot = inventorySlots[e.Index];
        if (inventorySlot == null) { return; }
        Image staticItemImage = inventorySlot.transform.Find("StaticItemImage").GetComponent<Image>();
        Image coloredItemImage = inventorySlot.transform.Find("ColoredItemImage").GetComponent<Image>();
        Slider durabilitySlider = inventorySlot.transform.Find("DurabilityBar").GetComponent<Slider>();
        if (e.Value.itemType != ItemType.Null)
        {
            staticItemImage.enabled = true;
            staticItemImage.sprite = itemTypeData.GetDataOfItemType(e.Value.itemType).staticItemSprite;

            coloredItemImage.enabled = true;
            coloredItemImage.sprite = itemTypeData.GetDataOfItemType(e.Value.itemType).coloredItemSprite;
            coloredItemImage.color = itemTierData.GetDataOfItemTier(e.Value.itemTier).UIColor;


            durabilitySlider.gameObject.SetActive(true);
            durabilitySlider.maxValue = itemTierData.GetDataOfItemTier(e.Value.itemTier).maximumDurability;
            durabilitySlider.value = e.Value.durablity;
        }
        else
        {
            staticItemImage.enabled = false;
            coloredItemImage.enabled = false;
            durabilitySlider.gameObject.SetActive(false);
        }
    }
    public void ChangeInventorySlot(int oldInventorySlot, int newInventorySlot) {
        if(!IsOwner) return;
        //Inventory slots on scene are named 1, 2, 3 etc, but in code they are 0 indexed!
        inventorySlots[oldInventorySlot].GetComponent<Image>().sprite = unusedInventorySlot;
        inventorySlots[newInventorySlot].GetComponent<Image>().sprite = usedInventorySlot;
    }

    public void ModifyHungerBar(int oldHungerValue, int newHungerValue)
    {
        if (!IsOwner) return;
        hungerBar.value = newHungerValue;
        hungerBarText.text = newHungerValue.ToString();
    }

    public void ModifyHealthBar(int oldHealthValue, int newHealthValue)
    {
        if (!IsOwner) return;
        healthBar.value = newHealthValue;
        healthBarText.text = newHealthValue.ToString();
        float t = Mathf.InverseLerp(1, 100, newHealthValue);
        healthBar.fillRect.GetComponent<Image>().color = Color.Lerp(Color.red, Color.green, t);
    }
    public void ModifyMoneyCount(float oldMoneyValue, float newMoneyValue)
    {
        if (!IsOwner) return;
        moneyCount.text = newMoneyValue.ToString() + "$";
    }
    public void ModifyVoiceChatIcon(bool shouldBeEnabled)
    {
        micActivityIcon.enabled = shouldBeEnabled;
    }
    public void DisplayIsCriminalText(int oldCooldown, int newCooldown) //this is criminal cooldown
    {
        if (!IsOwner) return;
        if (newCooldown == 0)
            criminalText.text = "";
        else
        {
            if (playerData.Role.Value == PlayerData.PlayerRole.Leader)
                criminalText.text = "You broke law: " + newCooldown.ToString() + "s left";
            else
                criminalText.text = "You are criminal: " + newCooldown.ToString() + "s left";
        }
            
    }
    public void DisplayInPrisonText(int oldCooldown, int newCooldown) //this is jail cooldown
    {
        if (!IsOwner) return;
        if (newCooldown == 0)
            criminalText.text = "";
        else
            criminalText.text = "You are in jail: " + newCooldown.ToString() + "s left";
    }
    //Rpc because taxRate is only server side and needs to be sent manually
    [Rpc(SendTo.Owner)]
    public void ModifyTaxRateTextOwnerRpc(float newTaxValue)
    {
        taxRate.text = "Tax rate: \n " + (newTaxValue * 100).ToString() + "%";
    }

    [Rpc(SendTo.Owner)]
    public void DisplayProgressBarOwnerRpc(int amountOfTime)
    {
        progressBar.gameObject.SetActive(true);
        progressBar.value = 0;
        progressBarCoroutine = StartCoroutine(FillProgressBar(amountOfTime));
    }
    
    /// <summary>
    /// Displays and sets up storage trade menu, enabling interaction with stored materials.
    /// </summary>
    /// <param name="storageObjectId">The unique identifier of the storage object to interact with.</param>
    [Rpc(SendTo.Owner)]
    public void DisplayStorageTradeMenuOwnerRpc(ulong storageObjectId)
    {
        GameObject storageMenu = Instantiate(storageTradePanel, playerUI); //Maybe use var instead of Find();
        //We need storage as we need data about it and we will call its function on button click
        Storage targetStorage = NetworkManager.SpawnManager.SpawnedObjects[storageObjectId].GetComponent<Storage>();
        
        bool isStorageOwner = targetStorage.OwnerId.Value == NetworkManager.Singleton.LocalClientId;

        TMP_InputField amountInputField = storageMenu.transform.Find("AmountInputField").GetComponent<TMP_InputField>();
        Button confirmButton = storageMenu.transform.Find("ConfirmButton").GetComponent<Button>();
        Button modeChangeButton = storageMenu.transform.Find("ModeChangeButton").GetComponent<Button>();
        TMP_Text explanatoryText = storageMenu.transform.Find("ExplanatoryText").GetComponent<TMP_Text>();
        TMP_Text paymentText = storageMenu.transform.Find("PaymentText").GetComponent<TMP_Text>();
        TMP_Dropdown materialDropdown = storageMenu.transform.Find("MaterialDropdown").GetComponent<TMP_Dropdown>();

        PlayerData.RawMaterial selectedRawMaterial;
        if (targetStorage.StoredMaterialData.Count == 1)
        {
            selectedRawMaterial = targetStorage.StoredMaterialData[0].MaterialType;
            Destroy(materialDropdown.gameObject);
        }
        else
        {
            List<TMP_Dropdown.OptionData> options = new();
            selectedRawMaterial = targetStorage.StoredMaterialData[0].MaterialType;
            foreach (PlayerData.ExtendedMaterialData material in targetStorage.StoredMaterialData)
            {
                options.Add(new TMP_Dropdown.OptionData(material.MaterialType.ToString()));
            }
            materialDropdown.AddOptions(options);

            materialDropdown.onValueChanged.AddListener(currentDropdownSlot =>
            {
                string selectedOption = materialDropdown.options[currentDropdownSlot].text;
                selectedRawMaterial = (PlayerData.RawMaterial)Enum.Parse(typeof(PlayerData.RawMaterial), selectedOption);

                amountInputField.text = ClampStorageTradeMenuInputField(selectedRawMaterial, targetStorage, int.Parse(amountInputField.text == "" ? "0" : amountInputField.text)).ToString();
            });

        }

        if (!isPlayerSelling) //listener is not run on initialization and I don't want to create function from this
        {
            explanatoryText.text = explanatoryText.text.Replace("sell", "buy");
            modeChangeButton.GetComponentInChildren<TMP_Text>().text = modeChangeButton.GetComponentInChildren<TMP_Text>().text.Replace("buying", "selling");
            paymentText.text = paymentText.text.Replace("have to pay", "receive");
        }

        modeChangeButton.onClick.AddListener(() =>
        {
            isPlayerSelling = !isPlayerSelling;
            if (isPlayerSelling)
            {
                explanatoryText.text = explanatoryText.text.Replace("buy", "sell");
                modeChangeButton.GetComponentInChildren<TMP_Text>().text = modeChangeButton.GetComponentInChildren<TMP_Text>().text.Replace("selling", "buying");
                paymentText.text = paymentText.text.Replace("have to pay", "receive");
            }
            else
            {
                explanatoryText.text = explanatoryText.text.Replace("sell", "buy");
                modeChangeButton.GetComponentInChildren<TMP_Text>().text = modeChangeButton.GetComponentInChildren<TMP_Text>().text.Replace("buying", "selling");
                paymentText.text = paymentText.text.Replace("receive", "have to pay");
            }
        });

        amountInputField.onValueChanged.AddListener(newAmount =>
        {
            if (int.TryParse(newAmount, out int value))
            {
                amountInputField.text = ClampStorageTradeMenuInputField(selectedRawMaterial, targetStorage, int.Parse(amountInputField.text)).ToString();

                if(isPlayerSelling)
                    paymentText.text = Regex.Replace(paymentText.text, @"-?\d+(\.\d+)?", isStorageOwner ? "0" : Convert.ToString(value * targetStorage.SellingPrice.Value));
                else
                    paymentText.text = Regex.Replace(paymentText.text, @"-?\d+(\.\d+)?", isStorageOwner ? "0" : Convert.ToString(value * targetStorage.BuyingPrice.Value));
            }
        });

        confirmButton.onClick.AddListener(() =>
        {
            if (amountInputField.text == "")
                return;
            if (isPlayerSelling)
                targetStorage.SellMaterialsServerRpc(NetworkManager.Singleton.LocalClientId, Convert.ToInt16(amountInputField.text), selectedRawMaterial);
            else
                targetStorage.BuyMaterialsServerRpc(NetworkManager.Singleton.LocalClientId, Convert.ToInt16(amountInputField.text), selectedRawMaterial);


            paymentText.text = Regex.Replace(paymentText.text, @"-?\d+(\.\d+)?", "0");
            amountInputField.text = "";
        });
        MakePanelInteractible(storageMenu);
    }

    /// <summary>
    /// Clamps given value within the range that can be sold/bought in given storage
    /// </summary>
    /// <param name="selectedRawMaterial">The raw material being traded.</param>
    /// <param name="targetStorage">The storage involved in the trade.</param>
    /// <param name="value">The input value to clamp.</param>
    /// <returns>The clamped value within the valid trade range.</returns>
    int ClampStorageTradeMenuInputField(PlayerData.RawMaterial selectedRawMaterial, Storage targetStorage, int value)
    {
        int maximumAmount;
        PlayerData.ExtendedMaterialData playerMaterial = playerData.GetMaterialDataOfOwnedRawMaterial(selectedRawMaterial);
        PlayerData.ExtendedMaterialData storageMaterial = targetStorage.GetMaterialDataOfRawMaterial(selectedRawMaterial);
        if (isPlayerSelling)
            maximumAmount = Mathf.Min(playerMaterial.Amount, storageMaterial.MaxAmount - storageMaterial.Amount);
        else
            maximumAmount = Mathf.Min(storageMaterial.Amount, playerMaterial.MaxAmount - playerMaterial.Amount);

        int minimumAmount = 0;
        if (value < minimumAmount || value > maximumAmount)
            value = Mathf.Clamp(value, minimumAmount, maximumAmount);

        return value;
    }

    /// <summary>
    /// Call this function when spawning some panel to make player able to interact with it.
    /// When panel is destroyed, function handles rewerting to normal player control.
    /// </summary>
    /// <param name="panel">GameObject of panel that we are making interactible.</param>
    void MakePanelInteractible(GameObject panel)
    {
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
        GetComponent<Movement>().blockRotation = true;
        GetComponent<ObjectInteraction>().canInteract = false;
        GameObject.Find("Canvas").GetComponent<Menu>().amountOfDisplayedMenus++;

        StartCoroutine(CheckIfMenuGotDestroyed(panel));
    }

    [Rpc(SendTo.Owner)]
    public void DisplayStorageManagementMenuOwnerRpc(ulong storageObjectId)
    {
        GameObject storageMenu = Instantiate(storageManagmentPanel, playerUI);
        //We need storage as we need data about it and we will call its function on button click
        Storage targetStorage = NetworkManager.SpawnManager.SpawnedObjects[storageObjectId].GetComponent<Storage>();

        Transform sellingPanel = storageMenu.transform.Find("SellingPanel");
        Transform buyingPanel = storageMenu.transform.Find("BuyingPanel");

        TMP_InputField sellingPriceInputField = sellingPanel.Find("SellingPriceInput").GetComponent<TMP_InputField>();
        sellingPriceInputField.placeholder.GetComponent<TMP_Text>().text = targetStorage.SellingPrice.Value.ToString(); 
        TMP_InputField buyingPriceInputField = buyingPanel.Find("BuyingPriceInput").GetComponent<TMP_InputField>();
        buyingPriceInputField.placeholder.GetComponent<TMP_Text>().text = targetStorage.BuyingPrice.Value.ToString();
        Button confirmSellingPriceButton = sellingPanel.Find("SellingPriceConfirmButton").GetComponent<Button>();
        Button confirmBuyingPriceButton = buyingPanel.Find("BuyingPriceConfirmButton").GetComponent<Button>();

        sellingPriceInputField.onValueChanged.AddListener(_ =>
            RoundInputFieldToTwoDecimalPlaces(sellingPriceInputField)
        );
        buyingPriceInputField.onValueChanged.AddListener(_ =>
            RoundInputFieldToTwoDecimalPlaces(buyingPriceInputField)
        );

        confirmSellingPriceButton.onClick.AddListener(() =>
        {
            if (sellingPriceInputField.text == "")
                return;

            if (float.TryParse(sellingPriceInputField.text, NumberStyles.Any, CultureInfo.CurrentCulture, out float sellingPrice))
            {
                targetStorage.SellingPrice.Value = sellingPrice;
                sellingPriceInputField.placeholder.GetComponent<TMP_Text>().text = Convert.ToString(sellingPrice); //Temporary way to see current prices
                sellingPriceInputField.text = "";
            }
        });

        confirmBuyingPriceButton.onClick.AddListener(() =>
        {
            if (buyingPriceInputField.text == "")
                return;

            if (float.TryParse(buyingPriceInputField.text, NumberStyles.Any, CultureInfo.CurrentCulture, out float buyingPrice))
            {
                targetStorage.BuyingPrice.Value = buyingPrice;
                buyingPriceInputField.placeholder.GetComponent<TMP_Text>().text = Convert.ToString(buyingPrice);
                buyingPriceInputField.text = "";
            }
        });

        MakePanelInteractible(storageMenu);
    }

    [Rpc(SendTo.Owner)]
    public void DisplayDeliveryPricesManagmentPanelOwnerRpc(ulong unbuiltBuildingId)
    {
        UnbuiltBuilding targetBuilding = NetworkManager.SpawnManager.SpawnedObjects[unbuiltBuildingId].GetComponent<UnbuiltBuilding>();
        GameObject materialDeliveryMenu = Instantiate(materialDeliveryPaymentManagmentPanel, playerUI);

        TMP_Dropdown materialDropdown = materialDeliveryMenu.transform.Find("MaterialDropdown").GetComponent<TMP_Dropdown>();
        TMP_InputField amountInputField = materialDeliveryMenu.transform.Find("PaymentInputField").GetComponent<TMP_InputField>();
        Button confirmButton = materialDeliveryMenu.transform.Find("ConfirmButton").GetComponent<Button>();

        List<string> options = new();
        foreach (PlayerData.ExtendedMaterialData material in targetBuilding.NeededMaterials)
            if (material.Amount < material.MaxAmount)
                options.Add(material.MaterialType.ToString());

        materialDropdown.ClearOptions();
        materialDropdown.AddOptions(options);

        materialDropdown.onValueChanged.AddListener(newValue =>
        {
            PlayerData.RawMaterial material = (PlayerData.RawMaterial)Enum.Parse(typeof(PlayerData.RawMaterial), materialDropdown.options[newValue].text);
            for(int i = 0; i < targetBuilding.NeededMaterials.Count; i++)
            {
                if (targetBuilding.NeededMaterials[i].MaterialType == material)
                    amountInputField.placeholder.GetComponent<TMP_Text>().text = targetBuilding.MaterialPrices[i].ToString();
            }
            amountInputField.text = "";
        });

        amountInputField.onValueChanged.AddListener(newAmount =>
        {
            RoundInputFieldToTwoDecimalPlaces(amountInputField);
        });

        confirmButton.onClick.AddListener(() =>
        {
            if (amountInputField.text == "")
                return;
            PlayerData.RawMaterial rawMaterial = (PlayerData.RawMaterial)Enum.Parse(typeof(PlayerData.RawMaterial), materialDropdown.options[materialDropdown.value].text);
            float newPrice = float.Parse(amountInputField.text);
            amountInputField.placeholder.GetComponent<TMP_Text>().text = newPrice.ToString();
            amountInputField.text = "";
            targetBuilding.ChangePriceOfMaterialServerRpc(rawMaterial, newPrice);
        });

        MakePanelInteractible(materialDeliveryMenu);
    }


    [Rpc(SendTo.Owner)]
    public void DisplayMaterialDeliveryPanelClientRpc(ulong unbuiltBuildingId)
    {
        UnbuiltBuilding targetBuilding = NetworkManager.SpawnManager.SpawnedObjects[unbuiltBuildingId].GetComponent<UnbuiltBuilding>();
        GameObject materialDeliveryMenu = Instantiate(materialDeliveryToUnbuiltBuildingPanel, playerUI);

        TMP_Dropdown materialDropdown = materialDeliveryMenu.transform.Find("MaterialDropdown").GetComponent<TMP_Dropdown>();
        TMP_InputField amountInputField = materialDeliveryMenu.transform.Find("AmountInputField").GetComponent<TMP_InputField>();
        Button confirmButton = materialDeliveryMenu.transform.Find("ConfirmButton").GetComponent<Button>();
        TMP_Text paymentInfoText = materialDeliveryMenu.transform.Find("PaymentTextBackground").transform.Find("PaymentText").GetComponent<TMP_Text>();
        List<string> options = new();
        foreach (PlayerData.ExtendedMaterialData material in targetBuilding.NeededMaterials)
            if(material.Amount < material.MaxAmount)
                options.Add(material.MaterialType.ToString());

        materialDropdown.ClearOptions();
        materialDropdown.AddOptions(options);

        materialDropdown.onValueChanged.AddListener(newValue =>
        {
            if (amountInputField.text == "")
                return;
            int inputFieldValue = ClampDeliverMaterialsPanelInputField((PlayerData.RawMaterial)Enum.Parse(typeof(PlayerData.RawMaterial), materialDropdown.options[newValue].text), targetBuilding, int.Parse(amountInputField.text));
            amountInputField.text = inputFieldValue.ToString();
        });

        amountInputField.onValueChanged.AddListener(newAmount =>
        {
            if (int.TryParse(newAmount, out int value))
            {
                PlayerData.RawMaterial selectedMaterial = (PlayerData.RawMaterial)Enum.Parse(typeof(PlayerData.RawMaterial), materialDropdown.options[materialDropdown.value].text);
                int inputFieldValue = ClampDeliverMaterialsPanelInputField(selectedMaterial, targetBuilding, value);
                amountInputField.text = inputFieldValue.ToString();
                //if is owner then always display 0, as he will never receive money
                paymentInfoText.text = Regex.Replace(paymentInfoText.text, @"\d+", targetBuilding.OwnerId.Value == NetworkManager.Singleton.LocalClientId ? "0" : (targetBuilding.GetPriceOfMaterial(selectedMaterial) * inputFieldValue).ToString());
            }
        });

        confirmButton.onClick.AddListener(() =>
        {
            if (amountInputField.text == "")
                return;
            PlayerData.RawMaterial rawMaterial = (PlayerData.RawMaterial)Enum.Parse(typeof(PlayerData.RawMaterial), materialDropdown.options[materialDropdown.value].text);
            int amountToDeliver = Int16.Parse(amountInputField.text);
            targetBuilding.DeliverMaterialsServerRpc(rawMaterial, amountToDeliver);
            int inputFieldText = ClampDeliverMaterialsPanelInputField(rawMaterial, targetBuilding, int.Parse(amountInputField.text));
            amountInputField.text = inputFieldText.ToString();
        });

        MakePanelInteractible(materialDeliveryMenu);
    } 
    int ClampDeliverMaterialsPanelInputField(PlayerData.RawMaterial selectedMaterial, UnbuiltBuilding targetBuilding, int value)
    {
        int playerMaterialAmount = 0;
        int unbuiltBuildingNeededMaterialAmount = 0;
        foreach (PlayerData.ExtendedMaterialData material in playerData.OwnedMaterials)
            if (material.MaterialType == selectedMaterial)
                playerMaterialAmount = material.Amount;
        foreach (PlayerData.ExtendedMaterialData material in targetBuilding.NeededMaterials)
            if (material.MaterialType == selectedMaterial)
                unbuiltBuildingNeededMaterialAmount = material.MaxAmount - material.Amount;
        int maximumAmount = Mathf.Min(playerMaterialAmount, unbuiltBuildingNeededMaterialAmount);
        int minimumAmount = 0;

        if (value < minimumAmount || value > maximumAmount)
        {
            value = Mathf.Clamp(value, minimumAmount, maximumAmount);
        }
        return value;
    }
    void RoundInputFieldToTwoDecimalPlaces(TMP_InputField inputField)
    {
        string input = inputField.text;
        if (string.IsNullOrEmpty(input)) return;

        input = input.Replace('.', ',');

        if (input.Contains(","))
        {
            int index = input.IndexOf(',');
            int decimalPlaces = input.Length - index - 1;

            if (decimalPlaces > 2)
            {
                input = input[..(index + 3)];
                inputField.text = input;
                inputField.MoveTextEnd(false);
            }
        }
    }
    /// <summary>
    /// Display workshop menu that player uses when they want to create an item with workshop
    /// </summary>
    /// <param name="workshopId">NetworkObject.NetworkObjectId of workshop that player is interacting with</param>
    [Rpc(SendTo.Owner)]
    public void DisplayWorkshopWorkMenuOwnerRpc(ulong workshopId)
    {
        Workshop workshop = NetworkManager.SpawnManager.SpawnedObjects[workshopId].GetComponent<Workshop>();

        GameObject workshopPanel = Instantiate(workshopWorkPanel, playerUI);
        WorkshopWorkPanelReferences panelReferences = workshopPanel.GetComponent<WorkshopWorkPanelReferences>();

        panelReferences.spawnItemButton.onClick.AddListener(() =>
        {
            workshop.CreateItemServerRpc();
            Destroy(workshopPanel);
        });

        MakePanelInteractible(workshopPanel);
    }

    [Rpc(SendTo.Owner)]
    public void DisplayWorkshopUpgradeMenuOwnerRpc(ulong workshopId)
    {
        Workshop workshop = NetworkManager.SpawnManager.SpawnedObjects[workshopId].GetComponent<Workshop>();

        GameObject workshopPanel = Instantiate(workshopUpgradePanel, playerUI);
        WorkshopUpgradePanelReferences panelReferences = workshopPanel.GetComponent<WorkshopUpgradePanelReferences>();

        panelReferences.upgradeButton.onClick.AddListener(() =>
        {
            workshop.UpgradeWorkshopServerRpc();
            Destroy(workshopPanel);
        });
        ItemData.ItemTier newItemTier = workshop.ItemTier;
        var values = Enum.GetValues(typeof(ItemData.ItemTier));
        int tierNumber = Array.IndexOf(values, workshop.ItemTier);
        if (tierNumber < values.Length - 1)
            newItemTier = (ItemData.ItemTier)values.GetValue(tierNumber + 1);

        List<PlayerData.MaterialData> neededMaterials = workshop.GetNeededMaterialsForAnItem(workshop.ItemType, newItemTier);

        panelReferences.upgradeInfoText.text = $"Update this workshop to produce {newItemTier} {workshop.ItemType}. \n Cost: \n";
        foreach (PlayerData.MaterialData materialData in neededMaterials)
            panelReferences.upgradeInfoText.text += $"{materialData.Amount} of {materialData.MaterialType}\n";
        
        if (newItemTier == workshop.ItemTier)
        {
            panelReferences.upgradeInfoText.text = $"This workshop is maximally upgraded already!\n You cannot upgrade it any longer.";
            Destroy(panelReferences.upgradeButton.gameObject);
        }

        MakePanelInteractible(workshopPanel);
    }

    //If menu is destroyed, make player be able to play the game again (and also destroy menu on button press)
    IEnumerator CheckIfMenuGotDestroyed(GameObject menuToCheck)
    {
        while (true)
        {
            if (!menuToCheck)
            {
                GameObject.Find("Canvas").GetComponent<Menu>().ResumeGame(false);
                GetComponent<Movement>().blockRotation = false;
                GetComponent<ObjectInteraction>().canInteract = true;
                break;
            }
            yield return new WaitForSeconds(0.1f);
        }
    }
    IEnumerator FillProgressBar(int totalAmountOfTime)
    {
        bool isBarFilled = false;
        while (!isBarFilled)
        {
            progressBar.value++;
            yield return new WaitForSeconds(totalAmountOfTime / 100f);

            if (progressBar.value >= 100)
            {
                progressBar.gameObject.SetActive(false);
                isBarFilled = true;
            }
        }
        progressBarCoroutine = null;
    }

    [Rpc(SendTo.Owner)]
    public void ForceStopProgressBarOwnerRpc()
    {
        progressBar.gameObject.SetActive(false);
        if(progressBarCoroutine != null)
            StopCoroutine(progressBarCoroutine);
    }

}
