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
        Food,
        Hammer,
        Sickle,
        FishingRod,
        Pickaxe
    }

    public enum ItemTier
    {
        Wood,
        Stone
    }

    //here I create networkVariable exclusive to every item
    public NetworkVariable<ItemProperties> itemProperties = new();

    //This is struct which stores ALL data related to item, which won't change on picking up/dropping items
    //To make NetworkList out of struct I need to serialize it this way and implement IEquatable
    [Serializable]
    public struct ItemProperties : INetworkSerializable, IEquatable<ItemProperties>
    {
        public ItemType itemType;
        public ItemTier itemTier;
        public int durablity; //if item doesn't have durability, then assign -1
        public ItemProperties(ItemType initialItemType, ItemTier initialItemTier, int initialDurablity)
        {
            itemType = initialItemType;
            itemTier = initialItemTier;
            durablity = initialDurablity;
        }

        //return true if item should be destroyed
        public bool ChangeDurability(int changeAmount)
        {
            if (durablity < 0)
            {
                Debug.LogWarning("You are changing durability of object, which has no durability (which means it has negative durability)");
                return false;
            }
            durablity += changeAmount;
            if (durablity < 0)
                return true;

            return false;
        }

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

    public void ReduceDurability(int amountToReduce)
    {
        if(!IsServer) { throw new Exception("Only server can change durability of tools"); }

        ItemProperties newItemProperties = itemProperties.Value;
        newItemProperties.durablity -= amountToReduce;
        itemProperties.Value = newItemProperties;
        if (newItemProperties.durablity < 0) {
            Destroy(gameObject);
        }
    }

    //Static function that changes texture of given item to appropariate material (it is here, because I have no script to handle behaviour and putting it in player scripts would be odd, but it is *technically* changing data)
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
    public override void OnNetworkSpawn()
    {
        if (itemProperties.Value.itemType != ItemType.Null)
            RetextureItem(transform.gameObject, itemProperties.Value.itemTier, itemTierData);
        else
            itemProperties.OnValueChanged += (oldItemProperties, itemProperties) => RetextureItem(transform.gameObject, itemProperties.itemTier, itemTierData);
    }

}
