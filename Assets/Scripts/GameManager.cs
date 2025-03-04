using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using static ItemData;

public class GameManager : NetworkBehaviour
{
    //TownProperties and TownData are server only!
    public class TownProperties
    {
        //first value foodSupply, second - value maximumFoodSupply
        int _foodSupply; //needed for setter getter
        public int FoodSupply { //setter getter so event on change exists
            get => _foodSupply;
            set
            {
                if (_foodSupply != value)
                {
                    _foodSupply = value;
                    OnFoodChange.Invoke(_foodSupply, _maximumFoodSupply);
                }
            } 
        }

        public int _maximumFoodSupply;
        public int MaximumFoodSupply
        { //setter getter so I can inform when food amount changes
            get => _maximumFoodSupply;
            set
            {
                if (_maximumFoodSupply != value)
                {
                    _maximumFoodSupply = value;
                    OnFoodChange.Invoke(_foodSupply, _maximumFoodSupply);
                }
            }
        }

        public event Action<int, int> OnFoodChange = delegate { };
        //key is player role and value is all players belonging to that role
        public Dictionary<PlayerData.PlayerRole, List<GameObject>> townMembers;
    }
    public List<TownProperties> TownData { get; private set; }
    public List<GameObject> PlayersWithoutTown { get; private set; }

    //Making this script singleton
    public static GameManager Instance { get; private set; }

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
            TownData.Add(new TownProperties() { FoodSupply = 0, MaximumFoodSupply = 100 });
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
    //Functions to manage data
    public void ChangeFoodSupply(int amountToChange) //TO DO: make that you can change it in every city (currently only in 1st one)
    {
        if (!IsServer) { throw new Exception("You can modify things in GameManager only on server!"); };
        TownProperties targetTownData = TownData[0];
        targetTownData.FoodSupply += amountToChange;

        if(targetTownData.FoodSupply < 0)
            targetTownData.FoodSupply = 0;
        if(targetTownData.FoodSupply > targetTownData.MaximumFoodSupply)
            targetTownData.FoodSupply = targetTownData.MaximumFoodSupply;

        TownData[0] = targetTownData;
    }
}
