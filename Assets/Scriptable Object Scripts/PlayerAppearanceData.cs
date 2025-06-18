using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.XR;

[Serializable]
public class OutfitTexture
{
    public PlayerData.PlayerRole playerRole;
    public Texture2D texture;
}

[CreateAssetMenu(fileName = "PlayerSkinData", menuName = "ScriptableObjects/PlayerSkinData", order = 1)]
public class PlayerAppearanceData : ScriptableObject
{
    [SerializeField] List<GameObject> hat = new();
    [SerializeField] List<Texture2D> skin = new();
    [SerializeField] List<Texture2D> face = new();
    [SerializeField] List<Texture2D> inprint = new();
    [SerializeField] List<OutfitTexture> outfit = new();

    public GameObject GetHat(int hatId)
    {
        if (hatId < 0 || hatId >= hat.Count) {
            return null; }//return no hat
        else
            return hat[hatId];
    }

    public Texture2D GetOutfit(PlayerData.PlayerRole role) {
        foreach (var item in outfit)
        {
            if (item.playerRole == role)
                return item.texture;
        }
        foreach (var item in outfit)
        {
            if (item.playerRole == PlayerData.PlayerRole.Citizen)
                return item.texture;
        }

        return outfit[0].texture; 
    }

    public Texture2D GetSkin(int skinId)
    {
        if (skinId < 0 || skinId >= skin.Count)
            return skin[0]; //return default skin
        else
            return skin[skinId];
    }

    public Texture2D GetFace(int faceId)
    {
        if (faceId < 0 || faceId >= skin.Count)
            return face[0];
        else
            return face[faceId];
    }

    public Texture2D GetInprint(int inprintId)
    {
        if (inprintId < 0 || inprintId >= skin.Count)
            return null; //return no inprint
        else
            return inprint[inprintId];
    }
}
