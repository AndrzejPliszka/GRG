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

    TMP_Text centerText;
    TMP_Text errorText;
    TMP_Text hungerBarText;
    TMP_Text healthBarText;
    TMP_Text moneyCount;
    TMP_Text taxRate;
    TMP_Text criminalText;
    Image hitmark;
    Image cooldownMarker;
    Image micActivityIcon;
    Slider hungerBar;
    Slider healthBar;
    Slider progressBar;
    readonly List<GameObject> inventorySlots = new();
    Coroutine progressBarCoroutine;
    public override void OnNetworkSpawn()
    {

        centerText = GameObject.Find("CenterText").GetComponent<TMP_Text>();
        errorText = GameObject.Find("ErrorText").GetComponent<TMP_Text>();
        hungerBarText = GameObject.Find("HungerBarText").GetComponent<TMP_Text>();
        healthBarText = GameObject.Find("HealthBarText").GetComponent<TMP_Text>();
        moneyCount = GameObject.Find("MoneyCount").GetComponent<TMP_Text>();
        taxRate = GameObject.Find("TaxRate").GetComponent<TMP_Text>();
        criminalText = GameObject.Find("CriminalText").GetComponent<TMP_Text>();

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
        PlayerData playerData = GetComponent<PlayerData>();
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
                centerText.text = $"Supply:\n{storage.FoodSupply}/{storage.MaximumFoodSupply}";
                break;
            case "Money":
                moneyObject = targetObject.GetComponent<MoneyObject>();
                centerText.text = $"{moneyObject.moneyAmount.Value}$";
                break;
            case "House":
                house = targetObject.GetComponent<House>();
                centerText.text = $"{house.displayedText.Value}";
                break;
            default:
                centerText.text = "";
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
            criminalText.text = "You are criminal: " + newCooldown.ToString() + "s left";
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
