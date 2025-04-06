using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
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
        public float _taxRate = 0.25f;
        public float TaxRate
        {
            get => _taxRate;
            set
            {
                if (value < 0)
                    value = 0;
                if (value > 1)
                    value = 1;

                if (_taxRate != value)
                {
                    _taxRate = value;
                    OnTaxRateChange.Invoke(_taxRate);
                }
            }
        }
        public event Action<int, int> OnFoodChange = delegate { };
        public event Action<float> OnTaxRateChange = delegate { };
        public List<GameObject> townMembers = new();
        public List<Shop> shopsControlledByLeader = new();
        public List<LandScript> landInTown = new();
    }

    public event Action<GameObject, PlayerData.PlayerRole> OnPlayerRoleChange = delegate { };
    public event Action<GameObject, int> OnPlayerTownChange = delegate { }; //this event is unused but it is kept cos it may be handy later


    public List<TownProperties> TownData { get; private set; } = new();
    public List<GameObject> PlayersWithoutTown { get; private set; } = new();

    //Making this script singleton
    public static GameManager Instance { get; private set; }

    [SerializeField] ItemTypeData itemTypeData; //used for spawning items
    [SerializeField] GameObject landObject;

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

        for (int i = 0; i < 1; i++)
        {
            TownData.Add(new TownProperties() { FoodSupply = 0, MaximumFoodSupply = 100 });
        }
    }

    override public void OnNetworkSpawn()
    {
        if (!IsServer) { return; }
        GenerateTownLand(0, 20, 13, -10);
        OnPlayerTownChange += ChangeLeader;

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

    void GenerateTownLand(int townId, int width, int length, int startingOffset)
    {
        if (!IsServer) { throw new Exception("This is generating fucntion calling this on server is impossible"); }

        Transform landContainer = GameObject.Find($"Town{townId}").transform.Find("LandContainer");
        Debug.Log(landContainer);
        float offsetBetweenTiles = 10f;
        for(int i = startingOffset; i < startingOffset + width; i++)
        {
            for(int j = 0; j < length; j++)
            {
                GameObject landTile = Instantiate(landObject, landContainer.position + landContainer.rotation * new Vector3(j * offsetBetweenTiles, 0, i * offsetBetweenTiles), new Quaternion());
                landTile.GetComponent<NetworkObject>().Spawn();
                LandScript landScript = landTile.GetComponent<LandScript>();
                TownData[townId].landInTown.Add(landScript);
                landScript.menuXPos = i;
                landScript.menuYPos = j;
                landScript.menuDisplayText = $"Land {i} {j}";
            }
        }
    }

    //Functions to manage data
    public void ChangeFoodSupply(int amountToChange, int townId) //TO DO: make that you can change it in every city (currently only in 1st one)
    {
        if (!IsServer) { throw new Exception("You can modify things in GameManager only on server!"); };
        TownProperties targetTownData = TownData[townId];
        targetTownData.FoodSupply += amountToChange;

        if (targetTownData.FoodSupply < 0)
            targetTownData.FoodSupply = 0;
        if (targetTownData.FoodSupply > targetTownData.MaximumFoodSupply)
            targetTownData.FoodSupply = targetTownData.MaximumFoodSupply;

        TownData[townId] = targetTownData;
    }

    public void ChangeLeader(GameObject player, int townId)
    {
        if (!IsServer) { throw new Exception("You can modify things in GameManager only on server!"); }
        if (TownData[townId].townMembers.Count == 0)
            return;
        ChangePlayerAffiliation(TownData[townId].townMembers[0], PlayerData.PlayerRole.Leader, townId);
    }

    //used to put player in PlayersWithoutTown list
    public void AddPlayerToRegistry(GameObject playerGameObject)
    {
        if (!IsServer) { throw new Exception("You can modify things in GameManager only on server!"); };
        PlayersWithoutTown.Add(playerGameObject);
        //This event is called so every time someone joins this method is called (used in RoleBasedColliders)
        OnPlayerRoleChange.Invoke(playerGameObject, PlayerData.PlayerRole.Peasant);
    }
    //remove player from all lists
    public void RemovePlayerFromRegistry(GameObject playerGameObject)
    {
        if (!IsServer) { throw new Exception("You can modify things in GameManager only on server!"); };
        PlayerData playerData = playerGameObject.GetComponent<PlayerData>();
        if (playerData.TownId.Value == -1) //this means that player should be transported in PlayersWithoutTown
        {
            PlayersWithoutTown.Remove(playerGameObject);
        }
        else {
            TownData[playerData.TownId.Value].townMembers.Remove(playerGameObject);
        }
    }
    void AddPlayerToTown(GameObject playerGameObject, int townId) 
    {
        if (!IsServer) { throw new Exception("You can modify things in GameManager only on server!"); };
        PlayerData playerData = playerGameObject.GetComponent<PlayerData>();
        if(townId == -1) //this means that player should be transported in PlayersWithoutTown
        {
            RemovePlayerFromTown(playerGameObject);
            return;
        }

        TownData[townId].townMembers.Add(playerGameObject);
        if(playerData.TownId.Value >= 0)
            TownData[playerData.TownId.Value].townMembers.Remove(playerGameObject);
        PlayersWithoutTown.Remove(playerGameObject);

        playerData.TownId.Value = townId;

    }

    void RemovePlayerFromTown(GameObject playerGameObject)
    {
        if (!IsServer) { throw new Exception("You can modify things in GameManager only on server!"); };

        PlayerData playerData = playerGameObject.GetComponent<PlayerData>();
        TownData[playerData.TownId.Value].townMembers.Remove(playerGameObject);
        PlayersWithoutTown.Add(playerGameObject);

        //if are so there are no redundant event calls 
        if (playerData.TownId.Value != -1)
        {
            playerData.TownId.Value = -1;
            OnPlayerTownChange.Invoke(playerGameObject, -1);
        }
        
        if (playerData.Role.Value != PlayerData.PlayerRole.Peasant)
        {
            playerData.Role.Value = PlayerData.PlayerRole.Peasant;
            OnPlayerRoleChange.Invoke(playerGameObject, PlayerData.PlayerRole.Peasant);
        }
            
    }

    public void ChangePlayerAffiliation(GameObject playerGameObject, PlayerData.PlayerRole role, int townId)
    {
        if (!IsServer) { throw new Exception("You can modify things in GameManager only on server!"); };
        PlayerData playerData = playerGameObject.GetComponent<PlayerData>();

        if (playerData.Role.Value == role && playerData.TownId.Value == townId)
            return;

        if (role == PlayerData.PlayerRole.Peasant) //if peasant then remove from any town
        {
            RemovePlayerFromTown(playerGameObject);
            return;
        }

        bool townChanged = false; //flags so events are always invoked after changes
        bool roleChanged = false;

        if (playerData.TownId.Value != townId)
        {
            AddPlayerToTown(playerGameObject, townId);
            playerData.TownId.Value = townId; // Upewnij siê, ¿e zmieniasz wartoœæ TownId, jeœli ma to sens
            townChanged = true;
        }

        if (playerData.Role.Value != role && townId >= 0)
        {
            playerData.Role.Value = role;
            roleChanged = true;
        }

        if (townChanged || roleChanged)
        {
            if (townChanged)
                OnPlayerTownChange.Invoke(playerGameObject, townId);

            if (roleChanged)
                OnPlayerRoleChange.Invoke(playerGameObject, role);
        }
    }
}
