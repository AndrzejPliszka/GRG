using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using Unity.Netcode;
using UnityEditor.PackageManager;
using UnityEngine;
using static PlayerData;
using static UnityEngine.Rendering.DebugUI;

/// <summary>
/// For given amountOfMinimumMaterial stores what is object that should be displayed. Used In StorageMaterialData. 
/// I use this class instead of key value pair like in dictionary, so it can be serializable and settable in inspector.
/// </summary>
[System.Serializable]
public class MaterialDisplayObjectData
{
    public int amountOfMinimumMaterial;
    public GameObject displayGameObject;
}

/// <summary>
/// Determines where and how given materials will be displayed. Usefull when storage stores more than one material. 
/// displayParent is Transform that will be parent of instantianted object representing material.
/// DisplayMaterialObjects specifies what object should be displayed on minimum value of material.
/// </summary>
[System.Serializable]
public class StorageMaterialData
{
    public PlayerData.RawMaterial materialType;
    public Transform displayParent;
    public List<MaterialDisplayObjectData> displayedMaterialObjects;
}

public class Storage : NetworkBehaviour
{
    enum DisplayMethod{
        SingularHeap, //Only works when there is one material in StoredMaterialData
        SmallBatches
    }

    [SerializeField] int townId;

    //TODO: make that you don't see this in inspector unless SingularHeap is selected materialDisplayMethod
    //Used only when there is SingularHeap
    [SerializeField] GameObject storedMaterialObject;
    [SerializeField] float maximumMaterialLevel;
    [SerializeField] float yOffset = -0.25f;
    float yOriginalPosition;

    //Used when small batches are used
    [SerializeField] List<StorageMaterialData> materialDisplayData;

    public NetworkVariable<ulong> OwnerId = new(0); //Netcode player id, not object id


    //This script may be attached to other objects that have material storing function (such as workshops), not only stand alone storage
    [SerializeField] DisplayMethod materialDisplayMethod;


    [SerializeField] List<PlayerData.MaterialData> _storedMaterialData;
    [HideInInspector] public NetworkList<PlayerData.MaterialData> StoredMaterialData = new();

    public NetworkVariable<float> SellingPrice { get; private set; } = new(1);
    public NetworkVariable<float> BuyingPrice { get; private set; } = new(1);

    public override void OnNetworkSpawn()
    {
        foreach (PlayerData.MaterialData materialData in _storedMaterialData)
            StoredMaterialData.Add(materialData);


        if(materialDisplayMethod == DisplayMethod.SingularHeap)
        {
            if (StoredMaterialData.Count > 1)
                throw new Exception("This display method doesn't support multiple materials in one storage");

            yOriginalPosition = storedMaterialObject.transform.position.y;
            ChangeLevelOfMaterial(StoredMaterialData[0].amount, StoredMaterialData[0].maxAmount);
            StoredMaterialData.OnListChanged += networkListEvent =>
            {
                ChangeLevelOfMaterial(networkListEvent.Value.amount, networkListEvent.Value.maxAmount);
            };
        } 
        else if (materialDisplayMethod == DisplayMethod.SmallBatches)
        {
            StoredMaterialData.OnListChanged += ChangeBatchesOfMaterial;
        }

        if (OwnerId.Value == 0 && IsServer && !IsHost)
            OwnerId.Value = NetworkManager.Singleton.LocalClientId; //Set owner to local client if it is server and owner is not set yet
        if (IsServer)
        {
            StoredMaterialData.OnListChanged += networkListEvent =>
            {
                AdjustDroppedItems(networkListEvent.PreviousValue.amount, networkListEvent.Value.amount);
            };
        }
    }

    /// <summary>
    /// Updates the displayed material objects to match specification from materialDisplayData. Should be called when StoredMaterialData is changed.
    /// </summary>
    /// <param name="materialListEvent">The event containing changes of the material data that are processed.</param>
    void ChangeBatchesOfMaterial(NetworkListEvent<PlayerData.MaterialData> materialListEvent)
    {
        if (materialDisplayMethod != DisplayMethod.SmallBatches)
            throw new Exception("This method should be used ONLY when displayMethod is SmallBatches!");
        foreach (var material in materialDisplayData) {
            if (material.materialType == materialListEvent.Value.materialType)
            {
                MaterialDisplayObjectData finalMaterialObject = material.displayedMaterialObjects[0];
                int currentValue = materialListEvent.Value.amount;
                foreach(var materialObject in material.displayedMaterialObjects)
                {
                    if (finalMaterialObject.amountOfMinimumMaterial < materialObject.amountOfMinimumMaterial &&
                        materialObject.amountOfMinimumMaterial <= currentValue)
                        finalMaterialObject = materialObject;
                }


                foreach (Transform child in material.displayParent)
                    Destroy(child.gameObject);
                Instantiate(finalMaterialObject.displayGameObject, material.displayParent);
            }
        }
    }

    public void ChangeLevelOfMaterial(int currentSupply, int maxSupply)
    {
        storedMaterialObject.transform.position = new Vector3(
            storedMaterialObject.transform.position.x, yOriginalPosition + (1.0f * currentSupply / maxSupply) * maximumMaterialLevel + yOffset, storedMaterialObject.transform.position.z); //multiplying times 1.0f to avoid integer devision and always getting 0
    }

    /// <summary>
    /// Retrieves the material data object associated with the specified raw material.
    /// </summary>
    /// <param name="rawMaterial">The raw material for which to retrieve the associated material data.</param>
    /// <returns>The material data object corresponding to the specified raw material.</returns>
    /// <exception cref="Exception">Thrown when the specified raw material cannot be stored in storage.</exception>

    public PlayerData.MaterialData GetMaterialDataOfRawMaterial(PlayerData.RawMaterial rawMaterial)
    {
        foreach (PlayerData.MaterialData material in StoredMaterialData)
        {
            if (material.materialType == rawMaterial)
                return material;
        }
        throw new Exception($"This storage doesn't store material: {rawMaterial}");
    }

    /// <summary>
    /// Updates material data object associated with rawMaterial.
    /// </summary>
    /// <param name="rawMaterial">Raw material for which materialData will be updated</param>
    /// <param name="materialData">MaterialData struct that will replace existing data to corresponding raw material</param>
    /// <exception cref="Exception">Thrown when the specified raw material cannot be stored in storage.</exception>
    public void UpdateMaterialDataGivenRawMaterial(PlayerData.RawMaterial rawMaterial, PlayerData.MaterialData materialData)
    {
        for (int i = 0; i < StoredMaterialData.Count; i++)
        {
            if (StoredMaterialData[i].materialType == rawMaterial)
            {
                StoredMaterialData[i] = materialData;
                return;
            }
        }
        throw new Exception($"This storage doesn't store material: {rawMaterial}");
    }

    /// <summary>
    /// Checks if rawMaterial can be stored (it has corresponding element in StoredMaterialData)
    /// </summary>
    /// <param name="rawMaterial">Raw material to check</param>
    /// <returns>True if can be stored, false if it can't</returns>
    public bool IsRawMaterialInStoredMaterialData(PlayerData.RawMaterial rawMaterial)
    {
        for (int i = 0; i < StoredMaterialData.Count; i++)
        {
            if (StoredMaterialData[i].materialType == rawMaterial)
                return true;
        }
        return false;
    }

    //Change this function to just deny selling more materials then it can hold
    public bool ChangeAmountOfMaterialInStorage(PlayerData.RawMaterial material, int amountToIncrease)
    {
        if (!IsServer) { throw new System.Exception("Only server can add material!"); }

        PlayerData.MaterialData materialData = GetMaterialDataOfRawMaterial(material);
        materialData.amount += amountToIncrease;
        if (materialData.amount > materialData.maxAmount)
        {
            return false;
        }
        else if (materialData.amount < 0)
        {
            return false;
        }
        else
        {
            UpdateMaterialDataGivenRawMaterial(material, materialData);
        }
        return true;
    }

    void AdjustDroppedItems(int oldSupply, int newSupply)
    {
        if (!IsServer)
            throw new System.Exception("Only server can change drop of items");
        if (!TryGetComponent<BreakableStructure>(out var breakableStructure))
            return;

        foreach (MaterialData storageMaterialData in StoredMaterialData)
        {
            bool wasMaterialUpdated = false;
            for (int i = 0; i < breakableStructure.droppedMaterials.Count; i++)
            {
                if (breakableStructure.droppedMaterials[i].materialType == storageMaterialData.materialType)
                {
                    PlayerData.MaterialData newDroppedMaterial = breakableStructure.droppedMaterials[i];
                    newDroppedMaterial.amount += (newSupply - oldSupply);
                    breakableStructure.droppedMaterials[i] = newDroppedMaterial;
                    wasMaterialUpdated = true;
                    break;
                }
            }
            if (!wasMaterialUpdated)
            {
                breakableStructure.droppedMaterials.Add(storageMaterialData);
            }

        }
    }

    //For now below functions use id of gameObjects which are players, and not id of clients (except owner which uses NetworkClientId, as it is easier to test) TODO: MAKE THIS UNIFORM
    //[TODO: also make validation f.e. if client is near storage]
    [Rpc(SendTo.Server)]
    public void SellMaterialsServerRpc(ulong playerId, int amountOfMaterials, RawMaterial material) {
        bool isSellerOwner = OwnerId.Value == playerId;
        if (NetworkManager.Singleton.ConnectedClients.TryGetValue(OwnerId.Value, out NetworkClient ownerClient) && NetworkManager.Singleton.ConnectedClients.TryGetValue(playerId, out NetworkClient sellerClient))
        {
            PlayerData ownerData = ownerClient.PlayerObject.gameObject.GetComponent<PlayerData>();
            PlayerData sellerData = sellerClient.PlayerObject.gameObject.GetComponent<PlayerData>();
            MaterialData materialData = GetMaterialDataOfRawMaterial(material);

            //In edge case when owner wants to put things in storage, paying money etc. is not needed
            if (isSellerOwner && amountOfMaterials <= materialData.maxAmount - materialData.amount &&
                sellerData.GetMaterialDataOfOwnedRawMaterial(material).amount >= amountOfMaterials)
            {
                sellerData.GetComponent<PlayerData>().ChangeAmountOfMaterial(material, -amountOfMaterials);
                ChangeAmountOfMaterialInStorage(material, amountOfMaterials);
            }
            

            if (!isSellerOwner && ownerData.Money.Value >= (amountOfMaterials * SellingPrice.Value) &&
                amountOfMaterials <= materialData.maxAmount - materialData.amount &&
                sellerData.GetMaterialDataOfOwnedRawMaterial(material).amount >= amountOfMaterials)
            {
                ownerData.ChangeMoney(-amountOfMaterials * SellingPrice.Value);
                sellerData.ChangeMoney(amountOfMaterials * SellingPrice.Value);
                sellerData.GetComponent<PlayerData>().ChangeAmountOfMaterial(material, -amountOfMaterials);
                ChangeAmountOfMaterialInStorage(material, amountOfMaterials);
            }
        }
        else
        {
            throw new Exception(playerId + " player (or owner " + OwnerId.Value + " of storage) was not found!");
        }
    }
    [Rpc(SendTo.Server)]
    public void BuyMaterialsServerRpc(ulong playerId, int amountOfMaterials, RawMaterial material)
    {
        bool isBuyerOwner = OwnerId.Value == playerId; //Check if buyer is owner of this client, so we can use OwnerId.Value
        if (NetworkManager.Singleton.ConnectedClients.TryGetValue(OwnerId.Value, out NetworkClient ownerClient) && NetworkManager.Singleton.ConnectedClients.TryGetValue(playerId, out NetworkClient buyerClient))
        {
            PlayerData ownerData = ownerClient.PlayerObject.gameObject.GetComponent<PlayerData>();
            PlayerData buyerData = buyerClient.PlayerObject.gameObject.GetComponent<PlayerData>();
            MaterialData materialData = GetMaterialDataOfRawMaterial(material);

            //In edge case when owner wants to get things from storage, paying money etc. is not needed
            if (isBuyerOwner && amountOfMaterials <= materialData.amount && amountOfMaterials <= buyerData.GetMaterialDataOfOwnedRawMaterial(material).maxAmount - buyerData.GetMaterialDataOfOwnedRawMaterial(material).amount)
            {
                ChangeAmountOfMaterialInStorage(material, -amountOfMaterials);
                buyerData.GetComponent<PlayerData>().ChangeAmountOfMaterial(material, amountOfMaterials);
                return;
            }

            if (buyerData.Money.Value >= (amountOfMaterials * BuyingPrice.Value) &&
                amountOfMaterials <= materialData.amount &&
                amountOfMaterials <= buyerData.GetMaterialDataOfOwnedRawMaterial(material).maxAmount - buyerData.GetMaterialDataOfOwnedRawMaterial(material).amount)
            {
                ownerData.ChangeMoney(amountOfMaterials * BuyingPrice.Value);
                buyerData.ChangeMoney(-amountOfMaterials * BuyingPrice.Value);
                buyerData.GetComponent<PlayerData>().ChangeAmountOfMaterial(material, amountOfMaterials);
                ChangeAmountOfMaterialInStorage(material, -amountOfMaterials);
            }
        }
        else
        {
            throw new Exception(playerId + " player (or owner " + OwnerId.Value + " of storage) was not found!");
        }
    }
}
