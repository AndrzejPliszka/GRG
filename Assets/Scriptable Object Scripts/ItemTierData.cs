using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static ItemData;

[System.Serializable]
public class MaterialEntry
{
    public ItemTier itemTier;
    public Material itemMaterial;
    public Color UIColor;
}

[CreateAssetMenu(fileName = "ItemTierData", menuName = "ScriptableObjects/ItemTierData", order = 1)]

public class ItemTierData : ScriptableObject
{
    public List<MaterialEntry> items = new();

    public MaterialEntry GetDataOfItemTier(ItemTier itemTier)
    {
        return items[items.FindIndex(item => item.itemTier == itemTier)];
    }
}