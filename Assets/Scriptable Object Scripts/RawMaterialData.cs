using System.Collections.Generic;
using UnityEngine;


[System.Serializable]
public class MaterialObjectEntry
{
    public PlayerData.RawMaterial rawMaterial;
    public GameObject droppedMaterialObject;
    public Sprite materialSprite;
}

[CreateAssetMenu(fileName = "MaterialObjectData", menuName = "ScriptableObjects/MaterialObjectData")]
public class RawMaterialData : ScriptableObject
{
    public List<MaterialObjectEntry> materialObjects = new();

    public MaterialObjectEntry GetMaterialObject(PlayerData.RawMaterial material)
    {
        int indexToFind = materialObjects.FindIndex(materialObject => materialObject.rawMaterial == material);
        if (indexToFind < 0)
        {
            Debug.LogWarning($"Item type {material} not found");
            return null;
        }
        else
            return materialObjects[indexToFind];
    }
}
