using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
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

        int _maximumFoodSupply;
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
        public Dictionary<PlayerData.PlayerRole, List<GameObject>> townMembers = new();

        public int PlayerCount()
        {
            return townMembers.Values.Sum(list => list.Count);
        }
    }

    public event Action<GameObject, PlayerData.PlayerRole> OnPlayerRoleChange = delegate { };
    public event Action<GameObject, int> OnPlayerTownChange = delegate { };


    public List<TownProperties> TownData { get; private set; } = new();
    public List<GameObject> PlayersWithoutTown { get; private set; } = new();

    //Making this script singleton
    public static GameManager Instance { get; private set; }

    [SerializeField] ItemTypeData itemTypeData; //used for spawning items

    private void Awake()
    {
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

    //used to put player in PlayersWithoutTown registry
    public void AddPlayerToRegistry(GameObject playerGameObject)
    {
        if (!IsServer) { throw new Exception("You can modify things in GameManager only on server!"); };
        PlayersWithoutTown.Add(playerGameObject);
        //This event is called so every time someone joins this method is called (used in RoleBasedColliders)
        OnPlayerRoleChange.Invoke(playerGameObject, PlayerData.PlayerRole.Peasant);
    }

    public void AddPlayerToTown(GameObject playerGameObject, int townId) //TODO: Make this function delete player from peasant registry and other towns
    {
        if (!IsServer) { throw new Exception("You can modify things in GameManager only on server!"); };

        if(TownData[townId].townMembers.ContainsKey(PlayerData.PlayerRole.Citizen))
            TownData[townId].townMembers[PlayerData.PlayerRole.Citizen].Add(playerGameObject);
        else
            TownData[townId].townMembers.Add(PlayerData.PlayerRole.Citizen, new List<GameObject> { playerGameObject });

        PlayersWithoutTown.Remove(playerGameObject);


        OnPlayerTownChange.Invoke(playerGameObject, townId);
        OnPlayerRoleChange.Invoke(playerGameObject, PlayerData.PlayerRole.Citizen);
    }

    public void RemovePlayerFromTown(GameObject playerGameObject) //TODO: Make this function delete player from peasant registry and other towns
    {
        if (!IsServer) { throw new Exception("You can modify things in GameManager only on server!"); };

        PlayerData playerData = playerGameObject.GetComponent<PlayerData>();
        TownData[playerData.TownId.Value].townMembers[PlayerData.PlayerRole.Citizen].Remove(playerGameObject);
        PlayersWithoutTown.Add(playerGameObject);

        OnPlayerTownChange.Invoke(playerGameObject, -1);
        OnPlayerRoleChange.Invoke(playerGameObject, PlayerData.PlayerRole.Peasant);
    }

    public void ChangePlayerRole(GameObject playerGameObject, PlayerData.PlayerRole role, int townId = -1)
    {
        if (!IsServer) { throw new Exception("You can modify things in GameManager only on server!"); };

        PlayerData playerData = playerGameObject.GetComponent<PlayerData>();

        if (playerData.Role.Value == role)
            return;

        if (playerData.Role.Value == PlayerData.PlayerRole.Peasant && townId < 0) {
            if (playerGameObject.GetComponent<PlayerUI>())
                playerGameObject.GetComponent<PlayerUI>().DisplayErrorOwnerRpc("You need to become citizen of town first!");
        }

        if (role == PlayerData.PlayerRole.Peasant)
        {
            RemovePlayerFromTown(playerGameObject);
            return;
        }

        if (playerData.TownId.Value < 0)
            AddPlayerToTown(playerGameObject, townId);
        else
            TownData[playerData.TownId.Value].townMembers[playerData.Role.Value].Remove(playerGameObject);


        if(TownData[playerData.TownId.Value].townMembers.ContainsKey(role))
            TownData[playerData.TownId.Value].townMembers[role].Add(playerGameObject);
        else
            TownData[playerData.TownId.Value].townMembers.Add(role, new List<GameObject> { playerGameObject });

        OnPlayerRoleChange.Invoke(playerGameObject, role);
    }
}
