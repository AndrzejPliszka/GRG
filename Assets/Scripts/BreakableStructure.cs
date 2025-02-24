using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class BreakableStructure : NetworkBehaviour
{
    [SerializeField] int maximumHealth;
    public NetworkVariable<int> Health { get; private set; } = new();
    public DynamicObjectSpawning spawner; //this is used to communicate with spawner. it may be empty if object was put manually itp.

    public override void OnNetworkSpawn()
    {
        if(!IsServer) return;
        Health.Value = maximumHealth;
    }

    public void ChangeHealth(int amountToChange)
    {
        if (!IsServer) throw new System.Exception("You can change HP of an object only on Server side!");
        Health.Value += amountToChange;

        if (Health.Value <= 0)
        {
            gameObject.GetComponent<NetworkObject>().Despawn();
            Destroy(gameObject);
        }
            

        if (Health.Value > maximumHealth)
            Health.Value = maximumHealth;
    }

    override public void OnDestroy()
    {
        if (spawner != null)
            spawner.DecreaseNumberOfSpawnedObjects();
    }
}
