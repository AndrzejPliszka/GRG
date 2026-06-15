using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using static PlayerData;

public class RagdollData : MonoBehaviour
{
    public NetworkVariable<FixedString32Bytes> Nickname { get; private set; } = new("Corpse");
    public NetworkVariable<PlayerRole> Role { get; private set; } = new(PlayerRole.Peasant);
}
