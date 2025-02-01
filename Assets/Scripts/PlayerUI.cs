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
    TMP_Text hungerBarText;
    TMP_Text healthBarText;
    TMP_Text moneyCount;
    Image hitmark;
    Image cooldownMarker;
    Image micActivityIcon;
    Slider hungerBar;
    Slider healthBar;
    readonly List<GameObject> inventorySlots = new();
    public override void OnNetworkSpawn()
    {
        centerText = GameObject.Find("CenterText").GetComponent<TMP_Text>();
        hungerBarText = GameObject.Find("HungerBarText").GetComponent<TMP_Text>();
        healthBarText = GameObject.Find("HealthBarText").GetComponent<TMP_Text>();
        moneyCount = GameObject.Find("MoneyCount").GetComponent<TMP_Text>();

        hitmark = GameObject.Find("Hitmark").GetComponent<Image>();
        cooldownMarker = GameObject.Find("CooldownMarker").GetComponent<Image>();
        micActivityIcon = GameObject.Find("MicActivityIcon").GetComponent<Image>();

        hungerBar = GameObject.Find("HungerBar").GetComponent<Slider>();
        healthBar = GameObject.Find("HealthBar").GetComponent<Slider>();

        GameObject inventorySlotsContainer = GameObject.Find("InventorySlots");
        for (int i = 0; i < inventorySlotsContainer.transform.childCount; i++) {
            inventorySlots.Add(inventorySlotsContainer.transform.GetChild(i).gameObject);
        }

        objectInteraction = GetComponent<ObjectInteraction>();
        PlayerData playerData = GetComponent<PlayerData>();
        playerData.SelectedInventorySlot.OnValueChanged += ChangeInventorySlot;
        playerData.Inventory.OnListChanged += DisplayInventory;
        playerData.Hunger.OnValueChanged += ModifyHungerBar;
        playerData.Health.OnValueChanged += ModifyHealthBar;
        playerData.Money.OnValueChanged += ModifyMoneyCount;

        objectInteraction.OnHittingSomething += DisplayHitmarkOwnerRpc;
        objectInteraction.OnPunch += DisplayCooldownCircleOwnerRpc;

        voiceChat = gameObject.GetComponent<VoiceChat>();
    }

    private void Update()
    {
        if (!IsOwner) {  return; }
        UpdateLookedAtObjectText(GameObject.Find("Camera").transform.rotation.eulerAngles.x);
        if (voiceChat)
            ModifyVoiceChatIcon(!voiceChat.IsMuted);

    }

    //This function will update text which tells player what is he looking at. It needs X Camera Rotation from client (in "Vector3 form") (server doesn't have camera - it is only on client)
    void UpdateLookedAtObjectText(float cameraXRotation)
    {
        GameObject targetObject = objectInteraction.GetObjectInFrontOfCamera(cameraXRotation);
        if (targetObject == null || string.IsNullOrEmpty(targetObject.tag))
        {
            centerText.text = "";
            return;
        }
        
         switch (targetObject.tag)
        {
            case "Player":
                centerText.text = targetObject.GetComponent<PlayerData>().Nickname.Value.ToString();
                break;
            case "Item":
                centerText.text = targetObject.GetComponent<ItemData>().itemProperties.Value.itemType.ToString();;
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
    public void ModifyMoneyCount(int oldMoneyValue, int newMoneyValue)
    {
        if (!IsOwner) return;
        moneyCount.text = newMoneyValue.ToString() + "$";
    }
    public void ModifyVoiceChatIcon(bool shouldBeEnabled)
    {
        micActivityIcon.enabled = shouldBeEnabled;
    }
}
