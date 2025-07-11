using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using Unity.VisualScripting;
using UnityEngine;
using static ItemData;

public class PlayerAppearance : NetworkBehaviour
{
    PlayerData playerData;
    Renderer playerRenderer;

    [SerializeField] PlayerAppearanceData appearanceData;
    readonly string headPath = "rig/ORG-spine/ORG-spine.001/ORG-spine.002/ORG-spine.003/ORG-spine.004/ORG-spine.005/ORG-spine.006/ORG-face/ORG-face_end";
    GameObject currentHat;
    //this not in scriptable object, because it is used only in this script 
    [System.Serializable]
    public class RoleEntry
    {
        public PlayerData.PlayerRole playerRole;
        public Texture roleTexture;
    }

    //all textures data
    readonly NetworkVariable<int> hatId = new(-999, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner); //Change into server if taken from serv -999, means that it is unset yet
    readonly NetworkVariable<int> faceId = new(-999, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    readonly NetworkVariable<int> skinId = new(-999, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    readonly NetworkVariable<int> inprintId = new(-999, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

    private void Awake()
    {
        playerRenderer = GetObjectRenderer(gameObject);
    }
    private void Start()
    {

        if ((IsClient || IsServer) && (!TryGetComponent<PlayerData>(out playerData)))
        {
            Debug.LogError("PlayerData should be on this object when you are on the network");
            return;
        }

        if (IsOwner)
        {
            ChangePlayerRoleTextureRpc(playerData.Role.Value, playerData.Role.Value);
            hatId.Value = PlayerPrefs.GetInt("Hat");
            faceId.Value = PlayerPrefs.GetInt("Face");
            skinId.Value = PlayerPrefs.GetInt("Skin");
            inprintId.Value = PlayerPrefs.GetInt("Inprint");

            ChangePlayerSkin(skinId.Value);
            ChangePlayerInprint(inprintId.Value);
        }
        else
        {
            if(playerData)
                playerRenderer.material.SetTexture("_Outfit", appearanceData.GetOutfit(playerData.Role.Value));

            if (hatId.Value != -999)
                ChangePlayerHat(hatId.Value);
            else
                hatId.OnValueChanged += (int oldId, int newId) => { ChangePlayerHat(newId); }; //If hatId is unset, then wait until it is set

            if (faceId.Value != -999)
                ChangePlayerFace(faceId.Value);
            else
                faceId.OnValueChanged += (int oldId, int newId) => { ChangePlayerFace(newId); };

            if (skinId.Value != -999)
                ChangePlayerSkin(skinId.Value);
            else
                skinId.OnValueChanged += (int oldId, int newId) => { ChangePlayerSkin(newId); };

            if (inprintId.Value != -999)
                ChangePlayerInprint(inprintId.Value);
            else
                inprintId.OnValueChanged += (int oldId, int newId) => { ChangePlayerInprint(newId); };
        }
        if (IsServer)
        {
            playerData.Role.OnValueChanged += ChangePlayerRoleTextureRpc;
        }
        
    }
    
    public void ChangePlayerHat(int hatId)
    {
        if (currentHat)
            Destroy(currentHat);
        GameObject hatToSpawn = appearanceData.GetHat(hatId);
        if (hatToSpawn == null)
            return;
        GameObject hat = Instantiate(hatToSpawn, transform.Find(headPath));
        hat.transform.localPosition = new Vector3(0, 0.1f, -0.03f);
        hat.transform.localScale = hat.transform.localScale;
        currentHat = hat;
    }

    public void ChangePlayerFace(int faceId)
    {
        if (IsOwner)
        {
            //if is this player only change localTexture
            Movement movement = GetComponent<Movement>();
            if (movement.LocalPlayerModel.activeInHierarchy)
                GetObjectRenderer(movement.LocalPlayerModel).material.SetTexture("_Face", appearanceData.GetFace(faceId));
        }
        else
        {
            //if is other player change his texture
            playerRenderer.material.SetTexture("_Face", appearanceData.GetFace(faceId));
        }
    }

    public void ChangePlayerSkin(int skinId)
    {
        if (IsOwner)
        {
            //if is this player only change localTexture
            Movement movement = GetComponent<Movement>();
            if (movement.LocalPlayerModel.activeInHierarchy)
                GetObjectRenderer(movement.LocalPlayerModel).material.SetTexture("_Skin", appearanceData.GetSkin(skinId));
        }
        else
        {
            //if is other player change his texture
            playerRenderer.material.SetTexture("_Skin", appearanceData.GetSkin(skinId));
        }
    }
    public void ChangePlayerInprint(int inprintId)
    {
        if (IsOwner)
        {
            //if is this player only change localTexture
            Movement movement = GetComponent<Movement>();
            if (movement.LocalPlayerModel.activeInHierarchy)
                GetObjectRenderer(movement.LocalPlayerModel).material.SetTexture("_Inprint", appearanceData.GetInprint(inprintId));
        }
        else
        {
            //if is other player change his texture
            playerRenderer.material.SetTexture("_Inprint", appearanceData.GetInprint(inprintId));
        }
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
            if(movement.LocalPlayerModel.activeInHierarchy)
                GetObjectRenderer(movement.LocalPlayerModel).material.SetTexture("_Outfit", appearanceData.GetOutfit(currentRole));
        }
        else
        {
            //if is other player change his texture
            playerRenderer.material.SetTexture("_Outfit", appearanceData.GetOutfit(currentRole));
        }
    }
}
