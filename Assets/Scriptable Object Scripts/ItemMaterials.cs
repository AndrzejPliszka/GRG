using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static ItemData;

[System.Serializable]
public class MaterialEntry
{
    public ItemTier itemTier;
    public Material itemMaterial;
}

[CreateAssetMenu(fileName = "ItemMaterials", menuName = "ScriptableObjects/ItemMaterials", order = 1)]

public class ItemMaterials : ScriptableObject
{
    public List<MaterialEntry> items = new();

    public MaterialEntry GetDataOfItemTier(ItemTier itemTier)
    {
        return items[items.FindIndex(item => item.itemTier == itemTier)];
    }
}
