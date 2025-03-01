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

    public override void OnNetworkSpawn()
    {
        ChangeLevelOfMaterial(new NetworkListEvent<GameManager.TownProperties>() { Index = townId, Value = GameManager.Instance.TownData[townId] });//update amount of stuff in storage on joining
        GameManager.Instance.TownData.OnListChanged += ChangeLevelOfMaterial;
    }
    public void ChangeLevelOfMaterial(NetworkListEvent<GameManager.TownProperties> townPropertiesChangeEvent)
    {
        if (townPropertiesChangeEvent.Index != townId)
            return;

        GameManager.TownProperties targetTownProperties = townPropertiesChangeEvent.Value;
        storedMaterial.transform.position = new Vector3(
            storedMaterial.transform.position.x, 
            (1.0f * targetTownProperties.foodSupply / targetTownProperties.maximumFoodSupply) * maximumMaterialLevel + yOffset, //multiplying times 1.0f to avoid integer devision and always getting 0
            storedMaterial.transform.position.z);
    }
}
