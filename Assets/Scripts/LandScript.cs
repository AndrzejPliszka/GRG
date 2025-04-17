using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

public class LandScript : NetworkBehaviour
{
    public enum Building
    {
        Shop
    }

    [SerializeField] GameObject shopAsset;

    public NetworkVariable<int> townId = new(0);
    public NetworkVariable<int> menuXPos = new(0);
    public NetworkVariable<int> menuYPos = new(0);
    public NetworkVariable<FixedString128Bytes> menuDisplayText = new("Test Land");
    public GameObject BuildingOnLand { get; private set; }

    public void BuildShopOnLand(ItemData.ItemProperties itemSoldByShop)
    {
        if (!IsServer) { throw new Exception("Only server can modify shops!"); }

        if (BuildingOnLand != null)
        {
            Debug.LogWarning("Called building function, when there is building already");
            return;
        }

        GameObject shop = Instantiate(shopAsset, transform.position, transform.rotation);
        shop.GetComponent<NetworkObject>().Spawn();
        Shop shopScript = shop.GetComponent<Shop>();
        shopScript.SetUpShop(itemSoldByShop);
        BuildingOnLand = shop;
        menuDisplayText.Value = $"{itemSoldByShop.itemTier} {itemSoldByShop.itemType} Shop";

        GameManager.Instance.TownData[townId.Value].OnLandChange.Invoke(menuXPos.Value, menuYPos.Value, Building.Shop, menuDisplayText.Value);
    }
}
