using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.Collections;
using Unity.Netcode;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR;
[RequireComponent(typeof(Movement))]
[RequireComponent(typeof(PlayerData))]
public class ObjectInteraction : NetworkBehaviour
{
    Vector3 cameraOffset;
    PlayerData playerData;

    //Held items will be moved according to movement of this object
    [SerializeField]
    Transform rightHand;
    //How is right hand in localPlayerModel named
    [SerializeField]
    string localRightHandPath = "hand.R";

    //References to scriptable objects
    [SerializeField]
    ItemTypeData itemTypeData;
    [SerializeField]
    ItemTierData itemTierData;

    //Network variable, because it is changed on server but client also needs to know this to display cooldown accordingly
    public float AttackingCooldown { get; private set; }

    public event Action<float> OnPunch; //event used in Animations to play animation of punching, float is cooldown, so it can be used to display cooldown slider
    public event Action OnHittingSomething; //event used in playerUI to display hitmark

    private void Awake()
    {
        playerData = GetComponent<PlayerData>();
        //subscribe DisplayInventoryClientRpc, so it is called every time Inventory changes
    }
    void Start()
    {
        cameraOffset = gameObject.GetComponent<Movement>().CameraOffset;        
    }

    void Update()
    {
        if (!IsOwner) {  return; }
        float cameraXRotation = GameObject.Find("Camera").transform.rotation.eulerAngles.x;
        if (Input.GetKeyDown(KeyCode.E)) //E is interaction key (for now only for picking up items)
            InteractWithObjectServerRpc(cameraXRotation);

        if (Input.GetKeyDown(KeyCode.T)) //T is dropping items key
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

        if (Input.GetMouseButtonDown(0))
            AttackObjectServerRpc(cameraXRotation);
    }

    //This function casts a ray in front of camera and returns gameObject which got hit by it
    //based on rotation and position of transform.player, cameraOffset (which is variable in this class) and parameter cameraXRotation (which is in degrees in "Vector3 form")
    public GameObject GetObjectInFrontOfCamera(float cameraXRotation)
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
            if (hit.collider.gameObject == gameObject) //don't detect yourself [!!!!Temporary solution: doesn't let you pick up things under you]
                return null;
            return hit.collider.gameObject;
        }
        else
            return null;
    }


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
        for(int i = 0; i < parentObject.childCount; i++) //this is because for whatever reason sometimes 2 weapons spawned and when using .Find only first was deleted
        {
            if(parentObject.GetChild(i).name == "HeldItem")
                Destroy(parentObject.GetChild(i).gameObject);
        }
            
        //Do not spawn anything when there is no item
        if (itemToHold.itemType == ItemData.ItemType.Null)
            return;
        //Spawn object in hand
        GameObject heldItem = Instantiate(itemTypeData.GetDataOfItemType(itemToHold.itemType).holdedItemPrefab, parentObject);
        ItemData.RetextureItem(heldItem, itemToHold.itemTier, itemTierData);
        heldItem.name = "HeldItem"; 
    }

    //This function will detect what object is in front of you and interact with this object (it takes cameraXRotation which is in euler angles form) 
    [Rpc(SendTo.Server)]
    void InteractWithObjectServerRpc(float cameraXRotation)
    {
        GameObject targetObject = GetObjectInFrontOfCamera(cameraXRotation);
        if (targetObject == null) { return; }
        string targetObjectTag = targetObject.tag;
        //first see if is looking at interactive object
        switch (targetObjectTag)
        {
            case "Item":
                //Add object to inventory (and if it wasn't added do not despawn item)
                ItemData itemData = targetObject.GetComponent<ItemData>();
                bool didAddToInventory = transform.GetComponent<PlayerData>().AddItemToInventory(itemData.itemProperties.Value);
                if (didAddToInventory)
                {
                    //Instantiate item model which will be held in hand (it is needed for cases when player doesn't hold anything and picks up item)
                    ChangeHeldItemClientRpc(playerData.Inventory[playerData.SelectedInventorySlot.Value]);
                    //And destroy original object
                    targetObject.GetComponent<NetworkObject>().Despawn();
                    Destroy(targetObject);
                }
                return;
        }
        //if is not looking at interactive object, check if has interactible item in hand
        switch (playerData.Inventory[playerData.SelectedInventorySlot.Value].itemType)
        {
            case ItemData.ItemType.Medkit:
                playerData.ChangeHealth(30);
                playerData.RemoveItemFromInventory(playerData.SelectedInventorySlot.Value);
                ChangeHeldItemClientRpc(new ItemData.ItemProperties { itemType = ItemData.ItemType.Null });
                break;
        }
    }

    //Function that does of all things that happen when you press left mouse button
    [Rpc(SendTo.Server)]
    void AttackObjectServerRpc(float cameraXRotation)
    {
        if (AttackingCooldown > 0) { return; } //If cooldown not zero then ignore rest of code, because nothing will happen anyways

        //make it possible to punch nothing and punish player for doing that
        playerData.ChangeHunger(-2); //TO DO: CHECK IF HOLDING SOMETHING BEFORE DOING THIS!
        AttackingCooldown = 1f;
        InvokeOnPunchEventOwnerRpc(AttackingCooldown);
        StartCoroutine(DeacreaseCooldown());

        GameObject targetObject = GetObjectInFrontOfCamera(cameraXRotation);
        if (targetObject == null) { return; }
        string targetObjectTag = targetObject.tag;

        switch (targetObjectTag)
        {
            case "Player":
                targetObject.GetComponent<PlayerData>().ChangeHealth(-20);
                OnHittingSomething.Invoke();
                break;
        }
    }


    //tries to remove currently holded item in Inventory and spawn Item with same properties as those dropped
    [Rpc(SendTo.Server)]
    public void DropItemServerRpc()
    {
        ItemData.ItemProperties itemProperties = playerData.RemoveItemFromInventory(playerData.SelectedInventorySlot.Value);

        if(itemProperties.itemType == ItemData.ItemType.Null) { return; } //if it is null return, because there is no item there to spawn

        //Remove held item
        ChangeHeldItemClientRpc(new ItemData.ItemProperties { itemType = ItemData.ItemType.Null });

        GameObject itemPrefab = itemTypeData.GetDataOfItemType(itemProperties.itemType).droppedItemPrefab;
        GameObject newItem = Instantiate(itemPrefab, transform.position + transform.forward, new Quaternion());
        newItem.GetComponent<NetworkObject>().Spawn();
        newItem.GetComponent<ItemData>().itemProperties.Value = itemProperties;
    }

    //This function only exists, because I need to call OnPunch on owner, and only want to do this when server detects punch and no cooldown
    [Rpc(SendTo.Owner)]
    void InvokeOnPunchEventOwnerRpc(float maximumCooldownValue)
    {
        OnPunch.Invoke(maximumCooldownValue); //cooldown float is 0, because it is invoked on owner, to play animations so it doesn't matter
    }

    IEnumerator DeacreaseCooldown()
    {
        if (!IsServer) throw new Exception("attackingCooldown is only server side variable! Do not call this method on client!!!!");
        while (AttackingCooldown >= 0)
        {
            AttackingCooldown -= 0.01f;
            yield return new WaitForSeconds(0.01f);
        }
    }
}
