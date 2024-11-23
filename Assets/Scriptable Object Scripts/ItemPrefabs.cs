using System;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using static ItemData;

[System.Serializable]
public class ItemEntry
{
    public ItemType itemType;
    public GameObject droppedItemPrefab;
    public GameObject holdedItemPrefab;
}

[CreateAssetMenu(fileName = "ItemPrefabs", menuName = "ScriptableObjects/ItemPrefabs", order = 1)]
public class ItemPrefabs : ScriptableObject
{
    public List<ItemEntry> items = new();

    public ItemEntry GetDataOfItemType(ItemType itemType)
    {
        return items[items.FindIndex(item => item.itemType == itemType)];
    }
}