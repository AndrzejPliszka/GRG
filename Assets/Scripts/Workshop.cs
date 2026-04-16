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
    public void CreateItemServerRpc()
    {
        ItemProperties itemProperties = new() { 
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
