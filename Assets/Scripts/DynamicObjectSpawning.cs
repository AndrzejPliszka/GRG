using System.Collections;
using System.Collections.Generic;
using Unity.Burst.CompilerServices;
using Unity.Netcode;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

public class DynamicObjectSpawning : NetworkBehaviour
{
    [System.Serializable]
    public class ObjectSpawnData
    {
        public GameObject objectToSpawn;
        public int targetNumberOfObjects;
        public float minimalRadius; //Object cannot spawn in radius (lower than this )
        public float minimalCooldownBetweenSpawns; //specifies seconds seconds after which object will try to spawn, but it may not succeed (so there is no maximum cooldown) (Also if there is no objects on plate it is guaranteed that one will spawn after this amount of time)
        public float rayHeight; //height from which ray will be casted
        [HideInInspector] public int currentNumberOfObjects;
    }

    [SerializeField] List<ObjectSpawnData> objectsToSpawn;

    Bounds bounds;
    public override void OnNetworkSpawn()
    {
        if (!IsServer) { return; }

        bounds = GetComponent<Collider>().bounds;
        foreach(ObjectSpawnData spawnData in objectsToSpawn)
        {
            for (int i = 0; i < spawnData.targetNumberOfObjects; i++)
                TrySpawnSingleObject(spawnData);

            StartCoroutine(SpawnObjectsContinuosly(spawnData));
        }
        
    }

    IEnumerator SpawnObjectsContinuosly(ObjectSpawnData spawnData)
    {
        if (!IsServer) { throw new System.Exception("Client cannot spawn objects!"); }
        while (IsServer) {
            if(spawnData.currentNumberOfObjects < spawnData.targetNumberOfObjects)
                TrySpawnSingleObject(spawnData);
            yield return new WaitForSeconds(spawnData.minimalCooldownBetweenSpawns);
        }
    }

    void TrySpawnSingleObject(ObjectSpawnData spawnData)
    {
        if(!IsServer) { throw new System.Exception("Client cannot spawn objects!"); }
        float randomX = Random.Range(bounds.min.x, bounds.max.x);
        float randomZ = Random.Range(bounds.min.z, bounds.max.z);
        Vector3 rayStart = new(randomX, bounds.max.y + spawnData.rayHeight, randomZ);

        if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, 10 + spawnData.rayHeight))
        {
            if (CanSpawn(hit.point + new Vector3(0, 1, 0), spawnData.minimalRadius) && hit.transform.gameObject == gameObject)
            {
                if (!IsServer || !SceneManager.GetActiveScene().isLoaded || NetworkManager == null || NetworkManager.ShutdownInProgress || NetworkManager.Singleton == null)
                    return; //This function can be called when server is shutting down, so we need to check if we are still in the game

                GameObject spawnedObject = Instantiate(spawnData.objectToSpawn, hit.point, Quaternion.Euler(new Vector3(0, Random.Range(0, 360), 0)));
                spawnedObject.GetComponent<NetworkObject>().Spawn();

                spawnedObject.TryGetComponent<BreakableStructure>(out BreakableStructure spawnedObjectBreakableScript); //setting up refrence to this script for communication
                if (spawnedObjectBreakableScript != null)
                    spawnedObjectBreakableScript.spawner = this;

                spawnData.currentNumberOfObjects++;
            }
            
        }
    }

    bool CanSpawn(Vector3 locationOfObjectToSpawn, float minimalRadius)
    {
        Collider[] colliders = new Collider[10];
        Physics.OverlapSphereNonAlloc(locationOfObjectToSpawn, minimalRadius, colliders);
        foreach (Collider col in colliders)
        {
            Vector3 closestPoint;
            if (col is BoxCollider || col is SphereCollider || col is CapsuleCollider) //col.ClosestPoint doesn't work with other colliders
                closestPoint = col.ClosestPoint(locationOfObjectToSpawn);
            else
                continue;

            float distanceXZ = Vector2.Distance(new Vector2(closestPoint.x, closestPoint.z), new Vector2(locationOfObjectToSpawn.x, locationOfObjectToSpawn.z));

            if (distanceXZ <= minimalRadius)
                return false;
        }

        return true;
    }
    public void DecreaseNumberOfSpawnedObjects(string objectTag)
    {
        foreach (ObjectSpawnData spawnData in objectsToSpawn)
        {
            if (spawnData.objectToSpawn.CompareTag(objectTag))
            {
                spawnData.currentNumberOfObjects--;
                return;
            }
        }
    }
}
