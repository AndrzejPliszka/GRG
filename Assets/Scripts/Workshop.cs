using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using static ItemData;

public class Workshop : MonoBehaviour
{
    private ItemData.ItemType itemType;
    public ItemData.ItemType ItemType
    {
        get { return itemType; }
        set {
            itemType = value;
            UpdateWorkshopSpriteClientRpc();
        }
    }
    public ItemData.ItemTier itemTier;

    [SerializeField] SpriteRenderer itemTypeSprite;
    [SerializeField] SpriteRenderer itemTierSprite;
    Vector2 spriteSize = new(0.8f, 0.8f);

    [SerializeField] ItemTypeData itemTypeData;
    [SerializeField] ItemTierData itemTierData;

    [SerializeField] List<PlayerData.MaterialData> _itemMaterialCost;
    [HideInInspector] public List<PlayerData.MaterialData> ItemMaterialCost { get; private set; } = new();


    private void Awake()
    {
        foreach (PlayerData.MaterialData material in _itemMaterialCost)
            ItemMaterialCost.Add(material);
    }

    [Rpc(SendTo.Everyone)]
    public void UpdateWorkshopSpriteClientRpc()
    {
        if (itemTypeSprite == null || itemTierSprite == null)
            return;

        MaterialEntry itemTierInfo = itemTierData.GetDataOfItemTier(itemTier);
        ItemEntry itemTypeInfo = itemTypeData.GetDataOfItemType(ItemType);
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

    [Rpc(SendTo.Server)]
    public void CreateItemServerRpc(RpcParams rpcParams = default)
    {
        ulong playerId = rpcParams.Receive.SenderClientId;
        NetworkManager.Singleton.ConnectedClients.TryGetValue(playerId, out var player);

        //Check if there are materials in storage to create item
        bool areMaterialsAvailable = true;
        if (TryGetComponent<Storage>(out Storage storage) || ItemMaterialCost.Count == 0)
        {
            foreach (PlayerData.MaterialData neededMaterial in ItemMaterialCost)
                if (neededMaterial.amount > storage.GetMaterialDataOfRawMaterial(neededMaterial.materialType).amount)
                    areMaterialsAvailable = false;
        }
        else
            throw new System.Exception("There is no storage script attached to Workshop, even though items cost materials to be made");

        if (!areMaterialsAvailable) {
            if (player?.PlayerObject.TryGetComponent<PlayerUI>(out PlayerUI playerUI) ?? false)
                playerUI.DisplayErrorOwnerRpc("There are no materials in storage to create an item!");
            return;
        }

        //Subtract items
        if (storage || ItemMaterialCost.Count == 0)
        {
            foreach (PlayerData.MaterialData neededMaterial in ItemMaterialCost)
                storage.ChangeAmountOfMaterialInStorage(neededMaterial.materialType, -neededMaterial.amount);
                
        }

        //Spawn item
        ItemProperties itemProperties = new()
        {
            itemType = ItemType,
            itemTier = itemTier,
            durablity = itemTierData.GetDataOfItemTier(itemTier).maximumDurability
        };

        GameObject itemPrefab = itemTypeData.GetDataOfItemType(itemProperties.itemType).droppedItemPrefab;
        GameObject newItem = Instantiate(itemPrefab, transform.position + new Vector3(0, 1.5f), transform.rotation);
        newItem.GetComponent<NetworkObject>().Spawn();
        newItem.GetComponent<ItemData>().itemProperties.Value = itemProperties;
    }
}
