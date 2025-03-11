using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class RoleBasedCollider : NetworkBehaviour
{
    Collider objectCollider;
    [SerializeField] PlayerData.PlayerRole selectedRole;
    [SerializeField] bool ignoreCollisionOfSelectedRole; //if false it will collide with every player who doesn't have that role
    // Start is called before the first frame update
    public override void OnNetworkSpawn()
    {
        objectCollider = GetComponent<Collider>();
        if (!IsServer) { return; }
        GameManager.Instance.OnPlayerRoleChange += SetUpCollision;
        SetUpCollision(new GameObject(), new PlayerData.PlayerRole()); //empty because this is redundant on start
    }

    [Rpc(SendTo.SpecifiedInParams)]
    void ManageThisColliderOnClientRpc(bool ignoreCollision, RpcParams rpcParams)
    {
        _ = rpcParams; //Used to not get warnings, RpcParams is needed so Rpc can be SendTo.SpecifiedInParams
        Collider playerCollider = NetworkManager.LocalClient.PlayerObject.GetComponent<Movement>().LocalPlayerModel.GetComponent<CharacterController>();
        Physics.IgnoreCollision(playerCollider, GetComponent<Collider>(), ignoreCollision);
    }

    void SetUpCollision(GameObject gameObject, PlayerData.PlayerRole role) //TO DO: OPTIMIZE THIS SO THIS FUNCTION ACTUALLY USES PARAMETERS TO BE FASTER
    {
        if (!IsServer) { throw new System.Exception("You cannot use this method on client, as it uses server only info"); }

        for (int i = 0; i < GameManager.Instance.TownData.Count; i++) {
            foreach (var townRole in GameManager.Instance.TownData[i].townMembers)
            {
                foreach (var player in townRole.Value)
                {
                    if (townRole.Key == selectedRole)
                    {
                        Physics.IgnoreCollision(objectCollider, player.GetComponent<CharacterController>(), ignoreCollisionOfSelectedRole);
                        ManageThisColliderOnClientRpc(ignoreCollisionOfSelectedRole, RpcTarget.Single(player.GetComponent<NetworkObject>().OwnerClientId, RpcTargetUse.Temp)); //I'm not sure if RpcTargetUse.Temp is most approperiate here 
                    }
                    else
                    {
                        Physics.IgnoreCollision(objectCollider, player.GetComponent<CharacterController>(), !ignoreCollisionOfSelectedRole);
                        ManageThisColliderOnClientRpc(!ignoreCollisionOfSelectedRole, RpcTarget.Single(player.GetComponent<NetworkObject>().OwnerClientId, RpcTargetUse.Temp));
                    }
                        
                }
            }
        }
        //there is different list only for peasants
        foreach (var player in GameManager.Instance.PlayersWithoutTown)
        {
            Debug.Log("Jest wieœniak w rejestrze");
            if (PlayerData.PlayerRole.Peasant == selectedRole)
            {
                Physics.IgnoreCollision(objectCollider, player.GetComponent<CharacterController>(), ignoreCollisionOfSelectedRole);
                ManageThisColliderOnClientRpc(ignoreCollisionOfSelectedRole, RpcTarget.Single(player.GetComponent<NetworkObject>().OwnerClientId, RpcTargetUse.Temp));
            }
            else
            {
                Physics.IgnoreCollision(objectCollider, player.GetComponent<CharacterController>(), !ignoreCollisionOfSelectedRole);
                ManageThisColliderOnClientRpc(!ignoreCollisionOfSelectedRole, RpcTarget.Single(player.GetComponent<NetworkObject>().OwnerClientId, RpcTargetUse.Temp));
            }
        }
    }
}
