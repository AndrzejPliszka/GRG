using Unity.Netcode;
using UnityEngine;

//Item which when picked up give the player a material
public class GatherableMaterial : NetworkBehaviour
{

    [SerializeField] PlayerData.MaterialData _material;

    public NetworkVariable<PlayerData.MaterialData> Material { get; private set; } = new();

    public override void OnNetworkSpawn()
    {
        if (!IsServer) { return; }
        Material.Value = _material;
    }
}
