using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using static UnityEngine.UI.Image;

public class SkinSelector : MonoBehaviour
{
    [SerializeField] GameObject mockModel;
    [SerializeField] GameObject mainCamera;
    [SerializeField] PlayerAppearanceData playerAppearanceData;
    readonly float rotatingSpeed = 3f;

    int selectedHatId = -1; 
    int selectedFaceId = 0;
    int selectedSkinId = 0;
    int selectedInprintId = 0;

    private void Start()
    {
        //Set up skin
        selectedHatId = PlayerPrefs.GetInt("Hat", -1);
        selectedFaceId = PlayerPrefs.GetInt("Face", 0);
        selectedSkinId = PlayerPrefs.GetInt("Skin", 0);
        selectedInprintId = PlayerPrefs.GetInt("Inprint", 0);
        GameObject.Find("Player").GetComponent<PlayerAppearance>().ChangePlayerHat(selectedHatId);
        GameObject.Find("Player").GetComponent<PlayerAppearance>().ChangePlayerFace(selectedFaceId);
        GameObject.Find("Player").GetComponent<PlayerAppearance>().ChangePlayerSkin(selectedSkinId);
        GameObject.Find("Player").GetComponent<PlayerAppearance>().ChangePlayerInprint(selectedInprintId);

        //Set up UI
        GameObject.Find("HatSelection").transform.Find("Description").GetComponent<TMP_Text>().text =
            playerAppearanceData.GetHat(selectedHatId) == null ? "No Hat" : Regex.Replace(playerAppearanceData.GetHat(selectedHatId).name, "([a-z])([A-Z])", "$1 $2");
        GameObject.Find("FaceSelection").transform.Find("Description").GetComponent<TMP_Text>().text =
            Regex.Replace(playerAppearanceData.GetFace(selectedFaceId).name, "([a-z])([A-Z])", "$1 $2");
        GameObject.Find("SkinSelection").transform.Find("Description").GetComponent<TMP_Text>().text =
            Regex.Replace(playerAppearanceData.GetSkin(selectedSkinId).name, "([a-z])([A-Z])", "$1 $2");
        GameObject.Find("InprintSelection").transform.Find("Description").GetComponent<TMP_Text>().text =
            Regex.Replace(playerAppearanceData.GetInprint(selectedInprintId).name, "([a-z])([A-Z])", "$1 $2");
    }

    private void FixedUpdate()
    {
        if (UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject != null &&
            UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject.GetComponent<TMP_InputField>() != null)
        {
            // Currently player is typing something, so disable rotating functionality
            return;
        }

        if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow))
        {
            mockModel.transform.Rotate(Vector3.up * rotatingSpeed);
        }
        else if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow))
        {
            mockModel.transform.Rotate(Vector3.down * rotatingSpeed);
        }

        float maxAngle = 85f;
        float minAngle = 340f;

        if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow))
        {
            if(mainCamera.transform.rotation.eulerAngles.x >= minAngle - 10 || mainCamera.transform.rotation.eulerAngles.x <= maxAngle) //-10, because otherwise when we would be on a minimal point the camera would not move (same below)
                mainCamera.transform.Rotate(Vector3.right * rotatingSpeed);
        }
        else if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow))
        {
            if (mainCamera.transform.rotation.eulerAngles.x >= minAngle || mainCamera.transform.rotation.eulerAngles.x <= maxAngle + 10)
                mainCamera.transform.Rotate(Vector3.left * rotatingSpeed);
        }
    }

    public void ChangeHat(bool increment)
    {
        TMP_Text hatText = GameObject.Find("HatSelection").transform.Find("Description").GetComponent<TMP_Text>();
        if (increment)
        {
            selectedHatId++;
            if (selectedHatId >= playerAppearanceData.HatCount)
                selectedHatId = -1;
        }
        else
        {
            selectedHatId--;
            if (selectedHatId < -1)
                selectedHatId = playerAppearanceData.HatCount - 1;
        }

        hatText.text = playerAppearanceData.GetHat(selectedHatId) == null ? "No Hat" : Regex.Replace(playerAppearanceData.GetHat(selectedHatId).name, "([a-z])([A-Z])", "$1 $2"); //Make spaces between small and capital letters
        PlayerPrefs.SetInt("Hat", selectedHatId);
        GameObject.Find("Player").GetComponent<PlayerAppearance>().ChangePlayerHat(selectedHatId);
    }

    public void ChangeFace(bool increment)
    {
        TMP_Text faceText = GameObject.Find("FaceSelection").transform.Find("Description").GetComponent<TMP_Text>();
        if (increment)
        {
            selectedFaceId++;
            if (selectedFaceId >= playerAppearanceData.FaceCount)
                selectedFaceId = 0;
        }
        else
        {
            selectedFaceId--;
            if (selectedFaceId < 0)
                selectedFaceId = playerAppearanceData.FaceCount - 1;
        }
        faceText.text = Regex.Replace(playerAppearanceData.GetFace(selectedFaceId).name, "([a-z])([A-Z])", "$1 $2");
        PlayerPrefs.SetInt("Face", selectedFaceId);
        GameObject.Find("Player").GetComponent<PlayerAppearance>().ChangePlayerFace(selectedFaceId);
    }
    public void ChangeSkin(bool increment)
    {

        TMP_Text skinText = GameObject.Find("SkinSelection").transform.Find("Description").GetComponent<TMP_Text>();
        if (increment)
        {
            selectedSkinId++;
            if (selectedSkinId >= playerAppearanceData.SkinCount)
                selectedSkinId = 0;
        }
        else
        {
            selectedSkinId--;
            if (selectedSkinId < 0)
                selectedSkinId = playerAppearanceData.SkinCount - 1;
        }
        skinText.text = Regex.Replace(playerAppearanceData.GetSkin(selectedSkinId).name, "([a-z])([A-Z])", "$1 $2");
        PlayerPrefs.SetInt("Skin", selectedSkinId);
        GameObject.Find("Player").GetComponent<PlayerAppearance>().ChangePlayerSkin(selectedSkinId);
    }

    public void ChangeInprint(bool increment)
    {
        TMP_Text inprintText = GameObject.Find("InprintSelection").transform.Find("Description").GetComponent<TMP_Text>();
        if (increment)
        {
            selectedInprintId++;
            if (selectedInprintId >= playerAppearanceData.InprintCount)
                selectedInprintId = 0;
        }
        else
        {
            selectedInprintId--;
            if (selectedInprintId < 0)
                selectedInprintId = playerAppearanceData.InprintCount - 1;
        }
        inprintText.text = Regex.Replace(playerAppearanceData.GetInprint(selectedInprintId).name, "([a-z])([A-Z])", "$1 $2");
        PlayerPrefs.SetInt("Inprint", selectedInprintId);
        GameObject.Find("Player").GetComponent<PlayerAppearance>().ChangePlayerInprint(selectedInprintId);
    }
}
