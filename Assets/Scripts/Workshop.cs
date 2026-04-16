using Unity.Netcode;
using UnityEngine;
using static ItemData;

public class Workshop : MonoBehaviour
{
    public ItemData.ItemType itemType;
    public ItemData.ItemTier itemTier;

    [SerializeField] ItemTypeData itemTypeData;
    [SerializeField] ItemTierData itemTierData;

    [Rpc(SendTo.Server)]
    public void CreateItemServerRpc()
    {
        ItemProperties itemProperties = new() { 
            itemType = itemType,
            itemTier = itemTier,
            durablity = itemTierData.GetDataOfItemTier(itemTier).maximumDurability
        };

        GameObject itemPrefab = itemTypeData.GetDataOfItemType(itemProperties.itemType).droppedItemPrefab;
        GameObject newItem = Instantiate(itemPrefab, transform.position + new Vector3(0, 1.5f), transform.rotation);
        newItem.GetComponent<NetworkObject>().Spawn();
        newItem.GetComponent<ItemData>().itemProperties.Value = itemProperties;
    }
}
