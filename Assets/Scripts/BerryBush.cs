using System.Collections;
using Unity.Netcode;
using UnityEngine;

public class BerryBush : NetworkBehaviour
{
    [field: SerializeField] public int FoodAmount { get; private set; } //It is how much hunger and how many materials you get for now
    [SerializeField] float regenerationTime;
    [SerializeField] GameObject berries;

    public NetworkVariable<bool> HasBerries { get; private set; } = new(true);

    public override void OnNetworkSpawn()
    {
        HasBerries.OnValueChanged += (oldValue, newValue) =>
        {
            berries.SetActive(newValue);
        };
    }

    public void RemoveBerries()
    {
        HasBerries.Value = false;
        StartCoroutine(RegrowBerries());
    }
    IEnumerator RegrowBerries()
    {
        yield return new WaitForSeconds(regenerationTime);
        HasBerries.Value = true;
    }
}
