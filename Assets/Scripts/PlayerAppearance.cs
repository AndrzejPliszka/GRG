using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using static ItemData;

public class PlayerAppearance : NetworkBehaviour
{
    PlayerData playerData;
    Renderer playerRenderer;

    [SerializeField] List<GameObject> hats;
    readonly string headPath = "rig/metarig/spine/spine.001/spine.002/spine.003/spine.004/spine.005/spine.006/face/face_end";
    GameObject currentHat;
    //this not in scriptable object, because it is used only in this script 
    [System.Serializable]
    public class RoleEntry
    {
        public PlayerData.PlayerRole playerRole;
        public Texture roleTexture;
    }

    
    public List<RoleEntry> MainRoleTextures; //textures used when you see other players (and used on server)
    public List<RoleEntry> LocalRoleTextures;

    //all textures data
    NetworkVariable<int> hatId = new(-999, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

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
        if (IsOwner)
        {
            SetPlayerAppearanceServerRpc(PlayerPrefs.GetInt("Hat"));
        }


        //doing on all client on start because it needs to be synchronized
        playerRenderer = GetObjectRenderer(gameObject);
        if (!IsOwner)
        {
            ChangePlayerHat(hatId.Value);
        }
        if (!IsServer) { return; }
        if (!TryGetComponent<PlayerData>(out playerData))
        {
            Debug.LogError("PlayerData should be on this object when you are on the network");
            return;
        }
        ChangePlayerRoleTextureRpc(playerData.Role.Value, playerData.Role.Value); //call this to sync texture with everyone
        playerData.Role.OnValueChanged += ChangePlayerRoleTextureRpc;

        
    }

    [Rpc(SendTo.Server)]
    void SetPlayerAppearanceServerRpc(int setHatId)
    {
        //If skins are unlockable, add validation here
        hatId.Value = setHatId;


        ChangePlayerAppearanceForEveryoneRpc();
    }

    [Rpc(SendTo.Everyone)]
    void ChangePlayerAppearanceForEveryoneRpc()
    {
        if(IsOwner) { return; } //Owner needs to be set up independently (for example without hat etc.)
        ChangePlayerHat(hatId.Value);
    }

    public void ChangePlayerHat(int hatId)
    {
        if (currentHat)
            Destroy(currentHat);
        if(hatId < 0 || hatId >= hats.Count) //Hat that doesn't exist, don't try to spawn it
            return;

        GameObject hat = Instantiate(hats[hatId], transform.Find(headPath));
        hat.transform.localPosition = new Vector3(0, 0.001f, -0.0003f);
        hat.transform.localScale = hat.transform.localScale * 0.01f;
        currentHat = hat;
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
        
        //MAKE IT USE NEW SYSTEM FOR TEXTURES!
        /*
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
        }*/
    }
}
