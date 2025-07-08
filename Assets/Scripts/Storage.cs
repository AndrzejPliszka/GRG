using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class Storage : NetworkBehaviour
{
    [SerializeField] int townId;


    [SerializeField] GameObject storedMaterialObject;
    [SerializeField] float maximumMaterialLevel;
    [SerializeField] float yOffset = -0.25f;

    public NetworkVariable<ulong> OwnerId { get; private set; } = new(0);
    [field: SerializeField] public NetworkVariable<PlayerData.RawMaterial> StoredMaterial { get; private set; }
    public NetworkVariable<int> CurrentSupply { get; private set; } = new();
    [field: SerializeField] public NetworkVariable<int> MaxSupply { get; private set; } = new();
    public NetworkVariable<float> SellingPrice { get; private set; } = new(1);
    public NetworkVariable<float> BuyingPrice { get; private set; } = new(1);

    public override void OnNetworkSpawn()
    {
        ChangeLevelOfMaterial(CurrentSupply.Value, MaxSupply.Value);
        CurrentSupply.OnValueChanged += (int oldValue, int value) =>
        {
            ChangeLevelOfMaterial(value, MaxSupply.Value);
            if(townId >= 0 && IsServer)
                GameManager.Instance.ChangeMaterialAmount(townId, StoredMaterial.Value, value);
        };
        MaxSupply.OnValueChanged += (int oldValue, int value) =>
        {
            ChangeLevelOfMaterial(CurrentSupply.Value, value);
            if (townId >= 0 && IsServer)
                GameManager.Instance.ChangeMaxMaterialAmount(townId, StoredMaterial.Value, value);
        };
    }

    public void ChangeLevelOfMaterial(int currentSupply, int maxSupply)
    {
        storedMaterialObject.transform.position = new Vector3(
            storedMaterialObject.transform.position.x, (1.0f * currentSupply / maxSupply) * maximumMaterialLevel + yOffset, storedMaterialObject.transform.position.z); //multiplying times 1.0f to avoid integer devision and always getting 0
    }

    //Change this function to just deny selling more materials then it can hold
    public bool ChangeAmountOfMaterialInStorage(int amountToIncrease)
    {
        if (!IsServer) { throw new System.Exception("Only server can add material!"); }
        int newAmount = CurrentSupply.Value + amountToIncrease;
        if (newAmount > MaxSupply.Value)
        {
            return false;
        }
        else if (newAmount < 0)
        {
            return false;
        }
        else
        {
            CurrentSupply.Value = newAmount;
        }
        return true;
    }
    //For now below functions use id of gameObjects which are players, and not id of clients (except owner which uses NetworkClientId, as it is easier to test) TODO: MAKE THIS UNIFORM
    //[TODO: also make validation f.e. if client is near storage]
    [Rpc(SendTo.Server)]
    public void SellMaterialsServerRpc(ulong playerId, int amountOfMaterials) {
        if (NetworkManager.Singleton.ConnectedClients.TryGetValue(0, out NetworkClient client))
        {
            PlayerData ownerData = client.PlayerObject.gameObject.GetComponent<PlayerData>();
            PlayerData sellerData = NetworkManager.SpawnManager.SpawnedObjects[playerId].gameObject.GetComponent<PlayerData>();

            if (ownerData.Money.Value >= (amountOfMaterials * SellingPrice.Value) &&
                amountOfMaterials <= MaxSupply.Value - CurrentSupply.Value &&
                sellerData.OwnedMaterials[(int)StoredMaterial.Value].amount >= amountOfMaterials)
            {
                ownerData.ChangeMoney(-amountOfMaterials * SellingPrice.Value);
                sellerData.ChangeMoney(amountOfMaterials * SellingPrice.Value);
                sellerData.GetComponent<PlayerData>().ChangeAmountOfMaterial(StoredMaterial.Value, -amountOfMaterials);
                ChangeAmountOfMaterialInStorage(amountOfMaterials);

            }
        }
    }
    [Rpc(SendTo.Server)]
    public void BuyMaterialsServerRpc(ulong playerId, int amountOfMaterials)
    {
        if (NetworkManager.Singleton.ConnectedClients.TryGetValue(OwnerId.Value, out NetworkClient client))
        {
            PlayerData ownerData = client.PlayerObject.gameObject.GetComponent<PlayerData>();
            PlayerData buyerData = NetworkManager.SpawnManager.SpawnedObjects[playerId].gameObject.GetComponent<PlayerData>();

            if (buyerData.Money.Value >= (amountOfMaterials * BuyingPrice.Value) &&
                amountOfMaterials <= CurrentSupply.Value &&
                amountOfMaterials <= buyerData.OwnedMaterials[(int)StoredMaterial.Value].maxAmount - buyerData.OwnedMaterials[(int)StoredMaterial.Value].amount)
            {
                ownerData.ChangeMoney(amountOfMaterials * BuyingPrice.Value);
                buyerData.ChangeMoney(-amountOfMaterials * BuyingPrice.Value);
                buyerData.GetComponent<PlayerData>().ChangeAmountOfMaterial(StoredMaterial.Value, amountOfMaterials);
                ChangeAmountOfMaterialInStorage(-amountOfMaterials);

            }
        }
    }
}
