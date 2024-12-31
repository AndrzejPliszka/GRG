using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class LagCompensation : NetworkBehaviour
{
    [SerializeField] GameObject simplifiedPlayer; //game object with collider same as player and no other data
    List<GameObject> spawnedPlayers = new();
    public void SimulatePlayersOnGivenTime(float time)
    {
        if (!IsServer) { throw new Exception("Client cannot do lag compensation, dum dum!"); }
        float detectionRadius = 15f;
        GameObject[] players = GameObject.FindGameObjectsWithTag("Player");

        foreach (GameObject player in players)
        {
            float distance = Vector3.Distance(transform.position, player.transform.position);
            if (distance <= detectionRadius && player != gameObject)
            {
                PastDataTracker pastDataTracker = player.GetComponent<PastDataTracker>();
                TimeData pastData = pastDataTracker.GetPastData(time);
                GameObject instantiatedPlayer = Instantiate(simplifiedPlayer, pastData.position, pastData.rotation);
                instantiatedPlayer.layer = LayerMask.NameToLayer("ServerSimulation");
                //We need to differentiate these prefabs, so we know which player they are meant to "represent"
                instantiatedPlayer.name = player.GetComponent<NetworkObject>().NetworkObjectId.ToString();
                spawnedPlayers.Add(instantiatedPlayer);
            }
        }
    }
    void DestroySimulatedPlayers()
    {
        if (!IsServer) { throw new Exception("Client cannot do lag compensation, dum dum!"); }
        for (int i = spawnedPlayers.Count - 1; i >= 0; i--) //for loop going down, so when we delete elements, indexes of next ones stay the same
        {
            spawnedPlayers[i].GetComponent<Collider>().enabled = false;
            spawnedPlayers.RemoveAt(i);
        }
    }
    public GameObject CheckIfPlayerHit(float cameraXRotation, float timeWhenHit)
    {
        if (!IsServer) { throw new Exception("This checks only for ServerSimulation layer!"); }
        SimulatePlayersOnGivenTime(timeWhenHit);
        Vector3 cameraOffset = GetComponent<Movement>().CameraOffset;
        //COPYING CODE! VERY SIMILIAR CODE IS IN OBJECTINTERACTION
        Quaternion verticalRotation = Quaternion.Euler(cameraXRotation, transform.rotation.eulerAngles.y, 0);
        Vector3 rayDirection = verticalRotation * Vector3.forward; //Changing quaternion into vector3, because Ray takes Vector3
        Vector3 cameraPosition = transform.position + new Vector3(0, cameraOffset.y) + transform.rotation * new Vector3(0, 0, cameraOffset.z);
        Ray ray = new(cameraPosition, rayDirection);
        Debug.DrawRay(cameraPosition, rayDirection * 100f, Color.red, 0.5f);
        LayerMask layersToDetect = LayerMask.GetMask("ServerSimulation");
        if (Physics.Raycast(ray, out RaycastHit hit, 10, layersToDetect))
        {
            if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(ulong.Parse(hit.transform.gameObject.name), out NetworkObject playerPrefab))
            {
                DestroySimulatedPlayers();
                return playerPrefab.gameObject;
            }
            else
            {
                DestroySimulatedPlayers();
                return null;
            }
                
        }
        else
        {
            DestroySimulatedPlayers();
            return null;
        }
    }
}
