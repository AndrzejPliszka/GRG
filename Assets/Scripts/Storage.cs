using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class Storage : NetworkBehaviour
{
    [SerializeField] int townId;

    [SerializeField] GameObject storedMaterial;
    [SerializeField] float maximumMaterialLevel = 100;
    [SerializeField] float yOffset = -0.25f;

    //variables for refrence for PlayerUI
    public int FoodSupply { get; private set; } = new();
    public int MaximumFoodSupply { get; private set; } = new();

    public override void OnNetworkSpawn()
    {
        if(!IsServer) return;
        ChangeLevelOfMaterialRpc(GameManager.Instance.TownData[townId].FoodSupply, GameManager.Instance.TownData[townId].MaximumFoodSupply);//update amount of stuff in storage on joining
        GameManager.Instance.TownData[townId].OnFoodChange += ChangeLevelOfMaterialRpc;
    }
    [Rpc(SendTo.Everyone)]
    public void ChangeLevelOfMaterialRpc(int foodSupply, int maxFoodSupply)
    {
        storedMaterial.transform.position = new Vector3(
            storedMaterial.transform.position.x, (1.0f * foodSupply / maxFoodSupply) * maximumMaterialLevel + yOffset, storedMaterial.transform.position.z); //multiplying times 1.0f to avoid integer devision and always getting 0
        FoodSupply = foodSupply;
        MaximumFoodSupply = maxFoodSupply;
    }
}
