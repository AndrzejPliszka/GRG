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
    public override void OnNetworkSpawn()
    {
        objectInteraction = GetComponent<ObjectInteraction>();
        PlayerData playerData = GetComponent<PlayerData>();
        playerData.SelectedInventorySlot.OnValueChanged += ChangeInventorySlot;
        playerData.Inventory.OnListChanged += DisplayInventory;
        playerData.Hunger.OnValueChanged += ModifyHungerBar;
        playerData.Health.OnValueChanged += ModifyHealthBar;

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
            GameObject.Find("CenterText").GetComponent<TMP_Text>().text = "";
            return;
        }
        
         switch (targetObject.tag)
        {
            case "Player":
                GameObject.Find("CenterText").GetComponent<TMP_Text>().text = targetObject.GetComponent<PlayerData>().Nickname.Value.ToString();
                break;
            case "Item":
                GameObject.Find("CenterText").GetComponent<TMP_Text>().text = targetObject.GetComponent<ItemData>().itemProperties.Value.itemType.ToString();;
                break;
            default:
                GameObject.Find("CenterText").GetComponent<TMP_Text>().text = "";
                break;
        };
    }

    [Rpc(SendTo.Owner)]
    void DisplayHitmarkOwnerRpc()
    {
        Image image = GameObject.Find("Hitmark").GetComponent<Image>();
        image.color = new Color(255, 255, 255, 1);
        image.enabled = true;
        StartCoroutine(DecreaseVisibilityOfHitmark());
    }

    [Rpc(SendTo.Owner)]
    void DisplayCooldownCircleOwnerRpc(float maximumCooldownValue)
    {
        Image image = GameObject.Find("CooldownMarker").GetComponent<Image>();
        image.enabled = true;
        StartCoroutine(ChangeCooldownCircle(maximumCooldownValue));
    }

    IEnumerator ChangeCooldownCircle(float maximumCooldownValue)
    {
        Image cooldownImage = GameObject.Find("CooldownMarker").GetComponent<Image>();
        float currentCooldownValue = maximumCooldownValue;
        float updateTime = 0.01f;
        while (cooldownImage.fillAmount != 0)
        {
            currentCooldownValue -= updateTime;
            yield return new WaitForSeconds(updateTime * 1.001f);
            cooldownImage.fillAmount = currentCooldownValue / maximumCooldownValue;
        }
        cooldownImage.enabled = false;
        cooldownImage.fillAmount = 1;
    }
    IEnumerator DecreaseVisibilityOfHitmark() {
        Image image = GameObject.Find("Hitmark").GetComponent<Image>();
        float fadeDuration = 0.5f;
        float timeDifferenceBetweenFades = 0.05f;
        float fadeTime = 0f;
        while (image.enabled == true)
        {
            fadeTime += timeDifferenceBetweenFades;
            float alpha = Mathf.Clamp01(1 - fadeTime / fadeDuration); 
            Color newColor = image.color;
            newColor.a = alpha;
            image.color = newColor;
            if (alpha <= 0)
            {
                image.enabled = false;
                image.color = new Color(255, 255, 255, 1);
            }
            yield return new WaitForSeconds(timeDifferenceBetweenFades);
        }
    }

    public void DisplayInventory(NetworkListEvent<ItemData.ItemProperties> e)
    {
        if (!IsOwner) return;
        //Inventory slots are numbered 1, 2, 3, but Inventory is 0-indexed, e.Value is changed element, e.Index is it's index
        GameObject inventorySlot = GameObject.Find($"InventorySlot{e.Index + 1}");
        if (inventorySlot == null) { return; }
        Image staticItemImage = inventorySlot.transform.Find("StaticItemImage").GetComponent<Image>();
        Image coloredItemImage = inventorySlot.transform.Find("ColoredItemImage").GetComponent<Image>();
        if (e.Value.itemType != ItemData.ItemType.Null)
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
        GameObject.Find($"InventorySlot{oldInventorySlot + 1}").GetComponent<Image>().sprite = unusedInventorySlot;
        GameObject.Find($"InventorySlot{newInventorySlot + 1}").GetComponent<Image>().sprite = usedInventorySlot;
    }

    public void ModifyHungerBar(int oldHungerValue, int newHungerValue)
    {
        if (!IsOwner) return;
        GameObject.Find("HungerBar").GetComponent<Slider>().value = newHungerValue;
        GameObject.Find("HungerBarText").GetComponent<TMP_Text>().text = newHungerValue.ToString();
    }

    public void ModifyHealthBar(int oldHealthValue, int newHealthValue)
    {
        if (!IsOwner) return;
        GameObject.Find("HealthBar").GetComponent<Slider>().value = newHealthValue;
        GameObject.Find("HealthBarText").GetComponent<TMP_Text>().text = newHealthValue.ToString();
    }

    public void ModifyVoiceChatIcon(bool shouldBeEnabled)
    {
        GameObject.Find("MicActivityIcon").GetComponent<Image>().enabled = shouldBeEnabled;
    }
}
