using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Xml;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using static ItemData;

public class GameManager : NetworkBehaviour
{
    public enum Law
    {
        AllowViolence,
        AllowPeasants
    }
    //TownProperties and TownData are server only!
    public class TownProperties
    {
        public List<PlayerData.MaterialData> townMaterialSupply; //Keep in mind that townMaterialSupply is only to keep track of materials across different storages, these materials should not be used to make building etc., they also may not be reliable
        //To display this data in player leader create NetworkList<MaterialData>, make event fire on every material and leader change and this event should be able to change this NetworkList on server so it is updated on client
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
        public event Action<float> OnTaxRateChange = delegate { };
        public Action<Transform> OnPlayerArrest = delegate { }; //Used for teleporting player to jail (transform is player transform)
        public Action<int, int, LandScript.Building, FixedString128Bytes> OnLandChange = delegate { }; //Used for displaying land in leader menu
        public Action<int, int, LandScript.Building, FixedString128Bytes> OnLawChange = delegate { }; //Used for displaying laws
        //Used for managing laws
        public Action<Law> OnLawAddedToQueue = delegate { };
        public Action<ulong, bool> OnPlayerVote = delegate { }; //ulong - playerId, bool vote (true for yes, false for no)
        public Action<int, bool, Law> OnVotingStateChange = delegate { }; //int - cooldown time for voting, bool - true for start voting, false for end voting, Law - if bool is true, this is law being voted on, if false this is law that passed
        public Action<int, int> OnVoteCountChange = delegate { }; //int1 - votes for no, int2 - votes for yes


        public List<GameObject> townMembers = new();
        public List<Shop> shopsControlledByLeader = new();
        public List<LandScript> landInTown = new();
        public Dictionary<ItemProperties, float> itemPrices = new();
        public Transform townBase; //for now used to check if player is physically in town

        public Dictionary<Law, bool> townLaw = new()
        {
            { Law.AllowViolence, false },
            { Law.AllowPeasants, false }
        };
    }



    public event Action<GameObject, PlayerData.PlayerRole> OnPlayerRoleChange = delegate { };
    public event Action<GameObject, int, int> OnPlayerTownChange = delegate { };

    [SerializeField] GameObject playerPrefab; //Used here, as without custom spawning, player would spawn in menu scene, which is not desired

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
            List<PlayerData.MaterialData> townMaterials = new();
            foreach (PlayerData.RawMaterial material in Enum.GetValues(typeof(PlayerData.RawMaterial)))
            {
                townMaterials.Add(new PlayerData.MaterialData { materialType = material, amount = 0, maxAmount = 20 });
            }
            TownData.Add(new TownProperties() { townMaterialSupply = townMaterials, townBase = GameObject.Find("Town" + i).transform.Find("Pavement") }); //Pavement cos it has approperiate size (at least for now)
        }
    }

    override public void OnNetworkSpawn()
    {
        if (!IsServer) { return; }
        NetworkManager.Singleton.OnClientConnectedCallback += SpawnNewPlayer;
        if(IsHost)
            SpawnNewPlayer(NetworkManager.Singleton.LocalClientId);

        GenerateTownLand(0, 10, 5, -5);
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
        float offsetBetweenTiles = 10f;
        for(int i = startingOffset; i < startingOffset + width; i++)
        {
            for(int j = 0; j < length; j++)
            {
                GameObject landTile = Instantiate(landObject, landContainer.position + landContainer.rotation * new Vector3(j * offsetBetweenTiles, 0, i * offsetBetweenTiles), new Quaternion());
                landTile.GetComponent<NetworkObject>().Spawn();
                LandScript landScript = landTile.GetComponent<LandScript>();
                TownData[townId].landInTown.Add(landScript);
                landScript.menuXPos.Value = i;
                landScript.menuYPos.Value = j;
                landScript.menuDisplayText.Value = $"Empty Land";
            }
        }
    }

    //Functions to manage data


    //Keep in mind that townMaterialSupply is only to keep track of materials across different storages, these materials should not be used to make building etc.
    public void ChangeMaterialAmount(int townId, PlayerData.RawMaterial material, int amountToChange) //TO DO: make that you can change it in every city (currently only in 1st one)
    {
        if (!IsServer) { throw new Exception("You can modify things in GameManager only on server!"); };
        PlayerData.MaterialData materialData = TownData[townId].townMaterialSupply[(int)material];
        materialData.amount += amountToChange;
        TownData[townId].townMaterialSupply[(int)material] = materialData;
    }

    public void ChangeMaxMaterialAmount(int townId, PlayerData.RawMaterial material, int amountToChange) //TO DO: make that you can change it in every city (currently only in 1st one)
    {
        if (!IsServer) { throw new Exception("You can modify things in GameManager only on server!"); };
        PlayerData.MaterialData materialData = TownData[townId].townMaterialSupply[(int)material];
        materialData.maxAmount += amountToChange;
        TownData[townId].townMaterialSupply[(int)material] = materialData;
    }


    //TO DO: FIX THIS FUNCTION (there are two ids, and in both I check if leader is good)
    public void ChangeLeader(GameObject player, int oldTownId, int newTownId = 0)
    {
        if (!IsServer) { throw new Exception("You can modify things in GameManager only on server!"); }
        if (oldTownId >= 0 && TownData[oldTownId].townMembers.Count != 0)
            ChangePlayerAffiliation(TownData[oldTownId].townMembers[0], PlayerData.PlayerRole.Leader, oldTownId);
        if (newTownId >= 0 && TownData[newTownId].townMembers.Count != 0)
            ChangePlayerAffiliation(TownData[newTownId].townMembers[0], PlayerData.PlayerRole.Leader, newTownId);
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

            OnPlayerTownChange.Invoke(playerGameObject, playerData.TownId.Value, -1);
            playerData.TownId.Value = -1;
        }
        
        if (playerData.Role.Value != PlayerData.PlayerRole.Peasant)
        {
            playerData.Role.Value = PlayerData.PlayerRole.Peasant;
            OnPlayerRoleChange.Invoke(playerGameObject, PlayerData.PlayerRole.Peasant);
        }
            
    }
    //townId is town that player is going to
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
        int originalTownId = playerData.TownId.Value;

        if (playerData.TownId.Value != townId)
        {
            AddPlayerToTown(playerGameObject, townId);
            playerData.TownId.Value = townId;
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
                OnPlayerTownChange.Invoke(playerGameObject,originalTownId ,townId);

            if (roleChanged)
                OnPlayerRoleChange.Invoke(playerGameObject, role);
        }
    }

    public void SpawnNewPlayer(ulong playerId)
    {
        GameObject player = Instantiate(playerPrefab);
        player.GetComponent<NetworkObject>().SpawnAsPlayerObject(playerId);
    }

    public override void OnNetworkDespawn()
    {
        NetworkManager.Singleton.OnClientConnectedCallback -= SpawnNewPlayer;
    }
}
