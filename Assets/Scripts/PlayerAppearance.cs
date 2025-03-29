using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using static ItemData;

[RequireComponent(typeof(PlayerData))]
public class PlayerApperance : NetworkBehaviour
{
    PlayerData playerData;
    Renderer playerRenderer;

    //this not in scriptable object, because it is used only in this script 
    [System.Serializable]
    public class RoleEntry
    {
        public PlayerData.PlayerRole playerRole;
        public Texture roleTexture;
    }

    
    public List<RoleEntry> MainRoleTextures; //textures used when you see other players (and used on server)
    public List<RoleEntry> LocalRoleTextures;

    public RoleEntry GetDataOfRole(List<RoleEntry> listToCheck, PlayerData.PlayerRole playerRole)
    {
        int indexToFind = listToCheck.FindIndex(item => item.playerRole == playerRole);
        if (indexToFind < 0)
        {
            Debug.LogWarning($"Role {playerRole} not found");
            return null;
        }
        else
            return listToCheck[indexToFind];
    }



    private void Start()
    {
        //doing on all client on start because it needs to be synchronized
        playerData = GetComponent<PlayerData>();
        playerRenderer = GetObjectRenderer(gameObject);
        ChangePlayerRoleTextureRpc(playerData.Role.Value, playerData.Role.Value); //call this to sync texture with everyone
        if (!IsServer) { return; }
        playerData.Role.OnValueChanged += ChangePlayerRoleTextureRpc;
    }

    //this function if object doesnt have renderer looks at children of gameObjects and returns first renderer of child
    static Renderer GetObjectRenderer(GameObject gameObject)
    {
        if(!gameObject.TryGetComponent<Renderer>(out var renderer))
            renderer = gameObject.GetComponentInChildren<Renderer>();
        return renderer;
    }

    [Rpc(SendTo.Everyone)]
    void ChangePlayerRoleTextureRpc(PlayerData.PlayerRole previousRole, PlayerData.PlayerRole currentRole)
    {
        if (IsOwner)
        {
            //if is this player only change localTexture
            Movement movement = GetComponent<Movement>();
            GetObjectRenderer(movement.LocalPlayerModel).sharedMaterial.mainTexture = GetDataOfRole(LocalRoleTextures, currentRole).roleTexture;
        }
        else
        {
            //if is other player change his texture
            playerRenderer.sharedMaterial.mainTexture = GetDataOfRole(MainRoleTextures, currentRole).roleTexture;
        }
    }
}
