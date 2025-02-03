using System.Collections;
using System.Collections.Generic;
using Unity.Burst.CompilerServices;
using Unity.Netcode;
using Unity.VisualScripting;
using UnityEditor.PackageManager;
using UnityEngine;
using UnityEngine.UIElements;

public class SpawnObjectsOnObject : NetworkBehaviour
{
    [SerializeField] GameObject objectToSpawn;
    [SerializeField] int targetNumberOfObjects;
    [SerializeField] float minimalRadius; //Object cannot spawn in radius (lower than this 
    Bounds bounds;
    public override void OnNetworkSpawn()
    {
        if (!IsServer) { return; }

        bounds = GetComponent<Collider>().bounds;
        Debug.Log("To sie zrobi³o!");
        for (int i = 0; i < targetNumberOfObjects; i++)
        {
            SpawnSingleObject();
        }
    }

    void SpawnSingleObject()
    {
        if(!IsServer) { throw new System.Exception("Client cannot spawn objcets!"); }
        float randomX = Random.Range(bounds.min.x, bounds.max.x);
        float randomZ = Random.Range(bounds.min.z, bounds.max.z);
        Vector3 rayStart = new(randomX, bounds.max.y + 1, randomZ);

        if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, 10))
        {
            if (CanSpawn(hit.point + new Vector3(0, 1, 0)))
            {
                GameObject spawnedObject = Instantiate(objectToSpawn, hit.point, Quaternion.Euler(new Vector3(0, Random.Range(0, 360), 0)));
                spawnedObject.GetComponent<NetworkObject>().Spawn();
            }
            
        }
    }

    bool CanSpawn(Vector3 locationOfObjectToSpawn)
    {
        Collider[] colliders = Physics.OverlapSphere(locationOfObjectToSpawn, minimalRadius);
        foreach (Collider col in colliders)
        {
            Vector3 closestPoint;
            if (col is BoxCollider || col is SphereCollider || col is CapsuleCollider) //col.ClosestPoint doesn't work with other colliders
                closestPoint = col.ClosestPoint(locationOfObjectToSpawn);
            else
                continue;

            float distanceXZ = Vector2.Distance(new Vector2(closestPoint.x, closestPoint.z),
                                                new Vector2(locationOfObjectToSpawn.x, locationOfObjectToSpawn.z));

            if (distanceXZ <= minimalRadius)
                return false;
        }

        return true;
    }
}
