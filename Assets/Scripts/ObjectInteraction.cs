using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.UIElements;
[RequireComponent(typeof(Movement))]
[RequireComponent(typeof(PlayerData))]
public class ObjectInteraction : NetworkBehaviour
{
    Vector3 cameraOffset;
    PlayerData playerData;
    public GameObject testGameObject; //[TRANSFER OBJECT SPAWNING ON SERVER START TO GAME MANAGER!!!] !!!!

    //References to scriptable objects
    [SerializeField]
    ItemPrefabs itemPrefabsData;

    void Start()
    {
        playerData = GetComponent<PlayerData>();
        cameraOffset = gameObject.GetComponent<Movement>().CameraOffset;
        //[TRANSFER OBJECT SPAWNING ON SERVER START TO GAME MANAGER!!!] !!!!
        if (IsServer && IsOwner)
        {
            GameObject testObject = Instantiate(testGameObject, new Vector3(0, 5, 0), new Quaternion());
            testObject.GetComponent<NetworkObject>().Spawn();
        }
    }

    void Update()
    {
        if(!IsOwner) {  return; }
        float cameraXRotation = GameObject.Find("Camera").transform.rotation.eulerAngles.x;
        UpdateLookedAtObjectTextServerRpc(cameraXRotation); //This can be on update function, because it only displays data and doesn't change it in any way.
        if (Input.GetKeyDown(KeyCode.E)) //E is interaction key (for now only for picking up items)
            InteractWithObjectServerRpc(cameraXRotation);
        if (Input.GetKeyDown(KeyCode.Q)) //Q is dropping items key
            DropItemServerRpc(0);
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
                //Add object to inventory (if it wasn't added store it in var)
                bool didAddToInventory = transform.GetComponent<PlayerData>().AddItemToInventory(targetObject);
                ItemData itemData = targetObject.GetComponent<ItemData>();
                if (didAddToInventory)
                {
                    //If it was added instantiate item model which will be held in hand and destroy original object
                    GameObject holdedItem = Instantiate(itemPrefabsData.GetDataOfItemType(itemData.itemProperties.Value.itemType).holdedItemPrefab, transform);
                    holdedItem.GetComponent<NetworkObject>().Spawn();
                    //holdedItem.name = "HoldedItem";
                    targetObject.GetComponent<NetworkObject>().Despawn();
                    Destroy(targetObject);
                }
                break;
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

    [Rpc(SendTo.Owner)]
    public void DisplayTextOnScreenClientRpc(FixedString32Bytes stringToDisplay)
    {
        GameObject.Find("CenterText").GetComponent<TMP_Text>().text = stringToDisplay.ToString();
    }

    //tries to remove item in itemSlot index of Inventory and spawn Item with same properties as those dropped, returns null if nothing happenes
    [Rpc(SendTo.Server)]
    public void DropItemServerRpc(int itemSlot)
    {
        if (playerData.Inventory[0].itemType != ItemData.ItemType.Null)
        {
            ItemData.ItemProperties itemProperties = playerData.RemoveItemFromInventory(itemSlot);

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
