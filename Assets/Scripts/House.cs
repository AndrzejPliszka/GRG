using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

public class House : NetworkBehaviour
{
    GameObject houseOwner;

    [SerializeField] GameObject door;
    Collider doorCollider;


    [field: SerializeField] NetworkVariable<float> price = new();
    public NetworkVariable<FixedString128Bytes> displayedText = new();

    private void Start()
    {
        doorCollider = door.GetComponent<Collider>();

        if(!IsServer) return;
        displayedText.Value = "Empty House";
        price.Value = Mathf.Round(price.Value * 100f) / 100f; //round to 2 decimal places
    }

    public void BuyHouse(GameObject buyer)
    {
        PlayerUI playerUI = buyer.GetComponent<PlayerUI>();
        if (houseOwner == null)
        {
            PlayerData playerData = buyer.GetComponent<PlayerData>();
            if (playerData)
            {
                bool shouldBuy = playerData.ChangeMoney(-price.Value);
                if (shouldBuy)
                {
                    ChangeHouseOwner(buyer);
                }
                else
                {
                    if (playerUI)
                        playerUI.DisplayErrorOwnerRpc("You are too poor to buy this house!");
                }
            }
        }
        else
        {
            if (playerUI)
                playerUI.DisplayErrorOwnerRpc("This house is already taken!");

        }
    }

    void ChangeHouseOwner(GameObject owner)
    {
        if (!IsServer) throw new System.Exception("You can change owner of the house only on Server side!");

        if (houseOwner)
            {
            Physics.IgnoreCollision(doorCollider, houseOwner.GetComponent<CharacterController>(), false);
            UpdateDoorCollisionServerRpc(false, RpcTarget.Single(houseOwner.GetComponent<NetworkObject>().OwnerClientId, RpcTargetUse.Temp));
        }
        houseOwner = owner;
        //duplicated code, maybe use function instead?
        Physics.IgnoreCollision(doorCollider, houseOwner.GetComponent<CharacterController>(), true);
        UpdateDoorCollisionServerRpc(true, RpcTarget.Single(houseOwner.GetComponent<NetworkObject>().OwnerClientId, RpcTargetUse.Temp));
        displayedText.Value = "House of " + houseOwner.GetComponent<PlayerData>().Nickname.Value;
    }

    //Same as RoleBasedCollider.ManageThisColliderOnClientRpc(), maybe move somewhere else?
    [Rpc(SendTo.SpecifiedInParams)]
    void UpdateDoorCollisionServerRpc(bool ignoreCollision, RpcParams rpcParams)
    {
         _ = rpcParams; //Used to not get warnings, RpcParams is needed so Rpc can be SendTo.SpecifiedInParams
         Collider playerCollider = NetworkManager.LocalClient.PlayerObject.GetComponent<Movement>().LocalPlayerModel.GetComponent<CharacterController>();
        Physics.IgnoreCollision(playerCollider, doorCollider, ignoreCollision);
    }

}
