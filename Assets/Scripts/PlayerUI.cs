using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using Unity.Collections;
using UnityEngine.UI;
using Unity.Netcode;
using UnityEngine;
using System;
using static ItemData;
using System.Text.RegularExpressions;
using UnityEngine.Windows;
[RequireComponent(typeof(PlayerData))]
[RequireComponent(typeof(ObjectInteraction))] //this is because we will use function that says what are we looking at
public class PlayerUI : NetworkBehaviour
{
    ObjectInteraction objectInteraction;
    VoiceChat voiceChat;
    // Start is called before the first frame update
    [SerializeField] Sprite unusedInventorySlot;
    [SerializeField] Sprite usedInventorySlot;

    [SerializeField] ItemTypeData itemTypeData;
    [SerializeField] ItemTierData itemTierData;


    [SerializeField] GameObject storageTradePanel;
    [SerializeField] GameObject storageManagmentPanel;

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
    Image hitmark;
    Image cooldownMarker;
    Image micActivityIcon;
    Slider hungerBar;
    Slider healthBar;
    Slider progressBar;
    readonly List<GameObject> inventorySlots = new();
    Coroutine progressBarCoroutine;

    PlayerData playerData;
    bool isPlayerSelling = true; //Used in storage trade menu, global so it saves between menus, change name when more menus are added
    public override void OnNetworkSpawn()
    {
        
        centerText = GameObject.Find("CenterText").GetComponent<TMP_Text>();
        errorText = GameObject.Find("ErrorText").GetComponent<TMP_Text>();
        hungerBarText = GameObject.Find("HungerBarText").GetComponent<TMP_Text>();
        healthBarText = GameObject.Find("HealthBarText").GetComponent<TMP_Text>();
        moneyCount = GameObject.Find("MoneyCount").GetComponent<TMP_Text>();
        taxRate = GameObject.Find("TaxRate").GetComponent<TMP_Text>();
        criminalText = GameObject.Find("CriminalText").GetComponent<TMP_Text>();

        woodMaterialText = GameObject.Find("WoodMaterialData").GetComponent<TMP_Text>();
        foodMaterialText = GameObject.Find("FoodMaterialData").GetComponent<TMP_Text>();
        stoneMaterialText = GameObject.Find("StoneMaterialData").GetComponent<TMP_Text>();

        hitmark = GameObject.Find("Hitmark").GetComponent<Image>();
        cooldownMarker = GameObject.Find("CooldownMarker").GetComponent<Image>();
        micActivityIcon = GameObject.Find("MicActivityIcon").GetComponent<Image>();

        hungerBar = GameObject.Find("HungerBar").GetComponent<Slider>();
        healthBar = GameObject.Find("HealthBar").GetComponent<Slider>();

        errorText.enabled = false; //We do this so DisplayError() works (see function)

        GameObject inventorySlotsContainer = GameObject.Find("InventorySlots");
        for (int i = 0; i < inventorySlotsContainer.transform.childCount; i++) {
            inventorySlots.Add(inventorySlotsContainer.transform.GetChild(i).gameObject);
        }


        playerData = GetComponent<PlayerData>();
        objectInteraction = GetComponent<ObjectInteraction>();

        if (IsOwner)
        {
            progressBar = GameObject.Find("ProgressBar").GetComponent<Slider>();
            progressBar.gameObject.SetActive(false);
            playerData.SelectedInventorySlot.OnValueChanged += ChangeInventorySlot;
            playerData.Inventory.OnListChanged += DisplayInventory;
            playerData.Hunger.OnValueChanged += ModifyHungerBar;
            playerData.Health.OnValueChanged += ModifyHealthBar;
            playerData.Money.OnValueChanged += ModifyMoneyCount;
            playerData.CriminalCooldown.OnValueChanged += DisplayIsCriminalText;
            playerData.JailCooldown.OnValueChanged += DisplayInPrisonText;
            playerData.OwnedMaterials.OnListChanged += DisplayMaterialText;
        }

        if (IsServer)
        {
            //Here are only server side events which call RPCs
            objectInteraction.OnHittingSomething += (GameObject hitGameObject) => { DisplayHitmarkOwnerRpc(); };
            objectInteraction.OnPunch += DisplayCooldownCircleOwnerRpc;
            playerData.TownId.OnValueChanged += (int oldTownId, int newTownId) => { //MOVE TO OTHER FUNCTION IF IT GROWS TOO MUCH !!!
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
    }

    private void Update()
    {
        if (!IsOwner) {  return; }
        UpdateLookedAtObjectText();
        if (voiceChat)
            ModifyVoiceChatIcon(!voiceChat.IsMuted);

    }

    void DisplayMaterialText(NetworkListEvent<PlayerData.MaterialData> listChange)
    {
        PlayerData.MaterialData changedMaterialData = listChange.Value;
        TMP_Text modifiedText;
        switch(changedMaterialData.materialType)
        {
            case PlayerData.RawMaterial.Wood:
                woodMaterialText.text = $"{changedMaterialData.amount}";
                modifiedText = woodMaterialText;
                break;
            case PlayerData.RawMaterial.Food:
                foodMaterialText.text = $"{changedMaterialData.amount}";
                modifiedText = foodMaterialText;
                break;
            case PlayerData.RawMaterial.Stone:
                stoneMaterialText.text = $"{changedMaterialData.amount}";
                modifiedText = stoneMaterialText;
                break;
            default:
                return;
        }
        if(changedMaterialData.amount == changedMaterialData.maxAmount)
            modifiedText.color = new Color(0, 255, 0, 1);
        else
            modifiedText.color = new Color(0, 0, 0, 1);
    }

    //This function will update text which tells player what is he looking at. It needs X Camera Rotation from client (in "Vector3 form") (server doesn't have camera - it is only on client)
    void UpdateLookedAtObjectText()
    {
        GameObject targetObject = objectInteraction.GetObjectInFrontOfCamera(GameObject.Find("Camera").transform.rotation.eulerAngles.x);
        if (targetObject == null || string.IsNullOrEmpty(targetObject.tag))
        {
            centerText.text = "";
            return;
        }
        //declare these here to avoid scope issues
        Shop shopScript;
        BreakableStructure breakableStructure;
        Storage storage;
        MoneyObject moneyObject;
        House house;
        GatherableMaterial materialItem;
        int currentHealth, maxHealth;
        switch (targetObject.tag)
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
                maxHealth = breakableStructure.MaximumHealth;
                centerText.text = $"Tree\n{currentHealth}/{maxHealth}";
                break;
            case "Crop":
                breakableStructure = targetObject.GetComponent<BreakableStructure>();
                currentHealth = breakableStructure.Health.Value;
                maxHealth = breakableStructure.MaximumHealth;
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
                maxHealth = breakableStructure.MaximumHealth;
                centerText.text = $"{shopScript.HoverText}\n{currentHealth}/{maxHealth}";
                break;
            case "Storage":
                storage = targetObject.transform.parent.GetComponent<Storage>();
                centerText.text = $"{storage.StoredMaterial.Value} Supply:\n{storage.CurrentSupply.Value}/{storage.MaxSupply.Value}";
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
                maxHealth = breakableStructure.MaximumHealth;
                centerText.text = $"Rock\n{currentHealth}/{maxHealth}";
                break;
            case "GatherableMaterial":
                materialItem = targetObject.GetComponent<GatherableMaterial>();
                switch (materialItem.Material.Value)
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
                centerText.text = materialItem.Material.Value switch
                {
                    PlayerData.RawMaterial.Wood => $"Wood Material",
                    PlayerData.RawMaterial.Stone => $"Stone Material",
                    PlayerData.RawMaterial.Food => $"Food Material",
                    _ => "",
                };
                break;
        };
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
        //Inventory slots are numbered 1, 2, 3, but Inventory is 0-indexed, e.Value is changed element, e.Index is it's index
        GameObject inventorySlot = inventorySlots[e.Index];
        if (inventorySlot == null) { return; }
        Image staticItemImage = inventorySlot.transform.Find("StaticItemImage").GetComponent<Image>();
        Image coloredItemImage = inventorySlot.transform.Find("ColoredItemImage").GetComponent<Image>();
        if (e.Value.itemType != ItemType.Null)
        {
            staticItemImage.enabled = true;
            staticItemImage.sprite = itemTypeData.GetDataOfItemType(e.Value.itemType).staticItemSprite;

            coloredItemImage.enabled = true;
            coloredItemImage.sprite = itemTypeData.GetDataOfItemType(e.Value.itemType).coloredItemSprite;
            coloredItemImage.color = itemTierData.GetDataOfItemTier(e.Value.itemTier).UIColor;
        }
        else
        {
            staticItemImage.enabled = false;
            coloredItemImage.enabled = false;
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

    [Rpc(SendTo.Owner)] //To do add price to items
    public void DisplayStorageTradeMenuOwnerRpc(ulong storageObjectId, int playerMaterialSupply)
    {
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
        GetComponent<Movement>().blockRotation = true;
        GetComponent<ObjectInteraction>().canInteract = false;
        GameObject.Find("Canvas").GetComponent<Menu>().amountOfDisplayedMenus++;

        GameObject storageMenu = Instantiate(storageTradePanel, GameObject.Find("Canvas").transform.Find("PlayerUI").transform); //Maybe use var instead of Find();
        //We need storage as we need data about it and we will call its function on button click
        Storage targetStorage = NetworkManager.SpawnManager.SpawnedObjects[storageObjectId].GetComponent<Storage>();

        TMP_InputField amountInputField = storageMenu.transform.Find("AmountInputField").GetComponent<TMP_InputField>();
        Button confirmButton = storageMenu.transform.Find("ConfirmButton").GetComponent<Button>();
        Button modeChangeButton = storageMenu.transform.Find("ModeChangeButton").GetComponent<Button>();
        TMP_Text explanatoryText = storageMenu.transform.Find("ExplanatoryText").GetComponent<TMP_Text>();
        TMP_Text paymentText = storageMenu.transform.Find("PaymentText").GetComponent<TMP_Text>();

        if (!isPlayerSelling) //listener is not run on initialization and I don't want to create function from this
        {
            explanatoryText.text = explanatoryText.text.Replace("sell", "buy");
            modeChangeButton.GetComponentInChildren<TMP_Text>().text = modeChangeButton.GetComponentInChildren<TMP_Text>().text.Replace("buying", "selling");
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

        amountInputField.onValueChanged.AddListener((string newAmount) =>
        {
            if (int.TryParse(newAmount, out int value))
            {
                int maximumAmount;
                PlayerData.MaterialData playerMaterial = playerData.OwnedMaterials[(int)targetStorage.StoredMaterial.Value];
                if (isPlayerSelling)
                    maximumAmount = Mathf.Min(playerMaterial.amount, targetStorage.MaxSupply.Value - targetStorage.CurrentSupply.Value);
                else
                    maximumAmount = Mathf.Min(targetStorage.CurrentSupply.Value, playerMaterial.maxAmount - playerMaterial.amount);

                int minimumAmount = 0;
                if (value < minimumAmount || value > maximumAmount)
                {
                    value = Mathf.Clamp(value, minimumAmount, maximumAmount);
                    amountInputField.text = value.ToString();
                }

                if(isPlayerSelling)
                    paymentText.text = Regex.Replace(paymentText.text, @"-?\d+(\.\d+)?", Convert.ToString(value * targetStorage.SellingPrice.Value));
                else
                    paymentText.text = Regex.Replace(paymentText.text, @"-?\d+(\.\d+)?", Convert.ToString(value * targetStorage.BuyingPrice.Value));
            }
        });

        confirmButton.onClick.AddListener(() =>
        {
            if (amountInputField.text == "")
                return;

            if (isPlayerSelling)
                targetStorage.SellMaterialsServerRpc(gameObject.GetComponent<NetworkObject>().NetworkObjectId, Convert.ToInt16(amountInputField.text));
            else
                targetStorage.BuyMaterialsServerRpc(gameObject.GetComponent<NetworkObject>().NetworkObjectId, Convert.ToInt16(amountInputField.text));


            paymentText.text = Regex.Replace(paymentText.text, @"-?\d+(\.\d+)?", "0");
            amountInputField.text = "";
        });
        StartCoroutine(CheckIfMenuGotDestroyed(storageMenu));
    }

    [Rpc(SendTo.Owner)] //To do add price to items
    public void DisplayStorageManagementMenuOwnerRpc(ulong storageObjectId)
    {
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
        GetComponent<Movement>().blockRotation = true;
        GetComponent<ObjectInteraction>().canInteract = false;
        GameObject.Find("Canvas").GetComponent<Menu>().amountOfDisplayedMenus++;

        GameObject storageMenu = Instantiate(storageManagmentPanel, GameObject.Find("Canvas").transform.Find("PlayerUI").transform); //Maybe use var instead of Find();
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

        sellingPriceInputField.onValueChanged.AddListener((string _) =>
            RoundInputFieldToTwoDecimalPlaces(sellingPriceInputField)
        );
        buyingPriceInputField.onValueChanged.AddListener((string _) =>
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

        StartCoroutine(CheckIfMenuGotDestroyed(storageMenu));
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

    //If menu is destroyed, make player be able to play the game again
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
