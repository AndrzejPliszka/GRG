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
    [SerializeField] GameObject unbuiltWorkshop; //Used when upgrading workshop

    [SerializeField] SpriteRenderer itemTypeSprite;
    [SerializeField] SpriteRenderer itemTierSprite;
    Vector2 spriteSize = new(0.8f, 0.8f);

    ItemTypeData itemTypeData;
    ItemTierData itemTierData;
    [SerializeField] bool activated; //used to determine whether this script is used only to store data (such as during unbuilt state) or to manage working workshop

    [HideInInspector] public NetworkList<PlayerData.MaterialData> ItemMaterialCost { get; private set; } = new();

    public override void OnNetworkSpawn()
    {
        itemTypeData = GameManager.Instance.ItemTypeData;
        itemTierData = GameManager.Instance.ItemTierData;
        UpdateWorkshopSpriteClientRpc(ItemType, ItemTier);

        base.OnNetworkSpawn();
    }

    //TO DO: CHECK IF PERSON WHO UPGRADES IS OWNER!!!!
    /// <summary>
    /// Destroy this object and spawn unbuilt workshop that has tier one larger than current one (when converting enum to int). If there is no such tier this function does nothing.
    /// </summary>
    [Rpc(SendTo.Server)]
    public void UpgradeWorkshopServerRpc()
    {
        ItemData.ItemTier newItemTier = ItemTier;
        var values = Enum.GetValues(typeof(ItemData.ItemTier));
        int tierNumber = Array.IndexOf(values, ItemTier);
        if (tierNumber < values.Length - 1)
            newItemTier = (ItemData.ItemTier)values.GetValue(tierNumber + 1);

        if (newItemTier == ItemTier)
            return;

        Storage oldStorage = GetComponent<Storage>();

        GameObject newWorkshop = Instantiate(unbuiltWorkshop, transform.position, transform.rotation);
        newWorkshop.GetComponent<NetworkObject>().Spawn();

        Workshop workshop = newWorkshop.GetComponent<Workshop>();
        workshop.ItemType = ItemType;
        workshop.ItemTier = newItemTier;
        workshop.OwnerId.Value = OwnerId.Value;

        UnbuiltBuilding building = newWorkshop.GetComponent<UnbuiltBuilding>();
        building.OwnerId.Value = OwnerId.Value;
        building.ObjectStringDescription.Value = "Workshop";
        //For now cost of building upgrade will be cost of item produced by it but this is temp
        List<PlayerData.MaterialData> neededMaterials = GetNeededMaterialsForAnItem(workshop.ItemType, workshop.ItemTier);

        building.NeededMaterials.Clear();
        building.MaterialPrices.Clear();
        foreach (PlayerData.MaterialData material in neededMaterials)
        {
            building.NeededMaterials.Add(new PlayerData.ExtendedMaterialData()
            {
                MaterialType = material.MaterialType,
                MaxAmount = material.Amount,
                Amount = Math.Clamp(oldStorage.GetMaterialDataOfRawMaterial(material.MaterialType).Amount, 0, material.Amount)
            });

            //Reset material prices on upgrade, temp measure until MaterialPrices is not merged with neededMaterials
            building.MaterialPrices.Add(0);
        }

        building.TryBuildBuilding();

        Destroy(gameObject);
    }

    public List<PlayerData.MaterialData> GetNeededMaterialsForAnItem(ItemData.ItemType itemType, ItemData.ItemTier itemTier)
    {
        List<PlayerData.MaterialData> neededMaterials = new();
        ItemEntry itemTypeInfo = itemTypeData.GetDataOfItemType(itemType);
        MaterialEntry itemTierInfo = itemTierData.GetDataOfItemTier(itemTier);
        foreach (PlayerData.MaterialData material in itemTypeInfo.basicItemCost)
        {
            neededMaterials.Add(new PlayerData.MaterialData()
            {
                MaterialType = material.MaterialType,
                Amount = Mathf.RoundToInt(material.Amount * itemTierInfo.multiplier)
            });
        }
        return neededMaterials;
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
        List<PlayerData.MaterialData> materialCost = GetNeededMaterialsForAnItem(ItemType, ItemTier);
        foreach(PlayerData.MaterialData material in materialCost)
            ItemMaterialCost.Add(material);

        Storage storage = GetComponent<Storage>();
        List<PlayerData.ExtendedMaterialData> storageMaterialData = new();
        foreach (PlayerData.ExtendedMaterialData material in storage.StoredMaterialData)
            storageMaterialData.Add(material);

        storage.StoredMaterialData.Clear();

        foreach (PlayerData.MaterialData material in itemTypeInfo.basicItemCost)
        {
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
