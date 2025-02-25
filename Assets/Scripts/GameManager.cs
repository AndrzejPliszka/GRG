using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class GameManager : NetworkBehaviour
{
    [SerializeField]
    ItemTypeData itemTypeData;

    override public void OnNetworkSpawn()
    {
        if (!IsServer) { return; }
        for (int i = 0; i < 5; i++) {
            GameObject item = Instantiate(itemTypeData.GetDataOfItemType(ItemData.ItemType.Sword).droppedItemPrefab, new Vector3(10, 5, 0), new Quaternion());
            item.GetComponent<NetworkObject>().Spawn();
            item.GetComponent<ItemData>().itemProperties.Value = new ItemData.ItemProperties { itemTier = ItemData.ItemTier.Wood, itemType = ItemData.ItemType.Sword };
        }

        for (int i = 0; i < 5; i++)
        {
            GameObject item = Instantiate(itemTypeData.GetDataOfItemType(ItemData.ItemType.Sword).droppedItemPrefab, new Vector3(10, 5, 2), new Quaternion());
            item.GetComponent<NetworkObject>().Spawn();
            item.GetComponent<ItemData>().itemProperties.Value = new ItemData.ItemProperties { itemTier = ItemData.ItemTier.Stone, itemType = ItemData.ItemType.Sword };
        }
        for (int i = 0; i < 5; i++)
        {
            GameObject item = Instantiate(itemTypeData.GetDataOfItemType(ItemData.ItemType.Axe).droppedItemPrefab, new Vector3(10, 5, 4), new Quaternion());
            item.GetComponent<NetworkObject>().Spawn();
            item.GetComponent<ItemData>().itemProperties.Value = new ItemData.ItemProperties { itemTier = ItemData.ItemTier.Wood, itemType = ItemData.ItemType.Axe };
        }
        for (int i = 0; i < 5; i++)
        {
            GameObject item = Instantiate(itemTypeData.GetDataOfItemType(ItemData.ItemType.Axe).droppedItemPrefab, new Vector3(10, 5, 6), new Quaternion());
            item.GetComponent<NetworkObject>().Spawn();
            item.GetComponent<ItemData>().itemProperties.Value = new ItemData.ItemProperties { itemTier = ItemData.ItemTier.Stone, itemType = ItemData.ItemType.Axe };
        }
        for (int i = 0; i < 5; i++)
        {
            GameObject item = Instantiate(itemTypeData.GetDataOfItemType(ItemData.ItemType.Medkit).droppedItemPrefab, new Vector3(10, 5, 8), new Quaternion());
            item.GetComponent<NetworkObject>().Spawn();
            item.GetComponent<ItemData>().itemProperties.Value = new ItemData.ItemProperties { itemTier = ItemData.ItemTier.Wood, itemType = ItemData.ItemType.Medkit };
        }
        for (int i = 0; i < 5; i++)
        {
            GameObject item = Instantiate(itemTypeData.GetDataOfItemType(ItemData.ItemType.Medkit).droppedItemPrefab, new Vector3(10, 5, 10), new Quaternion());
            item.GetComponent<NetworkObject>().Spawn();
            item.GetComponent<ItemData>().itemProperties.Value = new ItemData.ItemProperties { itemTier = ItemData.ItemTier.Stone, itemType = ItemData.ItemType.Medkit };
        }
        for (int i = 0; i < 5; i++)
        {
            GameObject item = Instantiate(itemTypeData.GetDataOfItemType(ItemData.ItemType.Food).droppedItemPrefab, new Vector3(10, 5, 12), new Quaternion());
            item.GetComponent<NetworkObject>().Spawn();
            item.GetComponent<ItemData>().itemProperties.Value = new ItemData.ItemProperties { itemTier = ItemData.ItemTier.Wood, itemType = ItemData.ItemType.Food };
        }
        for (int i = 0; i < 5; i++)
        {
            GameObject item = Instantiate(itemTypeData.GetDataOfItemType(ItemData.ItemType.Food).droppedItemPrefab, new Vector3(10, 5, 14), new Quaternion());
            item.GetComponent<NetworkObject>().Spawn();
            item.GetComponent<ItemData>().itemProperties.Value = new ItemData.ItemProperties { itemTier = ItemData.ItemTier.Stone, itemType = ItemData.ItemType.Food };
        }
        for (int i = 0; i < 5; i++)
        {
            GameObject item = Instantiate(itemTypeData.GetDataOfItemType(ItemData.ItemType.Hammer).droppedItemPrefab, new Vector3(10, 5, 16), new Quaternion());
            item.GetComponent<NetworkObject>().Spawn();
            item.GetComponent<ItemData>().itemProperties.Value = new ItemData.ItemProperties { itemTier = ItemData.ItemTier.Wood, itemType = ItemData.ItemType.Hammer };
        }
        for (int i = 0; i < 5; i++)
        {
            GameObject item = Instantiate(itemTypeData.GetDataOfItemType(ItemData.ItemType.Hammer).droppedItemPrefab, new Vector3(10, 5, 18), new Quaternion());
            item.GetComponent<NetworkObject>().Spawn();
            item.GetComponent<ItemData>().itemProperties.Value = new ItemData.ItemProperties { itemTier = ItemData.ItemTier.Stone, itemType = ItemData.ItemType.Hammer };
        }
    }
}
