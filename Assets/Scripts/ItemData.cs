using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UIElements;

public class ItemData : NetworkBehaviour
{
    public enum ItemType
    {
        Null, //this type is used to denote no item in inventory (I use this instead of list with varying size, because with this I can store items dynamically in different item slots)
        Sample
    }

    public enum ItemRarity
    {
        Wood
    }
    //This is struct which stores ALL data related to item, which won't change on picking up/dropping items
    //To make NetworkList out of struct I need to serialize it this way and implement IEquatable
    [Serializable]
    public struct ItemProperties : INetworkSerializable, IEquatable<ItemProperties>
    {
        public ItemType itemType;
        public ItemRarity itemRarity;
        public readonly bool Equals(ItemProperties other) //this function is required for marking function IEquatable
        {
            return itemType == other.itemType && itemRarity == other.itemRarity;
        }
        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref itemType);
            serializer.SerializeValue(ref itemRarity);
        }
    }

    //here I create networkVariable exclusive to every item
    public NetworkVariable<ItemProperties> itemProperties = new();

    private void Start()
    {
        if(!IsServer) { return; }
        //Assign sample data (this will be normally assigned by item spawning script)
        itemProperties.Value = new ItemProperties {
            itemType = ItemType.Sample,
            itemRarity = ItemRarity.Wood,
        };
    }

}
