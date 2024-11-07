using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UIElements;

public class ItemData : NetworkBehaviour
{
    public enum ItemType
    {
        Sample
    }

    public enum ItemRarity
    {
        Wood
    }
    //This is struct which stores ALL data related to item, which won't change on picking up/dropping items
    //To make NetworkVariable out of struct I need to serialize it this way
    public struct ItemProperties : INetworkSerializable
    {
        public ItemType itemType;
        public ItemRarity itemRarity;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref itemType);
            serializer.SerializeValue(ref itemRarity);
        }
    }

    //here I create networkVariable exclusive to every item
    public NetworkVariable<ItemProperties> itemProperties = new();

    private void Awake()
    {
        if(!IsServer) { return; }
        //Assign sample data (this will be normally assigned by item spawning script)
        itemProperties.Value = new ItemProperties {
            itemType = ItemType.Sample,
            itemRarity = ItemRarity.Wood,
        };
    }

}
