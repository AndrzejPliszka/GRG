using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine.SceneManagement;
//this is only because of logout
using Unity.Services.Vivox;
using Unity.Services.Authentication;
using Unity.Collections;


public class Menu : NetworkBehaviour
{
    public bool isGamePaused = false;

    GameObject mainMenu;
    GameObject pauseMenu;

    Button serverButton;
    Button clientButton;
    Button hostButton;
    TMP_InputField ipInputField;
    TMP_InputField nicknameField;

    Button resumeGameButton;
    Button exitServerButton;

    public FixedString32Bytes Nickname { get; private set; }
    private void Awake()
    {
        mainMenu = GameObject.Find("MainMenu");
        pauseMenu = GameObject.Find("PauseMenu");

        serverButton = GameObject.Find("ServerButton").GetComponent<Button>();
        clientButton = GameObject.Find("ClientButton").GetComponent<Button>();
        hostButton = GameObject.Find("HostButton").GetComponent<Button>();
        ipInputField = GameObject.Find("IPInputField").GetComponent<TMP_InputField>();
        nicknameField = GameObject.Find("NicknameField").GetComponent<TMP_InputField>();

        resumeGameButton = GameObject.Find("ResumeGameButton").GetComponent<Button>();
        exitServerButton = GameObject.Find("ExitServerButton").GetComponent<Button>();
        exitServerButton = GameObject.Find("ExitServerButton").GetComponent<Button>();
    }
    void Start()
    {
        //removing listeners, so methods are never called twice (maybe there is cleaner solution)
        serverButton.onClick.RemoveAllListeners();
        clientButton.onClick.RemoveAllListeners();
        hostButton.onClick.RemoveAllListeners();
        ipInputField.onValueChanged.RemoveAllListeners();
        nicknameField.onValueChanged.RemoveAllListeners();
        resumeGameButton.onClick.RemoveAllListeners();
        exitServerButton.onClick.RemoveAllListeners();

        serverButton.onClick.AddListener(() => {
            HideStartingMenu();
            NetworkManager.Singleton.StartServer();
        });
        clientButton.onClick.AddListener(() => {
            HideStartingMenu();
            NetworkManager.Singleton.StartClient();
        });
        hostButton.onClick.AddListener(() => {
            HideStartingMenu();
            NetworkManager.Singleton.StartHost();
        });
        ipInputField.onValueChanged.AddListener((string inputValue) =>
        {
            NetworkManager.Singleton.GetComponent<UnityTransport>().SetConnectionData(
                inputValue,
                (ushort)7777,
                "0.0.0.0"
        );
        });
        nicknameField.onValueChanged.AddListener((string inputValue) => {
            Nickname = inputValue;
        });

        resumeGameButton.onClick.AddListener(() =>
        {
            ResumeGame();
        });
        exitServerButton.onClick.AddListener(QuitServer);
        pauseMenu.SetActive(false);
    }


    public void HideStartingMenu()
    {
        mainMenu.SetActive(false);
    }

    public void QuitServer()
    {
        NetworkManager.Singleton.Shutdown();
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name); //change if different scene for menu
    }
    public void ResumeGame()
    {
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;
        isGamePaused = false;
        pauseMenu.SetActive(false);

    }
    public void PauseGame()
    {
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
        isGamePaused = true;
        pauseMenu.SetActive(true);
    }
}
