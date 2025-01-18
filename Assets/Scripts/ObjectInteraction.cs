using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
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
        {
            //DebugFunctionServerRpc(2 * NetworkManager.Singleton.ServerTime.Time - NetworkManager.Singleton.LocalTime.Time);

            if (IsHost)
                AttackObjectServerRpc(cameraXRotation, NetworkManager.Singleton.ServerTime.Tick);
            else
                //I subtract 3 from here, because it doesn't work without it: server simulates time which was too early for some reason
                AttackObjectServerRpc(cameraXRotation, NetworkManager.Singleton.ServerTime.Tick - 3);
        }
    }

    //DELETE!!! THIS FUnCTION IS ONLY FOR DEBUG PURPOSES!
    [Rpc(SendTo.Server)]
    void DebugFunctionServerRpc(double currentTick)
    {
        Debug.Log("Past tick:                                                                  " + currentTick);
    }

    //This function casts a ray in front of camera and returns gameObject which got hit by it
    //based on rotation and position of transform.player, cameraOffset (which is variable in this class) and parameter cameraXRotation (which is in degrees in "Vector3 form")
    public GameObject GetObjectInFrontOfCamera(float cameraXRotation, int timeWhenHit = 0)
    {
        LagCompensation lagCompensation = null;
        Movement movement = GetComponent<Movement>();
        if (!IsServer && timeWhenHit != 0)
        {
            Debug.LogWarning("You specified server side property (timeWhenHit) on client!");
            timeWhenHit = 0;
        }

        if (timeWhenHit != 0) //if this is 0, we don't care about accuracy, so we dont need to do lag compensation
        {
            lagCompensation = gameObject.GetComponent<LagCompensation>();
            lagCompensation.SimulatePlayersOnGivenTime(timeWhenHit);
        }

        Vector3 cameraOffset = movement.CameraOffset;
        Quaternion verticalRotation = Quaternion.Euler(cameraXRotation, transform.rotation.eulerAngles.y, 0);
        Vector3 rayDirection = verticalRotation * Vector3.forward; //Changing quaternion into vector3, because Ray takes Vector3

        Vector3 currentPosition = IsServer ? transform.position : movement.LocalPlayerModel.transform.position; //if not on server use localmodel with more "Up to date" position and rotation
        Quaternion currentRotation = IsServer ? transform.rotation : movement.LocalPlayerModel.transform.rotation;
        Vector3 cameraPosition = currentPosition + new Vector3(0, cameraOffset.y) + currentRotation * new Vector3(0, 0, cameraOffset.z);

        Ray ray = new(cameraPosition, rayDirection);
        Debug.DrawRay(cameraPosition, rayDirection * 100f, Color.red, 0.01f);
        LayerMask layersToDetect;
        if (timeWhenHit != 0)
            layersToDetect = ~LayerMask.GetMask("UnsyncedObject", "LocalObject"); //when timeWhenHit is specified, then it is on server so, do not check unsynced player side object
        else
            layersToDetect = ~LayerMask.GetMask("LocalObject"); //else it is probably executed on client (and even if not if timeWhenHit is not specified we dont probably care so much about accuracy), so we can detect everything (except localPlayerModel)

        RaycastHit[] hits = new RaycastHit[10]; //LIMITATION: if looking at more than 10 things it can interact with NOT the first thing in front of camera! (it is becuse hits in raycastNonAlloc needs to be array and arrays have fixed length)
        int numberOfHits = Physics.RaycastNonAlloc(ray, hits, 10, layersToDetect);
        if (numberOfHits != 0)
        {
            RaycastHit hit = getClosestRay(hits);
            if (hit.transform == transform) {
                if (numberOfHits < 2)
                    return hit.transform.gameObject;
                hits = hits.Where(hit => hit.transform != transform).ToArray(); //We are modifying hits array, so there is no this gameobject in it
                hit = getClosestRay(hits);
            }
            //if it is simplified player, find "original" one with data and return it
            if (hit.transform.CompareTag("SimplifiedPlayer") && NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(ulong.Parse(hit.transform.gameObject.name), out NetworkObject playerPrefab))
            {
                if (timeWhenHit != 0)
                    lagCompensation.DestroySimulatedPlayers();
                return playerPrefab.gameObject;
            }
            //else return object that was hit
            if (timeWhenHit != 0)
                lagCompensation.DestroySimulatedPlayers();
            return hit.transform.gameObject;
        }
        if (timeWhenHit != 0)
            lagCompensation.DestroySimulatedPlayers();
        return null;
    }

    //This function exists, because RaycastNonAlloc doesn't return rays in order
    static RaycastHit getClosestRay(RaycastHit[] hits) {
        RaycastHit closestHit = hits[0];

        foreach (RaycastHit hit in hits)
        {
            if (hit.collider == null) //do not consider null colliders, that are in array to fill it up
                continue;
            if (hit.distance < closestHit.distance)
                closestHit = hit;
        }
        return closestHit;
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
    void AttackObjectServerRpc(float cameraXRotation, int timeOfAttack)
    {
        if (AttackingCooldown > 0) { return; } //If cooldown not zero then ignore rest of code, because nothing will happen anyways

        //make it possible to punch nothing and punish player for doing that
        playerData.ChangeHunger(-2); //TO DO: CHECK IF HOLDING SOMETHING BEFORE DOING THIS!
        AttackingCooldown = 1f;
        InvokeOnPunchEventOwnerRpc(AttackingCooldown);
        StartCoroutine(DeacreaseCooldown());


        GameObject targetObject = GetObjectInFrontOfCamera(cameraXRotation, timeOfAttack);
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
        GameObject newItem = Instantiate(itemPrefab, transform.position + transform.forward, transform.rotation);
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
