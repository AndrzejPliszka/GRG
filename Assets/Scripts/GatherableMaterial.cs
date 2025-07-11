using Unity.Netcode;
using UnityEngine;

//Item which when picked up give the player a material
public class GatherableMaterial : NetworkBehaviour
{

    [SerializeField] PlayerData.RawMaterial _material;
    [SerializeField] int _amount;

    public NetworkVariable<PlayerData.RawMaterial> Material { get; private set; } = new();
    public NetworkVariable<int> Amount { get; private set; } = new();

    public override void OnNetworkSpawn()
    {
        if (!IsServer) { return; }
        Material.Value = _material;
        Amount.Value = _amount;
    }
}
