using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SkinSelector : MonoBehaviour
{
    [SerializeField] GameObject mockModel;
    [SerializeField] GameObject mainCamera;
    readonly float rotatingSpeed = 3f;

    private void Start()
    {
        //Set up skin
        int hatId = PlayerPrefs.GetInt("Hat", -1);
        GameObject.Find("Player").GetComponent<PlayerAppearance>().ChangePlayerHat(hatId);
        
    }

    private void FixedUpdate()
    {
        if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow))
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

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Alpha1))
        {
            PlayerPrefs.SetInt("Hat", 0);
            GameObject.Find("Player").GetComponent<PlayerAppearance>().ChangePlayerHat(0);
        }
        else if (Input.GetKeyDown(KeyCode.Alpha2))
        {
            PlayerPrefs.SetInt("Hat", 1);
            GameObject.Find("Player").GetComponent<PlayerAppearance>().ChangePlayerHat(1);
        }
        else if (Input.GetKeyDown(KeyCode.Alpha3))
        {
            PlayerPrefs.SetInt("Hat", 2);
            GameObject.Find("Player").GetComponent<PlayerAppearance>().ChangePlayerHat(2);
        }
        else if (Input.GetKeyDown(KeyCode.Alpha4))
        {
            PlayerPrefs.SetInt("Hat", -1);
            GameObject.Find("Player").GetComponent<PlayerAppearance>().ChangePlayerHat(3);
        }
    }
}
