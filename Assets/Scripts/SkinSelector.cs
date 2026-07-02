using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

public class SkinSelector : MonoBehaviour
{
    [SerializeField] GameObject mockModel;
    [SerializeField] GameObject mainCamera;
    [SerializeField] PlayerAppearanceData playerAppearanceData;

    [SerializeField] TMP_Text hatSelectionDescriptionText;
    [SerializeField] TMP_Text faceSelectionDescriptionText;
    [SerializeField] TMP_Text skinSelectionDescriptionText;
    [SerializeField] TMP_Text inprintSelectionDescriptionText;
    readonly float rotatingSpeed = 3f;

    int selectedHatId = -1; 
    int selectedFaceId = 0;
    int selectedSkinId = 0;
    int selectedInprintId = 0;

    InputAction skinSelectorInput;

    private void Start()
    {
        skinSelectorInput = InputSystem.actions.FindAction("SkinSelector", true);

        //Set up skin
        selectedHatId = PlayerPrefs.GetInt("Hat", -1);
        selectedFaceId = PlayerPrefs.GetInt("Face", 0);
        selectedSkinId = PlayerPrefs.GetInt("Skin", 0);
        selectedInprintId = PlayerPrefs.GetInt("Inprint", 0);
        mockModel.GetComponent<PlayerAppearance>().ChangePlayerHat(selectedHatId);
        mockModel.GetComponent<PlayerAppearance>().ChangePlayerFace(selectedFaceId);
        mockModel.GetComponent<PlayerAppearance>().ChangePlayerSkin(selectedSkinId);
        mockModel.GetComponent<PlayerAppearance>().ChangePlayerInprint(selectedInprintId);

        //Set up UI
        hatSelectionDescriptionText.text = playerAppearanceData.GetHat(selectedHatId) == null ? "No Hat" : Regex.Replace(playerAppearanceData.GetHat(selectedHatId).name, "([a-z])([A-Z])", "$1 $2");
        faceSelectionDescriptionText.text = Regex.Replace(playerAppearanceData.GetFace(selectedFaceId).name, "([a-z])([A-Z])", "$1 $2");
        skinSelectionDescriptionText.text = Regex.Replace(playerAppearanceData.GetSkin(selectedSkinId).name, "([a-z])([A-Z])", "$1 $2");
        inprintSelectionDescriptionText.text = Regex.Replace(playerAppearanceData.GetInprint(selectedInprintId).name, "([a-z])([A-Z])", "$1 $2");
    }

    private void FixedUpdate()
    {
        if (UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject != null &&
            UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject.GetComponent<TMP_InputField>() != null)
        {
            // Currently player is typing something, so disable rotating functionality
            return;
        }

        Vector2 currentInput = skinSelectorInput.ReadValue<Vector2>();

        if (currentInput.x < 0)
        {
            mockModel.transform.Rotate(Vector3.up * rotatingSpeed);
        }
        else if (currentInput.x > 0)
        {
            mockModel.transform.Rotate(Vector3.down * rotatingSpeed);
        }

        float maxAngle = 85f;
        float minAngle = 340f;

        if (currentInput.y > 0)
        {
            if(mainCamera.transform.rotation.eulerAngles.x >= minAngle - 10 || mainCamera.transform.rotation.eulerAngles.x <= maxAngle) //-10, because otherwise when we would be on a minimal point the camera would not move (same below)
                mainCamera.transform.Rotate(Vector3.right * rotatingSpeed);
        }
        else if (currentInput.y < 0)
        {
            if (mainCamera.transform.rotation.eulerAngles.x >= minAngle || mainCamera.transform.rotation.eulerAngles.x <= maxAngle + 10)
                mainCamera.transform.Rotate(Vector3.left * rotatingSpeed);
        }
    }

    public void ChangeHat(bool increment)
    {
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
        //Make spaces between small and capital letters
        hatSelectionDescriptionText.text = playerAppearanceData.GetHat(selectedHatId) == null ? "No Hat" : Regex.Replace(playerAppearanceData.GetHat(selectedHatId).name, "([a-z])([A-Z])", "$1 $2");
        PlayerPrefs.SetInt("Hat", selectedHatId);
        mockModel.GetComponent<PlayerAppearance>().ChangePlayerHat(selectedHatId);
    }

    public void ChangeFace(bool increment)
    {
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
        faceSelectionDescriptionText.text = Regex.Replace(playerAppearanceData.GetFace(selectedFaceId).name, "([a-z])([A-Z])", "$1 $2");
        PlayerPrefs.SetInt("Face", selectedFaceId);
        mockModel.GetComponent<PlayerAppearance>().ChangePlayerFace(selectedFaceId);
    }
    public void ChangeSkin(bool increment)
    {
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
        skinSelectionDescriptionText.text = Regex.Replace(playerAppearanceData.GetSkin(selectedSkinId).name, "([a-z])([A-Z])", "$1 $2");
        PlayerPrefs.SetInt("Skin", selectedSkinId);
        mockModel.GetComponent<PlayerAppearance>().ChangePlayerSkin(selectedSkinId);
    }

    public void ChangeInprint(bool increment)
    {
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
        inprintSelectionDescriptionText.text = Regex.Replace(playerAppearanceData.GetInprint(selectedInprintId).name, "([a-z])([A-Z])", "$1 $2");
        PlayerPrefs.SetInt("Inprint", selectedInprintId);
        mockModel.GetComponent<PlayerAppearance>().ChangePlayerInprint(selectedInprintId);
    }
}
