using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.UIElements;
[RequireComponent(typeof(Movement))]
public class ObjectInteraction : NetworkBehaviour
{
    Vector3 cameraOffset;
    void Start()
    {
        cameraOffset = gameObject.GetComponent<Movement>().CameraOffset;
    }

    // Update is called once per frame
    void Update()
    {
        if(!IsOwner) {  return; }
        float cameraXRotation = GameObject.Find("Camera").transform.rotation.eulerAngles.x;
        GetObjectInFrontOfCameraServerRpc(GameObject.Find("Camera").transform.rotation.eulerAngles.x);
    }

    //This script will detect any object which is in front of camera. It cannot return this object, because it is serverRpc.
    //CHANGE NAME TO BE MORE DESCRIPTIVE!
    [Rpc(SendTo.Server)]
    void GetObjectInFrontOfCameraServerRpc(float cameraXRotation)
    {
        Quaternion verticalRotation = Quaternion.Euler(cameraXRotation, transform.rotation.eulerAngles.y, 0);
        Vector3 rayDirection = verticalRotation * Vector3.forward; //Changing quaternion into vector3, because Ray takes Vector3
        Vector3 cameraPosition = transform.position + new Vector3(0, cameraOffset.y) + transform.rotation * new Vector3(0, 0, cameraOffset.z);
        Ray ray = new Ray(cameraPosition, rayDirection);
        Debug.DrawRay(cameraPosition, rayDirection * 100f, Color.red, 0.5f);
        LayerMask layersToDetect = ~LayerMask.GetMask("LocalObject"); // ~ negates bytes, which makes that layersToDetect is all masks except LocalObject (only relevant on host)
        if (Physics.Raycast(ray, out RaycastHit hit, 10, layersToDetect))
        {
            string hitObjectTag = hit.collider.gameObject.tag;
            switch(hitObjectTag)
            {
                case "Player":
                    DisplayTextOnScreenClientRpc(hit.collider.gameObject.GetComponent<PlayerData>().Nickname.Value);
                    break;
                default:
                    DisplayTextOnScreenClientRpc("");
                    break;
            };
        }
        else
        {
            DisplayTextOnScreenClientRpc("");
        }
    }

    [Rpc(SendTo.Owner)]
    public void DisplayTextOnScreenClientRpc(FixedString32Bytes stringToDisplay)
    {
        GameObject.Find("CenterText").GetComponent<TMP_Text>().text = stringToDisplay.ToString();
    }
}
