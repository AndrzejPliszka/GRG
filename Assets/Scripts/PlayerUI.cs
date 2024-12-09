using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using Unity.Collections;
using UnityEngine.UI;
using Unity.Netcode;
using UnityEngine;
[RequireComponent(typeof(PlayerData))]
[RequireComponent(typeof(ObjectInteraction))] //this is because we will use function that says what are we looking at
public class PlayerUI : NetworkBehaviour
{
    ObjectInteraction objectInteraction;
    // Start is called before the first frame update
    [SerializeField] Sprite unusedInventorySlot;
    [SerializeField] Sprite usedInventorySlot;

    [SerializeField] ItemPrefabs itemPrefabs; //[TO DO: Change name to be actually descriptive]
    [SerializeField] ItemMaterials itemMaterials;
    public override void OnNetworkSpawn()
    {
        objectInteraction = GetComponent<ObjectInteraction>();
        PlayerData playerData = GetComponent<PlayerData>();
        playerData.SelectedInventorySlot.OnValueChanged += ChangeInventorySlot;
        playerData.Inventory.OnListChanged += DisplayInventory;
        playerData.Hunger.OnValueChanged += ModifyHungerBar;
        playerData.Health.OnValueChanged += ModifyHealthBar;
    }

    private void Update()
    {
        if (!IsOwner) {  return; }
        UpdateLookedAtObjectTextServerRpc(GameObject.Find("Camera").transform.rotation.eulerAngles.x);
    }

    //This function will update text which tells player what is he looking at. It needs X Camera Rotation from client (in "Vector3 form") (server doesn't have camera - it is only on client)
    [Rpc(SendTo.Server)]
    void UpdateLookedAtObjectTextServerRpc(float cameraXRotation)
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
                UpdateLookedAtPlayerTextOwnerRpc(targetObject.GetComponent<PlayerData>().Nickname.Value);
                break;
            case "Item":
                UpdateLookedAtItemTextOwnerRpc(targetObject.GetComponent<ItemData>().itemProperties.Value);
                break;
            default:
                ResetLookedAtTextOwnerRpc();
                break;
        };
        ;
    }
    //These short functions exists because they need to be sent to the owner as RPC and thus cannot be in ServerRpc above
    [Rpc(SendTo.Owner)]
    void UpdateLookedAtPlayerTextOwnerRpc(FixedString32Bytes nickname) {
        GameObject.Find("CenterText").GetComponent<TMP_Text>().text = nickname.ToString(); }

    [Rpc(SendTo.Owner)]
    void UpdateLookedAtItemTextOwnerRpc(ItemData.ItemProperties itemProperties) {
        GameObject.Find("CenterText").GetComponent<TMP_Text>().text = itemProperties.itemType.ToString(); }

    [Rpc(SendTo.Owner)]
    void ResetLookedAtTextOwnerRpc() {
        GameObject.Find("CenterText").GetComponent<TMP_Text>().text = ""; }

    public void DisplayInventory(NetworkListEvent<ItemData.ItemProperties> e)
    {
        if (!IsOwner) return;
        //Inventory slots are numbered 1, 2, 3, but Inventory is 0-indexed, e.Value is changed element, e.Index is it's index
        GameObject inventorySlot = GameObject.Find($"InventorySlot{e.Index + 1}");
        Image staticItemImage = inventorySlot.transform.Find("StaticItemImage").GetComponent<Image>();
        Image coloredItemImage = inventorySlot.transform.Find("ColoredItemImage").GetComponent<Image>();
        if (e.Value.itemType != ItemData.ItemType.Null)
        {
            staticItemImage.enabled = true;
            staticItemImage.sprite = itemPrefabs.GetDataOfItemType(e.Value.itemType).staticItemSprite;

            coloredItemImage.enabled = true;
            coloredItemImage.sprite = itemPrefabs.GetDataOfItemType(e.Value.itemType).coloredItemSprite;
            coloredItemImage.color = itemMaterials.GetDataOfItemTier(e.Value.itemTier).UIColor;
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
}
