using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

public class BreakableStructure : NetworkBehaviour
{
    [SerializeField] RawMaterialData materialObjectData;
    public NetworkVariable<int> MaximumHealth = new();
    public NetworkVariable<int> Health { get; private set; } = new();
    public DynamicObjectSpawning spawner; //this is used to communicate with spawner. it may be empty if object was put manually itp.
    public LandScript land; //also used to communicate with spawner, but in case of land plot in town
    [field: SerializeField] public List<PlayerData.MaterialData> droppedMaterials = new(); //maxAmount in this structure should be unused, if in future you want to modify this in runtime, use NetworkList<>
    [SerializeField] float dropMaterialPositionSpawnVariance = 0.2f;
    [SerializeField] float dropMaterialYOffset = 0.5f;

    PlayerData.RawMaterial repairMaterial;

    public override void OnNetworkSpawn()
    {
        if(!IsServer) return;
        Health.Value = MaximumHealth.Value;
    }

    public void ChangeHealth(int amountToChange)
    {
        if (!IsServer) throw new System.Exception("You can change HP of an object only on Server side!");
        Health.Value += amountToChange;

        if (Health.Value <= 0)
        {
            gameObject.GetComponent<NetworkObject>().Despawn();
            SpawnDroppedItems();
            Destroy(gameObject);
        }
            

        if (Health.Value > MaximumHealth.Value)
            Health.Value = MaximumHealth.Value;
    }
    private bool isShuttingDown = false;

    private void OnApplicationQuit()
    {
        isShuttingDown = true;
    }

    override public void OnDestroy()
    {
        if (isShuttingDown || !Application.isPlaying || !IsServer || !SceneManager.GetActiveScene().isLoaded || NetworkManager == null || NetworkManager.ShutdownInProgress || (NetworkManager.Singleton != null && !NetworkManager.Singleton.IsListening))
            return;
        if (spawner != null)
            spawner.DecreaseNumberOfSpawnedObjects(gameObject.tag);
        if (land != null)
            land.BuildingOnLand = null;
    }

    void SpawnDroppedItems()
    {
        foreach (PlayerData.MaterialData material in droppedMaterials)
        {
            for (int i = 0; i < material.amount; i++)
            {
                GameObject spawnedObject = Instantiate(materialObjectData.GetMaterialObject(material.materialType).droppedMaterialObject,
                    transform.position + new Vector3(Random.Range(-dropMaterialPositionSpawnVariance, dropMaterialPositionSpawnVariance), i * dropMaterialYOffset, Random.Range(-dropMaterialPositionSpawnVariance, dropMaterialPositionSpawnVariance)),
                    Quaternion.identity
                );
                if (spawnedObject.GetComponent<NetworkObject>() != null)
                    spawnedObject.GetComponent<NetworkObject>().Spawn();
            }
        }
    }
}
