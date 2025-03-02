using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using static ItemData;

public class GameManager : NetworkBehaviour
{
    //Making this script singleton
    public static GameManager Instance { get; private set; }

    public NetworkList<TownProperties> TownData { get; private set; }

    [SerializeField] ItemTypeData itemTypeData; //used for spawning items

    private void Awake()
    {
        TownData = new(); //Initializing here and not on declaration, because memory leak

        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    override public void OnNetworkSpawn()
    {
        if (!IsServer) { return; }

        for (int i = 0; i < 1; i++)
        {
            TownData.Add(new TownProperties() { foodSupply = 0, maximumFoodSupply = 100 });
        }

        //Spawn all items in the game for testing purposes
        int itemSpawnZPos = 0;
        foreach (ItemType itemType in Enum.GetValues(typeof(ItemType)))
        {
            if (itemType == ItemType.Null)
                continue;

            foreach (ItemTier itemTier in Enum.GetValues(typeof(ItemTier)))
            {
                GameObject item = Instantiate(itemTypeData.GetDataOfItemType(itemType).droppedItemPrefab, new Vector3(10, 5, itemSpawnZPos), new Quaternion());
                item.GetComponent<NetworkObject>().Spawn();
                item.GetComponent<ItemData>().itemProperties.Value = new ItemProperties { itemTier = itemTier, itemType = itemType };
                itemSpawnZPos += 2;
            }
        }
    }
    //To make NetworkList out of struct I need to serialize it this way and implement IEquatable
    [Serializable]
    public struct TownProperties : INetworkSerializable, IEquatable<TownProperties>
    {
        public int foodSupply;
        public int maximumFoodSupply;
        public readonly bool Equals(TownProperties other) //this function is required for marking function IEquatable
        {
            return false;
        }
        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref foodSupply);
            serializer.SerializeValue(ref maximumFoodSupply);
        }
    }

    //Functions to manage data
    public void ChangeFoodSupply(int amountToChange) //TO DO: make that you can change it in every city (currently only in 1st one)
    {
        if (!IsServer) { throw new Exception("You can modify things in GameManager only on server!"); };
        TownProperties targetTownData = TownData[0];
        targetTownData.foodSupply += amountToChange;

        if(targetTownData.foodSupply < 0)
            targetTownData.foodSupply = 0;
        if(targetTownData.foodSupply > targetTownData.maximumFoodSupply)
            targetTownData.foodSupply = targetTownData.maximumFoodSupply;

        TownData[0] = targetTownData;
    }
}
