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
    public event Action<GameObject> OnHittingSomething; //event used in playerUI to display hitmark, atribute is tag of hit object

    public bool canInteract = true;

    private void Awake()
    {
        playerData = GetComponent<PlayerData>();
    }
    void Start()
    {
        cameraOffset = gameObject.GetComponent<Movement>().CameraOffset;
    }

    void Update()
    {
        if (!IsOwner) {  return; }
        float cameraXRotation = GameObject.Find("Camera").transform.rotation.eulerAngles.x;
        if (!canInteract)
            return;

        if (Input.GetKeyDown(KeyCode.E)) //E is interaction key
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

            if (IsHost)
                AttackObjectServerRpc(cameraXRotation, NetworkManager.Singleton.ServerTime.Tick);
            else
                //I subtract 3 from here, because it doesn't work without it: server simulates time which was too early for some reason
                AttackObjectServerRpc(cameraXRotation, NetworkManager.Singleton.ServerTime.Tick - 3);
        }
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
        Vector3 currentPosition = IsServer ? transform.position : movement.LocalPlayerModel.transform.position; //if not on server use localmodel with more "Up to date" position and rotation
        Quaternion currentRotation = IsServer ? transform.rotation : movement.LocalPlayerModel.transform.rotation;
        Vector3 cameraPosition = currentPosition + new Vector3(0, cameraOffset.y) + currentRotation * new Vector3(0, 0, cameraOffset.z);

        Quaternion verticalRotation = Quaternion.Euler(cameraXRotation, currentRotation.eulerAngles.y, 0);
        Vector3 rayDirection = verticalRotation * Vector3.forward; //Changing quaternion into vector3, because Ray takes Vector3

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
            RaycastHit hit = GetClosestRay(hits);
            if (hit.transform == transform) {
                if (numberOfHits < 2)
                    return hit.transform.gameObject;
                hits = hits.Where(hit => hit.transform != transform).ToArray(); //We are modifying hits array, so there is no this gameobject in it
                hit = GetClosestRay(hits);
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
    static RaycastHit GetClosestRay(RaycastHit[] hits) {
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
        Shop shopScript; //declared here to avoid scope issues
        MoneyObject moneyObject;
        House houseScript;
        Storage storage;
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
            case "Buy":
                shopScript = targetObject.transform.parent.GetComponent<Shop>();
                if (shopScript == null)
                    throw new Exception("Parent of object with BuyingPlace, does not have Shop script, modify hierarchy or this script accordingly!");
                shopScript.BuyFromShop(gameObject);
                ChangeHeldItemClientRpc(playerData.Inventory[playerData.SelectedInventorySlot.Value]);
                return;
            case "Work":
                shopScript = targetObject.transform.parent.GetComponent<Shop>();
                if (shopScript == null)
                    throw new Exception("Parent of object with BuyingPlace, does not have Shop script, modify hierarchy or this script accordingly!");
                shopScript.WorkInShop(gameObject);
                return;
            case "Money":
                moneyObject = targetObject.transform.GetComponent<MoneyObject>();
                float moneyAmount = moneyObject.moneyAmount.Value;
                playerData.ChangeMoney(moneyAmount);
                targetObject.GetComponent<NetworkObject>().Despawn();
                Destroy(targetObject);
                return;
            case "House":
                houseScript = targetObject.GetComponent<House>();
                houseScript.BuyHouse(gameObject);
                break;
            case "Parliament":
                GetComponent<CouncilorMenu>().InitiateMenuOwnerRpc();
                break;
            case "Storage":
                //for now only storage prefab has collider in child, TO DO: Change to not depend on hierarchy
                storage = targetObject.transform.parent.GetComponent<Storage>();
                //Make this line not retarded
                GetComponent<PlayerUI>().DisplayStorageTradeMenuOwnerRpc(targetObject.transform.parent.GetComponent<NetworkObject>().NetworkObjectId, playerData.OwnedMaterials[(int)storage.StoredMaterial.Value].amount);
                /*
                int excessiveMaterials = storage.ChangeAmountOfMaterialInStorage(playerData.OwnedMaterials[(int)storage.StoredMaterial.Value].amount);
                if(excessiveMaterials > 0)
                    playerData.SetAmountOfMaterial(storage.StoredMaterial.Value, excessiveMaterials);
                else
                    playerData.SetAmountOfMaterial(storage.StoredMaterial.Value, 0); */
                break;
            case "BerryBush":
                BerryBush berryBush = targetObject.GetComponent<BerryBush>();
                if (berryBush.HasBerries.Value)
                {
                    playerData.ChangeHunger(berryBush.FoodAmount);
                    berryBush.RemoveBerries();
                }
                else
                {
                    PlayerUI playerUI = GetComponent<PlayerUI>();
                    if (playerUI)
                        playerUI.DisplayErrorOwnerRpc("There are no berries on this bush!");
                }
                break;
            case "MaterialObject":
            case "GatherableMaterial":
                GatherableMaterial materialItem = targetObject.GetComponent<GatherableMaterial>();
                int didSucced = playerData.ChangeAmountOfMaterial(materialItem.Material.Value, materialItem.Amount.Value);
                if (didSucced == 0) //doesn't handle material.Material.Value > 1)
                {
                    materialItem.GetComponent<NetworkObject>().Despawn();
                    Destroy(targetObject.transform.gameObject);
                }
                break;

        }

        //if is not looking at interactive object, check if has interactible item in hand
        float itemTierValueMultiplier = itemTierData.GetDataOfItemTier(playerData.Inventory[playerData.SelectedInventorySlot.Value].itemTier).multiplier;
        switch (playerData.Inventory[playerData.SelectedInventorySlot.Value].itemType)
        {
            case ItemData.ItemType.Medkit:
                int baseMedkitHealhValue = 30;
                int medkitHealhValue = Convert.ToInt16(baseMedkitHealhValue * itemTierValueMultiplier);
                playerData.ChangeHealth(medkitHealhValue);
                playerData.RemoveItemFromInventory(playerData.SelectedInventorySlot.Value);
                ChangeHeldItemClientRpc(new ItemData.ItemProperties { itemType = ItemData.ItemType.Null });
                break;
            case ItemData.ItemType.Food:
                int baseFoodHungerValue = 30;
                int foodHungerValue = Convert.ToInt16(baseFoodHungerValue * itemTierValueMultiplier);
                playerData.ChangeHunger(foodHungerValue);
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
        ItemData.ItemProperties heldItem = playerData.Inventory[playerData.SelectedInventorySlot.Value]; 

        if(heldItem.itemType == ItemData.ItemType.Null)
            playerData.ChangeHunger(-2);
        AttackingCooldown = 1f;
        InvokeOnPunchEventOwnerRpc(AttackingCooldown);
        StartCoroutine(DeacreaseCooldown());

        GameObject targetObject = GetObjectInFrontOfCamera(cameraXRotation, timeOfAttack);
        if (targetObject == null) { return; }

        float itemTierValueMultiplier = itemTierData.GetDataOfItemTier(playerData.Inventory[playerData.SelectedInventorySlot.Value].itemTier).multiplier;
        string targetObjectTag = targetObject.tag;
        int baseAttack = -20;
        switch (targetObjectTag)
        {
            case "Player":
                if (heldItem.itemType == ItemData.ItemType.Sword)
                    baseAttack = Convert.ToInt16(baseAttack * itemTierValueMultiplier);
                else if(heldItem.itemType == ItemData.ItemType.Null)
                    baseAttack = Convert.ToInt16(baseAttack * (1f/2f)); //when punching someone with fist, deal half of damage of weakest sword 
                else
                    break;

                targetObject.GetComponent<PlayerData>().ChangeHealth(baseAttack);
                OnHittingSomething.Invoke(targetObject);
                break;
            case "Tree":
                if (heldItem.itemType == ItemData.ItemType.Axe)
                    baseAttack = Convert.ToInt16(baseAttack * itemTierValueMultiplier);
                else
                    break;
                

                targetObject.GetComponent<BreakableStructure>().ChangeHealth(baseAttack);
                OnHittingSomething.Invoke(targetObject);

                break;
            case "Shop":
                if (heldItem.itemType == ItemData.ItemType.Sword)
                    baseAttack = Convert.ToInt16(baseAttack * itemTierValueMultiplier);
                else if(heldItem.itemType == ItemData.ItemType.Hammer)
                    baseAttack = Convert.ToInt16(baseAttack * itemTierValueMultiplier * -1); //hammer repairs shops, so we multiply times -1 so we add health to shop
                else
                    break;

                targetObject.GetComponent<BreakableStructure>().ChangeHealth(baseAttack);
                OnHittingSomething.Invoke(targetObject);
                break;
            case "Crop":
                if (heldItem.itemType == ItemData.ItemType.Sickle)
                    baseAttack = Convert.ToInt16(baseAttack * itemTierValueMultiplier);
                else
                    break;

                targetObject.GetComponent<BreakableStructure>().ChangeHealth(baseAttack);
                OnHittingSomething.Invoke(targetObject);
                break;
            case "Rock":
                if (heldItem.itemType == ItemData.ItemType.Pickaxe)
                    baseAttack = Convert.ToInt16(baseAttack * itemTierValueMultiplier);
                else
                    break;

                targetObject.GetComponent<BreakableStructure>().ChangeHealth(baseAttack);
                OnHittingSomething.Invoke(targetObject);
                break;
            default: //here put all things that don't require punching at specific object
                if(heldItem.itemType == ItemData.ItemType.FishingRod)
                {
                    if (!GetComponent<Collider>())
                        return;

                    //Check if is in/near water (currently works kinda weird)
                    Collider[] collisionResults = new Collider[10];
                    CapsuleCollider collider = GetComponent<CapsuleCollider>();

                    Vector3 center = collider.bounds.center;
                    float radius = collider.radius;
                    float height = collider.height * 0.5f - radius;
                    Vector3 direction = Vector3.up;

                    switch (collider.direction)
                    {
                        case 0: direction = Vector3.right; break;   // X-axis
                        case 1: direction = Vector3.up; break;      // Y-axis
                        case 2: direction = Vector3.forward; break; // Z-axis
                    }

                    Vector3 point1 = center + direction * height;
                    Vector3 point2 = center - direction * height;

                    int hitNumber = Physics.OverlapCapsuleNonAlloc(point1, point2, radius, collisionResults);

                    for (int i = 0; i < hitNumber; i++)
                    {
                        if (collisionResults[i] != collider && collisionResults[i].CompareTag("Water"))
                        {
                            //Fishing successfull
                            OnHittingSomething.Invoke(null);
                            if (playerData)
                                playerData.ChangeMoney(2f * itemTierValueMultiplier);
                            return;
                        }
                    }
                    PlayerUI playerUI = GetComponent<PlayerUI>();
                    if (playerUI)
                        playerUI.DisplayErrorOwnerRpc("You need to be near water to fish!");
                }
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

    //Handling interaction with (raw) material objects (maybe move to other script?)
    private void OnControllerColliderHit(ControllerColliderHit collision)
    {
        if (!IsServer) { return; } //Only server side should handle this, because it is changing playerData

        if (collision.transform.CompareTag("MaterialObject") && collision.transform.GetComponent<NetworkObject>().IsSpawned)
        {
            GatherableMaterial material = collision.transform.GetComponent<GatherableMaterial>();
            int didSucced = playerData.ChangeAmountOfMaterial(material.Material.Value, material.Amount.Value);
            if(didSucced == 0) //DOESNT HANDLE NUMBERS GREATER THAN 1 (can happen if material.Material.Value > 1)
            {
                material.GetComponent<NetworkObject>().Despawn();
                Destroy(collision.transform.gameObject);
            }
            
        }
    }
}
