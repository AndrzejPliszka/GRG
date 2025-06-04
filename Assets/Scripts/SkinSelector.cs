using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SkinSelector : MonoBehaviour
{
    [SerializeField] GameObject mockModel;
    [SerializeField] GameObject mainCamera;
    readonly float rotatingSpeed = 2f;
    private void Update()
    {
        if(Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow))
        {
            mockModel.transform.Rotate(Vector3.up * rotatingSpeed);
        }
        else if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow))
        {
            mockModel.transform.Rotate(Vector3.down * rotatingSpeed);
        }

        if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow))
        {
            mainCamera.transform.Rotate(Vector3.right * rotatingSpeed);
        }
        else if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow))
        {
            mainCamera.transform.Rotate(Vector3.left * rotatingSpeed); 
        }
    }
}
