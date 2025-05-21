using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

public class PlayerData : NetworkBehaviour
{
    public enum PlayerRole
    {
        Peasant,
        Citizen,
        Leader,
        Guard,
        Councilor
    }

    public NetworkVariable<FixedString32Bytes> Nickname { get;  private set; } = new();

    public NetworkList<ItemData.ItemProperties> Inventory { get; private set; }

    public NetworkVariable<int> SelectedInventorySlot { get; private set; } = new(0);

    public NetworkVariable<int> Hunger { get; private set; } = new(100);

    public NetworkVariable<int> Health { get; private set; } = new(100);

    public NetworkVariable<float> Money { get; private set; } = new(0);

    public NetworkVariable<PlayerRole> Role { get; private set; } = new(PlayerRole.Peasant);

    public NetworkVariable<int> TownId { get; private set; } = new(-1);

    public NetworkVariable<bool> IsCriminal { get; private set; } = new(false);

    public NetworkVariable<int> CriminalCooldown { get; private set; } = new(0); //relevant only when IsCriminal == true  (seconds until stops being criminal)

    public NetworkVariable<bool> IsInJail { get; private set; } = new(false);

    public NetworkVariable<int> JailCooldown { get; private set; } = new(0);

    public NetworkVariable<int> TownPlayerIsIn { get; private set; } = new(-1); //physical location, -1 means no town

    //Variables that hold things related to managing data above
    bool decreaseHungerFaster = false;
    bool decreaseHealth = false;

    //Event variables
    public event Action OnDeath;

    Movement playerMovement; //You can use this, but you need to check if it is null, because it is not needed to be attached to player

    public void Awake()
    {
        //we need to do this before connection (so before Start()/OnNetworkSpawn()), but not on declaration, because there will be memory leak
        Inventory = new NetworkList<ItemData.ItemProperties>();

        playerMovement = GetComponent<Movement>();
    }

    private void Start()
    {
        if (IsServer)
        {
            GameManager.Instance.AddPlayerToRegistry(gameObject);

            Health.OnValueChanged += InvokeDeath;
            //move to game manager or player manager??
            StartCoroutine(ReduceHunger());
            StartCoroutine(ChangeHealthOverTime());

            ChangeHealth(-50);

            for (int i = 0; i < 3; i++) //we want to have 3 inventory slots in the beginning
            {
                Inventory.Add(new ItemData.ItemProperties());
            }

            if (TryGetComponent<ObjectInteraction>(out var objectInteraction))
                objectInteraction.OnHittingSomething += CheckIfHitIsIllegal;

            StartCoroutine(CheckTownPlayerIsIn());
            TownPlayerIsIn.OnValueChanged += CheckIfPlayerIsIllegalInTown;
            IsCriminal.OnValueChanged += (oldValue, isCriminal) =>
            {
                if (isCriminal)
                    if (!IsInJail.Value)
                        StartCoroutine(IsCriminalCooldown());
                    else
                        IsCriminal.Value = false; //if player is in jail, he cannot be criminal
                else
                    CriminalCooldown.Value = 0;
            };
            IsInJail.OnValueChanged += (oldValue, isInJail) =>
            {
                if (isInJail)
                    StartCoroutine(IsInJailCooldown());
                else
                    JailCooldown.Value = 0;
            };
        }
        //Reset inventory on server
        if (!IsOwner) { return; }
        ChangeNicknameServerRpc(GameObject.Find("Canvas").GetComponent<Menu>().Nickname);
    }

    private void Update()
    {
        if (!IsServer) { return; }

        //If running decrease hunger faster
        if (playerMovement && playerMovement.IsRunning) //Movement may not be attached to gameObject so remember to check
            decreaseHungerFaster = true;
        else
            decreaseHungerFaster = false;

        if (Hunger.Value <= 0)
            decreaseHealth = true;
        else
            decreaseHealth = false;

    }
    //[WARNING !] This is unsafe, because it makes that nickname is de facto Owner controlled and can be changed any time by client by calling this method
    //It is that way, because otherwise nickname would need to be send to server on player join and server would need to assign it only one time and I don't really know how to do this and this will be assigned by client anyways, so I don't care
    [Rpc(SendTo.Server)]
    private void ChangeNicknameServerRpc(FixedString32Bytes setNickname)
    {
        Nickname.Value = setNickname;
    }

    //tries to add ItemData.itemProperties of GameObject to inventory and returns true if adding it to inventory succeded
    public bool AddItemToInventory(ItemData.ItemProperties itemData)
    {
        if (!IsServer) throw new Exception("Trying to add item to inventory as a client");
        int freeSlot = FindFreeInventorySlot();
        if (freeSlot != -1)
        {
            Inventory[freeSlot] = itemData;
            return true;
        }

        return false;
    }

    //If no inventory slots are available, return -1
    public int FindFreeInventorySlot()
    {
        //try selectedInventorySlot
        if (Inventory[SelectedInventorySlot.Value].itemType == ItemData.ItemType.Null)
            return SelectedInventorySlot.Value;

        //try other slots
        for (int i = 0; i < Inventory.Count; i++)
        {
            if (Inventory[i].itemType == ItemData.ItemType.Null)
                return i;
        }
        //all slots are taken, return -1
        return -1;
    }

    //Removes item in current inventory slot and returnes it (so it can be spawned as an gameObject)
    //[REFACTOR THIS FUNCTION TO HAVE PROPERTY FROM WHICH SLOT TO REMOVE ITEM!!!]
    public ItemData.ItemProperties RemoveItemFromInventory(int targetSlot)
    {
        if (!IsServer) throw new Exception("Trying to remove item from inventory as a client");

        ItemData.ItemProperties item = Inventory[targetSlot];
        Inventory[targetSlot] = new ItemData.ItemProperties { itemType = ItemData.ItemType.Null }; //deleting item from inventory
        return item; //returnng item so it can be spawned on scene as gameObject
        
    }

    //This method is needed because we cannot directly change selectedInventorySlots in other script
    [Rpc(SendTo.Server)]
    public void ChangeSelectedInventorySlotServerRpc(int targetSlot)
    {
        //Validation
        if(targetSlot >  Inventory.Count - 1)
            targetSlot = Inventory.Count - 1;
        else if(targetSlot < 0) targetSlot = 0;

        SelectedInventorySlot.Value = targetSlot;
    }

    //Because this code returns IEnuerator it is executed asynchronously, which makes sense, because hunger only decreases every couple seconds
    private IEnumerator ReduceHunger()
    {
        if (!IsServer) { throw new Exception("Trying to modify hunger on client!"); };
        int currentHungerTick = 0;
        int TicksPerSecond = 30;
        int TicksToDecreaseHunger = 300;
        while (Hunger.Value > 0)
        {
            currentHungerTick++;

            if (decreaseHungerFaster)
                currentHungerTick += 2;

            if (currentHungerTick >= TicksToDecreaseHunger) {
                Hunger.Value--;
                currentHungerTick = 0;
            }
            yield return new WaitForSeconds((float)1/TicksPerSecond); //amount of time program waits before continuing
        }
    }

    private IEnumerator ChangeHealthOverTime()
    {
        if (!IsServer) { throw new Exception("Trying to modify health on client!"); };
        int currentHealthTick = 0;
        int TicksPerSecond = 30;
        int TicksToDecreaseHealth = 100;
        while (true)
        {
            currentHealthTick++;
            if (decreaseHealth)
            {
                currentHealthTick += 9;
            }
            if (currentHealthTick >= TicksToDecreaseHealth)
            {
                if (decreaseHealth)
                    ChangeHealth(-1);
                else
                    ChangeHealth(1);
                currentHealthTick = 0;
            }
            yield return new WaitForSeconds((float)1 / TicksPerSecond); //amount of time program waits before continuing
        }
    }

    public void ChangeHunger(int amountToIncrease)
    {
        if (!IsServer) { throw new Exception("Trying to modify hunger on client!"); };
        Hunger.Value += amountToIncrease;
        if (Hunger.Value < 0)
            Hunger.Value = 0;
        else if (Hunger.Value > 100)
            Hunger.Value = 100;
    }
    //This function is setter for Health. If called on client it gives error, so it is perfectly safe!
    public void ChangeHealth(int amountToIncrease)
    {
        if (!IsServer) { throw new Exception("Trying to modify health on client!"); };
        Health.Value += amountToIncrease;
        if (Health.Value < 0)
            Health.Value = 0;
        else if (Health.Value > 100)
            Health.Value = 100;
    }
    //this function is setter for money. It returns false, if player would have negative balance after changing money
    public bool ChangeMoney(float amountToIncrease)
    {
        if (!IsServer) { throw new Exception("Trying to modify money amount on client!"); };
        if (Money.Value + amountToIncrease < 0)
            return false;

        //pay tax
        if (amountToIncrease > 0 && !(Role.Value == PlayerRole.Leader || Role.Value == PlayerRole.Peasant))
        { 
            float taxMoney = GameManager.Instance.TownData[TownId.Value].TaxRate * amountToIncrease;
            amountToIncrease -= taxMoney;
            GameManager.Instance.TownData[TownId.Value].townMembers[0].GetComponent<PlayerData>().ChangeMoney(taxMoney); //give tax money to leader
        }

        Money.Value += amountToIncrease;
        Money.Value = (Mathf.Round(Money.Value * 100)) / 100.0f; //ensure amountToIncrease has max 2 digits after colon
        return true;

    }
    private void InvokeDeath(int oldValue, int newValue)
    {
        if(newValue == 0)
            OnDeath.Invoke();
    }

    void CheckIfHitIsIllegal(GameObject hitObject)
    {
        if(IsInJail.Value) //if player is in jail, he cannot be criminal
            return;
        string hitTag = hitObject.tag;
        if (hitTag == "Shop")
        {
            CriminalCooldown.Value = 30;
            IsCriminal.Value = true;
        }
        else if (hitTag == "Player")
        {
            PlayerData playerData = hitObject.GetComponent<PlayerData>();
            if (!playerData.IsCriminal.Value)
            {
                CriminalCooldown.Value = 30;
                IsCriminal.Value = true;
            }
        }
    }

    void CheckIfPlayerIsIllegalInTown(int oldTownPlayerWasIn, int newTownPlayerIsIn)
    {
        if (IsInJail.Value) //if player is in jail, he cannot be criminal
            return;
        if ((newTownPlayerIsIn != -1 && Role.Value == PlayerRole.Peasant) || (newTownPlayerIsIn != -1 && newTownPlayerIsIn != TownId.Value))
        {
            CriminalCooldown.Value = 10;
            IsCriminal.Value = true;
        }
    }

    IEnumerator CheckTownPlayerIsIn()
    {
        while (true)
        {
            int i = 0;
            TownPlayerIsIn.Value = -1; //if is in town it gets imidiatelly changed into other number, may cause problems on listeners!
            foreach (GameManager.TownProperties town in GameManager.Instance.TownData)
            {
                Bounds bounds = town.townBase.GetComponent<Collider>().bounds;
                Vector3 min = bounds.min;
                Vector3 max = bounds.max;

                Vector3 playerPos = transform.position;
                if (playerPos.x >= min.x && playerPos.x <= max.x && playerPos.z >= min.z && playerPos.z <= max.z)
                {
                    TownPlayerIsIn.Value = i;
                    break;
                }
                i++;
            }
            yield return new WaitForSeconds(1f); //check if this changes every second
        }
    }

    IEnumerator IsCriminalCooldown()
    {
        while(CriminalCooldown.Value > 0)
        {
            if(IsInJail.Value) //dont change cooldown because it is not relevant, as the player is in jail
                break;

            CriminalCooldown.Value--;
            yield return new WaitForSeconds(1);
        }
        IsCriminal.Value = false;
    }

    IEnumerator IsInJailCooldown()
    {
        while (JailCooldown.Value > 0)
        {
            JailCooldown.Value--;
            yield return new WaitForSeconds(1);
        }
        //Maybe move to another function???
        if (playerMovement)
            playerMovement.TeleportPlayerToPosition(playerMovement.StartingPosition);
        IsInJail.Value = false;
    }
}
