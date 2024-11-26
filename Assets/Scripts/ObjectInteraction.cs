using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.UIElements;
using UnityEngine.XR;
[RequireComponent(typeof(Movement))]
[RequireComponent(typeof(PlayerData))]
public class ObjectInteraction : NetworkBehaviour
{
    Vector3 cameraOffset;
    PlayerData playerData;
    public GameObject testGameObject; //[TRANSFER OBJECT SPAWNING ON SERVER START TO GAME MANAGER!!!] !!!!

    //Held items will be moved according to movement of this object
    [SerializeField]
    Transform rightHand;
    //How is right hand in localPlayerModel named
    [SerializeField]
    string localRightHandPath = "hand.R";

    //References to scriptable objects
    [SerializeField]
    ItemPrefabs itemPrefabsData;
    [SerializeField]
    ItemMaterials itemMaterialData;


    private void Awake()
    {
        playerData = GetComponent<PlayerData>();
    }
    void Start()
    {
        cameraOffset = gameObject.GetComponent<Movement>().CameraOffset;
        //[TRANSFER OBJECT SPAWNING ON SERVER START TO GAME MANAGER!!!] !!!!
        if (IsServer && IsOwner)
        {
            GameObject testObject = Instantiate(testGameObject, new Vector3(0, 5, 0), new Quaternion());
            testObject.GetComponent<NetworkObject>().Spawn();
        }
        if (IsOwner)
        {
            //subscribe DisplayInventoryClientRpc, so it is called every time Inventory changes
            playerData.Inventory.OnListChanged += DisplayInventory;
        }
        
    }

    void Update()
    {
        if (!IsOwner) {  return; }
        float cameraXRotation = GameObject.Find("Camera").transform.rotation.eulerAngles.x;
        UpdateLookedAtObjectTextServerRpc(cameraXRotation); //This can be on update function, because it only displays data and doesn't change it in any way.
        if (Input.GetKeyDown(KeyCode.E)) //E is interaction key (for now only for picking up items)
            InteractWithObjectServerRpc(cameraXRotation);
        if (Input.GetKeyDown(KeyCode.Q)) //Q is dropping items key
            DropItemServerRpc();
        if (Input.GetKeyDown(KeyCode.Alpha1)) {
            playerData.ChangeSelectedInventorySlotServerRpc(0);
            ChangeHeldItemClientRpc(playerData.Inventory[0]);
        }
        if (Input.GetKeyDown(KeyCode.Alpha2))
        {
            playerData.ChangeSelectedInventorySlotServerRpc(1);
            ChangeHeldItemClientRpc(playerData.Inventory[1]);
        }
        if (Input.GetKeyDown(KeyCode.Alpha3))
        {
            playerData.ChangeSelectedInventorySlotServerRpc(2);
            ChangeHeldItemClientRpc(playerData.Inventory[2]);
        }
    }

    //This function casts a ray in front of camera and returns gameObject which got hit by it
    //based on rotation and position of transform.player, cameraOffset (which is variable in this class) and parameter cameraXRotation (which is in degrees in "Vector3 form")
    GameObject GetObjectInFrontOfCamera(float cameraXRotation)
    {
        if (!IsServer) throw new Exception("Don't call GetObjectInFrontOfCamera on client!"); //We do not trust client, remember?

        Quaternion verticalRotation = Quaternion.Euler(cameraXRotation, transform.rotation.eulerAngles.y, 0);
        Vector3 rayDirection = verticalRotation * Vector3.forward; //Changing quaternion into vector3, because Ray takes Vector3
        Vector3 cameraPosition = transform.position + new Vector3(0, cameraOffset.y) + transform.rotation * new Vector3(0, 0, cameraOffset.z);
        Ray ray = new(cameraPosition, rayDirection);
        Debug.DrawRay(cameraPosition, rayDirection * 100f, Color.red, 0.5f);
        LayerMask layersToDetect = ~LayerMask.GetMask("LocalObject"); // ~ negates bytes, which makes that layersToDetect is all masks except LocalObject (only relevant on host)
        if (Physics.Raycast(ray, out RaycastHit hit, 10, layersToDetect))
        { 
            return hit.collider.gameObject;
        }
        else
            return null;
    }

    //MOVE TWO METHODS BELOW TO DIFFERENT SCRIPT DEDICATED TO PLAYER INTERFACE !!!!!!!!!!!!!
    [Rpc(SendTo.Owner)]
    public void DisplayTextOnScreenClientRpc(FixedString32Bytes stringToDisplay)
    {
        GameObject.Find("CenterText").GetComponent<TMP_Text>().text = stringToDisplay.ToString();
    }

    public void DisplayInventory(NetworkListEvent<ItemData.ItemProperties> e)
    {
        GameObject.Find("InventoryText").GetComponent<TMP_Text>().text = "";
        for (int i = 0; i < playerData.Inventory.Count; i++) {
            GameObject.Find("InventoryText").GetComponent<TMP_Text>().text += playerData.Inventory[i].itemType.ToString() + " " + playerData.Inventory[i].itemTier.ToString() + "\n";
        }
    }
    //MOVE TWO METHODS ABOVE TO DIFFERENT SCRIPT DEDICATED TO PLAYER INTERFACE !!!!!!!!!!!!!



    //This function changes model that is held in hand
    [Rpc(SendTo.ClientsAndHost)]
    public void ChangeHeldItemClientRpc(ItemData.ItemProperties itemToHold)
    {
        Transform parentObject;
        //If it is owner, we want to modify localPlayerModel, instead of Player (because localPlayerModel is what owner sees)
        if (IsOwner)
            parentObject = transform.GetComponent<Movement>().LocalPlayerModel.transform.Find(localRightHandPath);
        else
            parentObject = rightHand;
            
        //Remove held item if it existed
        if (parentObject.Find("HeldItem"))
            Destroy(parentObject.Find("HeldItem").gameObject);
        //Do not spawn anything when there is no item
        if (itemToHold.itemType == ItemData.ItemType.Null)
            return;
        //Spawn object in hand
        GameObject heldItem = Instantiate(itemPrefabsData.GetDataOfItemType(itemToHold.itemType).holdedItemPrefab, parentObject);
        ItemData.RetextureItem(heldItem, itemToHold.itemTier, itemMaterialData);
        heldItem.name = "HeldItem"; 
    }
    //This function will update text which tells player what is he looking at. It needs X Camera Rotation from client ( in "Vector3 form")
    [Rpc(SendTo.Server)]
    void UpdateLookedAtObjectTextServerRpc(float cameraXRotation)
    {
        GameObject targetObject = GetObjectInFrontOfCamera(cameraXRotation);
        if (targetObject == null || string.IsNullOrEmpty(targetObject.tag))
        {
            DisplayTextOnScreenClientRpc("");
            return;
        }

        switch (targetObject.tag)
        {
            case "Player":
                DisplayTextOnScreenClientRpc(targetObject.GetComponent<PlayerData>().Nickname.Value);
                break;
            case "Item":
                DisplayTextOnScreenClientRpc(targetObject.GetComponent<ItemData>().itemProperties.Value.itemType.ToString());
                break;
            default:
                DisplayTextOnScreenClientRpc("");
                break;
        };
    }

    //This function will detect what object is in front of you and interact with this object (it takes cameraXRotation which is in euler angles form) 
    [Rpc(SendTo.Server)]
    void InteractWithObjectServerRpc(float cameraXRotation)
    {
        GameObject targetObject = GetObjectInFrontOfCamera(cameraXRotation);
        if (targetObject == null) { return; }
        string targetObjectTag = targetObject.tag;
        switch (targetObjectTag)
        {
            case "Item":
                //Add object to inventory (and if it wasn't added do not despawn item)
                bool didAddToInventory = transform.GetComponent<PlayerData>().AddItemToInventory(targetObject.GetComponent<ItemData>().itemProperties.Value);
                ItemData itemData = targetObject.GetComponent<ItemData>();
                if (didAddToInventory)
                {
                    //Instantiate item model which will be held in hand
                    ChangeHeldItemClientRpc(itemData.itemProperties.Value);
                    //And destroy original object
                    targetObject.GetComponent<NetworkObject>().Despawn();
                    Destroy(targetObject);
                }
                break;
        }


    }

    //tries to remove currently holded item in Inventory and spawn Item with same properties as those dropped
    [Rpc(SendTo.Server)]
    public void DropItemServerRpc()
    {
        if (playerData.Inventory[playerData.SelectedInventorySlot.Value].itemType != ItemData.ItemType.Null)
        {
            ItemData.ItemProperties itemProperties = playerData.RemoveItemFromInventory();

            //Remove held item
            ChangeHeldItemClientRpc(new ItemData.ItemProperties { itemType = ItemData.ItemType.Null });

            GameObject itemPrefab = itemPrefabsData.GetDataOfItemType(itemProperties.itemType).droppedItemPrefab;
            GameObject newItem = Instantiate(itemPrefab, transform.position + transform.forward, new Quaternion());
            newItem.GetComponent<NetworkObject>().Spawn();
            newItem.GetComponent<ItemData>().itemProperties.Value = itemProperties;
        }
        else {
            return;
        }
    }
}
