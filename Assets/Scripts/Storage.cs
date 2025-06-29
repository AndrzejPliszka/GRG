using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using static UnityEngine.Rendering.DebugUI;

public class Storage : NetworkBehaviour
{
    [SerializeField] int townId;


    [SerializeField] GameObject storedMaterialObject;
    [SerializeField] float maximumMaterialLevel = 100;
    [SerializeField] float yOffset = -0.25f;

    //variables for refrence for other scripts

    [field: SerializeField] public NetworkVariable<PlayerData.RawMaterial> StoredMaterial { get; private set; }
    public NetworkVariable<int> CurrentSupply { get; private set; } = new();
    [field: SerializeField] public NetworkVariable<int> MaxSupply { get; private set; } = new();

    public override void OnNetworkSpawn()
    {
        ChangeLevelOfMaterial(CurrentSupply.Value, MaxSupply.Value);
        CurrentSupply.OnValueChanged += (int oldValue, int value) =>
        {
            ChangeLevelOfMaterial(value, MaxSupply.Value);
            if(townId >= 0)
                GameManager.Instance.ChangeMaterialAmount(townId, StoredMaterial.Value, value);
        };
        MaxSupply.OnValueChanged += (int oldValue, int value) =>
        {
            ChangeLevelOfMaterial(CurrentSupply.Value, value);
            if (townId >= 0)
                GameManager.Instance.ChangeMaxMaterialAmount(townId, StoredMaterial.Value, value);
        };
    }

    public void ChangeLevelOfMaterial(int currentSupply, int maxSupply)
    {
        storedMaterialObject.transform.position = new Vector3(
            storedMaterialObject.transform.position.x, (1.0f * currentSupply / maxSupply) * maximumMaterialLevel + yOffset, storedMaterialObject.transform.position.z); //multiplying times 1.0f to avoid integer devision and always getting 0
    }

    public int ChangeMaterialInStorage(int amountToIncrease)
    {
        if (!IsServer) { throw new System.Exception("Only server can add material!"); }
        int newAmount = CurrentSupply.Value + amountToIncrease;
        if (newAmount > MaxSupply.Value)
        {
            int excessiveAmount = newAmount - MaxSupply.Value;
            CurrentSupply = MaxSupply;
            return excessiveAmount; //returning amount that was not added to material, because excessive material can be dropped, or dealt with other way etc.
        }
        else if (newAmount < 0)
        {
            //This means that we wanted to deduct material, but there was not enough of it, so we do nothing
            return newAmount; //returning amount that was not removed from material, so it can be used in other way (for example added to inventory)
        }
        else
        {
            CurrentSupply.Value = newAmount;
        }
        return 0;
    }
}
