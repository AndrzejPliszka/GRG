using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UIElements;

public class ItemData : NetworkBehaviour
{
    [SerializeField] ItemTierData itemTierData;
    public enum ItemType
    {
        Null, //this type is used to denote no item in inventory (I use this instead of list with varying size, because with this I can store items dynamically in different item slots)
        Sword,
        Axe,
        Medkit,
        Food
    }

    public enum ItemTier
    {
        Wood,
        Stone
    }

    //here I create networkVariable exclusive to every item
    public NetworkVariable<ItemProperties> itemProperties = new();

    //Static function that changes texture of given item to appropariate material (it is here, because I have no script to handle behaviour and putting it in player scripts would be odd, but it is *theoretically* changing data)
    public static void RetextureItem(GameObject item, ItemTier itemTier, ItemTierData itemMaterials)
    {
        foreach (Renderer itemPartRenderer in item.GetComponentsInChildren<Renderer>())
        {
            if (itemPartRenderer.transform.CompareTag("Material"))
            {
                itemPartRenderer.material = itemMaterials.GetDataOfItemTier(itemTier).itemMaterial;
            }
        }
    }
    //This is struct which stores ALL data related to item, which won't change on picking up/dropping items
    //To make NetworkList out of struct I need to serialize it this way and implement IEquatable
    [Serializable]
    public struct ItemProperties : INetworkSerializable, IEquatable<ItemProperties>
    {
        public ItemType itemType;
        public ItemTier itemTier;
        public readonly bool Equals(ItemProperties other) //this function is required for marking function IEquatable
        {
            return itemType == other.itemType && itemTier == other.itemTier;
        }
        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref itemType);
            serializer.SerializeValue(ref itemTier);
        }
    }
    private void Start()
    {
        RetextureItem(transform.gameObject, itemProperties.Value.itemTier, itemTierData);
    }

}
