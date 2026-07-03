using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine.SceneManagement;


public class MainMenu : MonoBehaviour
{
    public bool isGamePaused = false;
    [SerializeField] Button serverButton;
    [SerializeField] Button clientButton;
    [SerializeField] Button hostButton;
    [SerializeField] TMP_InputField ipInputField;
    [SerializeField] TMP_InputField nicknameField;

    public void Start()
    {
        string nickname = PlayerPrefs.GetString("Nickname");
        if (nickname != null)
        {
            nicknameField.text = nickname;
        }

        string serverIP = PlayerPrefs.GetString("ServerIP");
        if (serverIP != null)
        {
            ipInputField.text = serverIP;
        }

        serverButton.onClick.RemoveAllListeners();
        clientButton.onClick.RemoveAllListeners();
        hostButton.onClick.RemoveAllListeners();
        ipInputField.onValueChanged.RemoveAllListeners();
        nicknameField.onValueChanged.RemoveAllListeners();
        serverButton.onClick.AddListener(() => {
            NetworkManager.Singleton.StartServer();
            NetworkManager.Singleton.SceneManager.LoadScene("MainScene", LoadSceneMode.Single);
        });
        clientButton.onClick.AddListener(() => {

            NetworkManager.Singleton.StartClient();
            //SceneManager.LoadScene("MainScene");
        });
        hostButton.onClick.AddListener(() => {
            NetworkManager.Singleton.StartHost();
            NetworkManager.Singleton.SceneManager.LoadScene("MainScene", LoadSceneMode.Single);
        });
        if (PlayerPrefs.GetString("ServerIP") != null) //changing unity transport if there is server IP saved in PlayerPrefs
        {
            NetworkManager.Singleton.GetComponent<UnityTransport>().SetConnectionData(
                PlayerPrefs.GetString("ServerIP"),
                (ushort)7777,
                "0.0.0.0");
        }
        ipInputField.onValueChanged.AddListener((string inputValue) =>
        {
            PlayerPrefs.SetString("ServerIP", inputValue);
            NetworkManager.Singleton.GetComponent<UnityTransport>().SetConnectionData(
                inputValue,
                (ushort)7777,
                "0.0.0.0"
        );
        });
        nicknameField.onValueChanged.AddListener((string inputValue) => {
            PlayerPrefs.SetString("Nickname", inputValue);
        });
    }
}
