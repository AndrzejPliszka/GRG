using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Rendering;

public class UnbuiltBuilding : NetworkBehaviour
{
    [SerializeField] List<PlayerData.MaterialData> _neededMaterials = new(); //maxAmount is materials needed, amount is materials already delivered
    public NetworkList<PlayerData.MaterialData> NeededMaterials { get;  private set; } = new(); //maxAmount is materials needed, amount is materials already delivered
    public NetworkVariable<ulong> OwnerId;
    public NetworkList<float> MaterialPrices { get; private set; } = new(); //Materials in this list have same index as in NeededMaterials, so if you need a price, first find index of material in NeededMaterials and then use that index with this list
    [SerializeField] GameObject buildingToBuild;
    [SerializeField] GameObject singularMaterialDataInfo;
    [SerializeField] RawMaterialData rawMaterialData;
    public NetworkVariable<FixedString32Bytes> ObjectStringDescription = new("");
    readonly float panelWidth = 1.0f;
    readonly List<GameObject> buildingParts = new();

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            foreach (PlayerData.MaterialData material in _neededMaterials) { 
                NeededMaterials.Add(material);
                MaterialPrices.Add(0f);
            }
        }
        Transform buildPartsParent = transform.Find("UnbuiltParts");
        for(int i = 0; i < buildPartsParent.childCount; i++)
        {
            buildingParts.Add(buildPartsParent.GetChild(i).gameObject);
        }

        DisplayFinishedParts(new NetworkListEvent<PlayerData.MaterialData>());
        NeededMaterials.OnListChanged += DisplayFinishedParts;

        SetupNedeedMaterialUI();
        NeededMaterials.OnListChanged += ModifyNeededMaterialAmountUI;
    }
    private void Update()
    {
        RotateNeededMaterialUI();
    }
    void ResetMaterialUI()
    {
        foreach (Transform child in transform.Find("MaterialsNeededPanel"))
            GameObject.Destroy(child.gameObject);

        SetupNedeedMaterialUI();
    }
    void SetupNedeedMaterialUI()
    {
        List<PlayerData.MaterialData> materialsToDisplay = new();
        //Making new list without materials that are already fully provided
        foreach(PlayerData.MaterialData materialData in NeededMaterials)
        {
            if(materialData.maxAmount - materialData.amount > 0)
                materialsToDisplay.Add(materialData);
        }
        for (int i = 0; i < materialsToDisplay.Count; i++)
        {
            float offset = -(materialsToDisplay.Count - i - 1) * (panelWidth/3) + i * (panelWidth / 3);
            transform.Find("MaterialsNeededPanel").rotation = Quaternion.Euler(0, 0, 0); //Resetting rotation for a moment, so material info gets Instantianted in correct place
            GameObject materialInfo = Instantiate(singularMaterialDataInfo, transform.Find("MaterialsNeededPanel"));
            materialInfo.transform.position = materialInfo.transform.position + new Vector3(offset, 0);
            TMP_Text materialAmount = materialInfo.transform.Find("AmountOfMaterial").GetComponent<TMP_Text>();
            SpriteRenderer materialSprite = materialInfo.transform.Find("MaterialTexture").GetComponent<SpriteRenderer>();
            PlayerData.MaterialData material = materialsToDisplay[i];
            materialAmount.text = (material.maxAmount - material.amount).ToString() + " P:" + MaterialPrices[i];
            materialSprite.sprite = rawMaterialData.GetMaterialObject(material.materialType).materialSprite;
            materialInfo.name = material.materialType.ToString();
        }
    }

    //Funtion has this argument, so it can be executed every time NeededMaterial changes
    void ModifyNeededMaterialAmountUI(NetworkListEvent<PlayerData.MaterialData> _)
    {
        Transform materialsNeededPanel = transform.Find("MaterialsNeededPanel");
        for (int i = 0; i < NeededMaterials.Count; i++)
        {
            Transform materialPanel = materialsNeededPanel.transform.Find(NeededMaterials[i].materialType.ToString());
            if (!materialPanel)
                continue;
            TMP_Text materialAmount = materialPanel.Find("AmountOfMaterial").GetComponent<TMP_Text>();
            materialAmount.text = (NeededMaterials[i].maxAmount - NeededMaterials[i].amount).ToString() + " P:" + MaterialPrices[i];
        }
    }

    void RotateNeededMaterialUI()
    {
        if (!NetworkManager.Singleton)
            return;
        ulong clientId = NetworkManager.Singleton.LocalClientId;
        NetworkObject playerObject;
        if (NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out var client))
        {
            playerObject = client.PlayerObject;
            if (playerObject == null)
                return;
            Transform neededText = transform.Find("MaterialsNeededText");
            Transform neededPanel = transform.Find("MaterialsNeededPanel");
            Vector3 direction = playerObject.transform.position - neededText.position;
            Quaternion lookRotation = Quaternion.LookRotation(direction);
            lookRotation = Quaternion.Euler(0, lookRotation.eulerAngles.y, lookRotation.eulerAngles.z);
            Quaternion offset = Quaternion.Euler(0, -180, 0);
            neededText.rotation = lookRotation * offset;
            neededPanel.rotation = lookRotation * offset;
        }
    }

    //false if it method fails, but recommended to check manually if this method will fail
    [Rpc(SendTo.Server)]
    public void DeliverMaterialsServerRpc(PlayerData.RawMaterial materialType, int amount, ulong deliveringPlayerId)
    {
        if (amount == 0)
            return;

        if (!NetworkManager.Singleton.ConnectedClients.TryGetValue(OwnerId.Value, out var buildingOwner))
            return;

        if (!NetworkManager.Singleton.ConnectedClients.TryGetValue(deliveringPlayerId, out var deliveringPlayer))
            return;

        if ((GetPriceOfMaterial(materialType) * amount <= buildingOwner.PlayerObject.GetComponent<PlayerData>().Money.Value || deliveringPlayerId == OwnerId.Value)
            && deliveringPlayer.PlayerObject.GetComponent<PlayerData>().OwnedMaterials[(int)materialType].amount >= amount)
        {
            for (int i = 0; i < NeededMaterials.Count; i++)
            {
                var material = NeededMaterials[i];
                if (material.materialType == materialType)
                {
                    material.amount += amount;
                    if (material.amount > material.maxAmount)
                        throw new System.Exception("If this was delivered, Amount would be greater than MaxAmount, reminder that validation is on your side, pal!");
                    NeededMaterials[i] = material;
                    deliveringPlayer.PlayerObject.GetComponent<PlayerData>().ChangeAmountOfMaterial(materialType, -amount);
                    if (deliveringPlayerId != OwnerId.Value)
                    {
                        float pricePerUnit = GetPriceOfMaterial(materialType);
                        deliveringPlayer.PlayerObject.GetComponent<PlayerData>().ChangeMoney(amount * pricePerUnit);
                        buildingOwner.PlayerObject.GetComponent<PlayerData>().ChangeMoney(-amount * pricePerUnit);
                    }
                    if (material.amount == material.maxAmount) //We dont want to display material which has been fully delivered
                    {
                        ResetMaterialUI();
                        bool didSucceed = TryBuildBuilding();
                        if (didSucceed)
                            return;
                    }
                }
            }
        }
    }

    //Funtion has this argument, so it can be executed every time NeededMaterial changes
    void DisplayFinishedParts(NetworkListEvent<PlayerData.MaterialData> _)
    {
        int amountOfNeededMaterials = 0;
        int amountOfDeliveredMaterials = 0;
        foreach (var material in NeededMaterials)
        {
            amountOfNeededMaterials += material.maxAmount;
            amountOfDeliveredMaterials += material.amount;
        }
        float deliveredRatio = (float)amountOfDeliveredMaterials / amountOfNeededMaterials;
        int numberOfDisplayedChildrenParts = Mathf.FloorToInt(deliveredRatio * buildingParts.Count);
        int currentChild = 0;
        foreach(GameObject part in buildingParts)
        {
            if (currentChild < numberOfDisplayedChildrenParts)
                part.SetActive(true);
            else
                part.SetActive(false);

            currentChild++;
        }
    }

    [Rpc(SendTo.Server)]
    public void ChangePriceOfMaterialServerRpc(PlayerData.RawMaterial rawMaterial, float price)
    {
        //TODO: MAKE ANTICHEAT f.e. CHECK IF PLAYER CALLING THIS FUNCTION IS OWNER OF BUILDING etc.

        for (int i = 0; i < NeededMaterials.Count; i++)
        {
            if (NeededMaterials[i].materialType == rawMaterial)
                MaterialPrices[i] = (float)Mathf.Round(price * 100) / 100f;
        }
    }

    public float GetPriceOfMaterial(PlayerData.RawMaterial rawMaterial)
    {
        for (int i = 0; i < NeededMaterials.Count; i++)
        {
            if (NeededMaterials[i].materialType == rawMaterial)
                return MaterialPrices[i];
        }
        throw new System.Exception("Material of this type doesn't exist in this building");
    }

    public PlayerData.MaterialData GetMaterialDataOfMaterial(PlayerData.RawMaterial rawMaterial)
    {
        for (int i = 0; i < NeededMaterials.Count; i++)
        {
            if (NeededMaterials[i].materialType == rawMaterial)
                return NeededMaterials[i];
        }
        throw new System.Exception("Material of this type doesn't exist in this building");
    }
    public bool IsCompletelyUnbuilt()
    {
        bool isCompletelyUnbuilt = true;
        foreach (var material in NeededMaterials)
        {
            if(material.amount != 0)
                isCompletelyUnbuilt = false;
        }
        return isCompletelyUnbuilt;
    }

    bool TryBuildBuilding()
    {
        //Check if there is correct number of materials
        foreach (var material in NeededMaterials) {
            if (material.amount < material.maxAmount)
                return false;
        }

        GameObject building = Instantiate(buildingToBuild, transform.position, transform.rotation);
        //CHANGE THIS CODE TO INCLUDE OTHER BUILDINGS !!!!!
        building.GetComponent<NetworkObject>().Spawn();
        building.GetComponent<Storage>().OwnerId.Value = OwnerId.Value;
        Destroy(gameObject);
        GetComponent<NetworkObject>().Despawn();
        return true;
    }
}
