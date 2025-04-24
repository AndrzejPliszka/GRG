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
        Null, //Used for empty land
        Shop,
        House
    }

    [SerializeField] GameObject shopAsset;

    public NetworkVariable<int> townId = new(0);
    public NetworkVariable<int> menuXPos = new(0);
    public NetworkVariable<int> menuYPos = new(0);
    public NetworkVariable<FixedString128Bytes> menuDisplayText = new("Test Land");

    GameObject _buildingOnLand;
    public GameObject BuildingOnLand {
        get { 
            return _buildingOnLand;
        }
        set {
            if (value == null)
            {
                BuildingType = Building.Null;
                menuDisplayText.Value = $"Empty land";
            }
            else if (value.GetComponent<Shop>() != null)
            {
                ItemData.ItemProperties itemSoldByShop = value.GetComponent<Shop>().SoldItem;
                menuDisplayText.Value = $"{itemSoldByShop.itemTier} {itemSoldByShop.itemType} Shop";
                BuildingType = Building.Shop;
            }
                

            if(value != _buildingOnLand)
            {
                GameManager.Instance.TownData[townId.Value].OnLandChange.Invoke(menuXPos.Value, menuYPos.Value, BuildingType, menuDisplayText.Value);
            }
            _buildingOnLand = value;
        }
    }
    public Building BuildingType { get; private set; } //used for easy access elsewhere

    public void BuildShopOnLand(ItemData.ItemProperties itemSoldByShop)
    {
        if (!IsServer) { throw new Exception("Only server can modify land!"); }

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
        BreakableStructure breakableStructure = shop.GetComponent<BreakableStructure>();
        if (breakableStructure != null)
            breakableStructure.land = this;

    }

    public void DestroyBuilding()
    {
        if (!IsServer) { throw new Exception("Only server can modify land!"); }

        Destroy(BuildingOnLand);
        BuildingOnLand = null;

    }
}
