using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class Workshop : NetworkBehaviour
{
    readonly NetworkVariable<ItemData.ItemType> _itemType = new();
    public ItemData.ItemType ItemType
    {
        get { return _itemType.Value; }
        set {
            if (!IsServer)
                throw new Exception("Client cannot change ItemTier sold in workshop!");
            _itemType.Value = value;
            UpdateWorkshopSpriteClientRpc(ItemType, ItemTier);
            UpdateItemCostAndStorage();
        }
    }
    readonly NetworkVariable<ItemData.ItemTier> _itemTier = new();
    public ItemData.ItemTier ItemTier
    {
        get { return _itemTier.Value; }
        set
        {
            if (!IsServer)
                throw new Exception("Client cannot change ItemTier sold in workshop!");
            _itemTier.Value = value;
            if (ItemType != ItemData.ItemType.Null)
            {
                UpdateWorkshopSpriteClientRpc(ItemType, ItemTier);
                UpdateItemCostAndStorage();
            }
        }
    }

    public NetworkVariable<ulong> OwnerId = new(0); //Netcode player id, not object id

    [SerializeField] SpriteRenderer itemTypeSprite;
    [SerializeField] SpriteRenderer itemTierSprite;
    Vector2 spriteSize = new(0.8f, 0.8f);

    [SerializeField] ItemTypeData itemTypeData;
    [SerializeField] ItemTierData itemTierData;
    [SerializeField] bool activated; //used to determine whether this script is used only to store data (such as during unbuilt state) or to manage working workshop

    [HideInInspector] public NetworkList<PlayerData.MaterialData> ItemMaterialCost { get; private set; } = new();

    public override void OnNetworkSpawn()
    {
        UpdateWorkshopSpriteClientRpc(ItemType, ItemTier);

        base.OnNetworkSpawn();
    }

    public void UpgradeWorkshop()
    {
        if (!IsServer) { throw new System.Exception("Only server can upgrade workshop!"); }

        var values = Enum.GetValues(typeof(ItemData.ItemTier));
        int index = Array.IndexOf(values, ItemTier);
        if (index < values.Length - 1)
            ItemTier = (ItemData.ItemTier)values.GetValue(index + 1);
    }

    /// <summary>
    /// Updates ItemMaterialCost of workshop and StoredMaterialData in corresponding storage according to itemTypeData and itemTierData
    /// </summary>
    /// <exception cref="System.Exception">Function was run on client.</exception>
    public void UpdateItemCostAndStorage()
    {
        if (!activated) { return; }
        if (!IsServer) { throw new System.Exception("Only server can update item cost or modify storage data!"); }
        ItemEntry itemTypeInfo = itemTypeData.GetDataOfItemType(ItemType);
        MaterialEntry itemTierInfo = itemTierData.GetDataOfItemTier(ItemTier);

        ItemMaterialCost.Clear();

        Storage storage = GetComponent<Storage>();
        List<PlayerData.ExtendedMaterialData> storageMaterialData = new();
        foreach (PlayerData.ExtendedMaterialData material in storage.StoredMaterialData)
            storageMaterialData.Add(material);

        storage.StoredMaterialData.Clear();

        foreach (PlayerData.MaterialData material in itemTypeInfo.basicItemCost)
        {
            ItemMaterialCost.Add(new PlayerData.MaterialData()
            {
                MaterialType = material.MaterialType,
                Amount = Mathf.RoundToInt(material.Amount * itemTierInfo.multiplier)
            });

            int amountStored = 0;
            foreach (PlayerData.ExtendedMaterialData storageMaterial in storageMaterialData)
                if (storageMaterial.MaterialType == material.MaterialType)
                    amountStored = storageMaterial.Amount;

            if (storage)
                storage.StoredMaterialData.Add(new PlayerData.ExtendedMaterialData()
                {
                    MaterialType = material.MaterialType,
                    Amount = amountStored,
                    //For now I want storage to hold materials needed for creation of 3 items + bonus if workshop is better
                    MaxAmount = Mathf.RoundToInt(material.Amount * itemTierInfo.multiplier * 3 * itemTierInfo.multiplier)
                });
        }
    }
    /// <summary>
    /// Update sprite that is on workshop to match given itemType and itemTier. Sended to all clients, so they have synced state.
    /// </summary>
    /// <param name="itemType">ItemType of item that should be displayed on sprite</param>
    /// <param name="itemTier">ItemTier of item that should be displayed on sprite</param>
    [Rpc(SendTo.Everyone)]
    public void UpdateWorkshopSpriteClientRpc(ItemData.ItemType itemType, ItemData.ItemTier itemTier)
    {
        if (itemTypeSprite == null || itemTierSprite == null || itemType == ItemData.ItemType.Null)
            return;

        MaterialEntry itemTierInfo = itemTierData.GetDataOfItemTier(itemTier);
        ItemEntry itemTypeInfo = itemTypeData.GetDataOfItemType(itemType);
        itemTierSprite.sprite = itemTypeInfo.coloredItemSprite;
        itemTierSprite.color = itemTierInfo.UIColor;
        itemTypeSprite.sprite = itemTypeInfo.staticItemSprite;

        //Modify size, so it works for every resolution (we know that itemTypeSprite has same starting size as itemTierSprite)
        Vector2 currentSize = itemTypeSprite.sprite.bounds.size;
        float sizeX = spriteSize.x / currentSize.x;
        float sizeY = spriteSize.y / currentSize.y;
        itemTypeSprite.transform.localScale = new Vector3(sizeX, sizeY, 1f);
        itemTierSprite.transform.localScale = new Vector3(sizeX, sizeY, 1f);
    }

    /// <summary>
    /// Check if there are enough materials in storage script attached to workshop to create an item. 
    /// </summary>
    /// <returns>true if item can be created; false if there is not enough material in storage.</returns>
    /// <exception cref="System.Exception">Workshop has no Storage script even though it requires materials to create item.</exception>
    public bool CanCreateItem()
    {
        //Check if there are materials in storage to create item
        bool canCreateItem = true;
        if (TryGetComponent<Storage>(out Storage storage) || ItemMaterialCost.Count == 0)
        {
            foreach (PlayerData.MaterialData neededMaterial in ItemMaterialCost)
                if (neededMaterial.Amount > storage.GetMaterialDataOfRawMaterial(neededMaterial.MaterialType).Amount)
                    canCreateItem = false;
        }
        else
            throw new System.Exception("There is no storage script attached to Workshop, even though items cost materials to be made");

        return canCreateItem;
    }

    /// <summary>
    /// Creates item specific to workshop (itemType, itemTier) on it, with cost of materials from storage as specified by ItemMaterialCost;
    /// If there is no enough materials in storage, it does nothing.
    /// </summary>
    /// <param name="rpcParams">Don't override! Used to get player that called this function.</param>
    [Rpc(SendTo.Server)]
    public void CreateItemServerRpc(RpcParams rpcParams = default)
    {
        ulong playerId = rpcParams.Receive.SenderClientId;
        NetworkManager.Singleton.ConnectedClients.TryGetValue(playerId, out var player);

        bool areMaterialsAvailable = CanCreateItem();

        if (!areMaterialsAvailable) {
            if (player?.PlayerObject.TryGetComponent<PlayerUI>(out PlayerUI playerUI) ?? false)
                playerUI.DisplayErrorOwnerRpc("There are no materials in storage to create an item!");
            return;
        }

        //Subtract items
        if (TryGetComponent<Storage>(out Storage storage) || ItemMaterialCost.Count == 0)
        {
            foreach (PlayerData.MaterialData neededMaterial in ItemMaterialCost)
                storage.ChangeAmountOfMaterialInStorage(neededMaterial.MaterialType, -neededMaterial.Amount);
                
        }

        //Spawn item
        ItemData.ItemProperties itemProperties = new()
        {
            itemType = ItemType,
            itemTier = ItemTier,
            durablity = itemTierData.GetDataOfItemTier(ItemTier).maximumDurability
        };

        GameObject itemPrefab = itemTypeData.GetDataOfItemType(itemProperties.itemType).droppedItemPrefab;
        GameObject newItem = Instantiate(itemPrefab, transform.position + new Vector3(0, 1.5f), transform.rotation);
        newItem.GetComponent<NetworkObject>().Spawn();
        newItem.GetComponent<ItemData>().itemProperties.Value = itemProperties;
    }
}
