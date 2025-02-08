using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class BreakableStructure : NetworkBehaviour
{
    [SerializeField] int startingHealth;
    public NetworkVariable<int> Health { get; private set; } = new();
    public DynamicObjectSpawning spawner; //this is used to communicate with spawner. it may be empty if object was put manually itp.

    private void Start()
    {
        if(!IsServer) return;
        Health.Value = startingHealth;
        Health.OnValueChanged += HandleHealthChange;
    }

    public void ChangeHealth(int amountToChange)
    {
        if (!IsServer) throw new System.Exception("You can change HP of an object only on Server side!");
        Health.Value += amountToChange;
    }

    void HandleHealthChange(int oldHealthValue, int newHealthValue)
    {
        if(newHealthValue <= 0)
            Destroy(gameObject);
    }

    override public void OnDestroy()
    {
        if (spawner != null)
            spawner.DecreaseNumberOfSpawnedObjects();
    }
}
