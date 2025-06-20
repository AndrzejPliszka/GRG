using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine.SceneManagement;


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

    public int amountOfDisplayedMenus; //used, because there can be multiple menus displayed at once
    private void Awake()
    {
        mainMenu = GameObject.Find("MainMenu");
        pauseMenu = GameObject.Find("PauseMenu");

        serverButton = GameObject.Find("ServerButton").GetComponent<Button>();
        clientButton = GameObject.Find("ClientButton").GetComponent<Button>();
        hostButton = GameObject.Find("HostButton").GetComponent<Button>();
        ipInputField = GameObject.Find("IPInputField").GetComponent<TMP_InputField>();
        nicknameField = GameObject.Find("NicknameField").GetComponent<TMP_InputField>();

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
    }
    void Start()
    {
        amountOfDisplayedMenus = 1; //there is pause menu visible at start, so we can set up refrences to it
        //removing listeners, so methods are never called twice (maybe there is cleaner solution)
        serverButton.onClick.RemoveAllListeners();
        clientButton.onClick.RemoveAllListeners();
        hostButton.onClick.RemoveAllListeners();
        ipInputField.onValueChanged.RemoveAllListeners();
        nicknameField.onValueChanged.RemoveAllListeners();
        serverButton.onClick.AddListener(() => {
            HideStartingMenu();
            NetworkManager.Singleton.StartServer();
            NetworkManager.Singleton.SceneManager.LoadScene("MainScene", LoadSceneMode.Single);
        });
        clientButton.onClick.AddListener(() => {

            HideStartingMenu();
            NetworkManager.Singleton.StartClient();
            //SceneManager.LoadScene("MainScene");
        });
        hostButton.onClick.AddListener(() => {
            HideStartingMenu();
            NetworkManager.Singleton.StartHost();
            NetworkManager.Singleton.SceneManager.LoadScene("MainScene", LoadSceneMode.Single);
        });
        if(PlayerPrefs.GetString("ServerIP") != null) //changing unity transport if there is server IP saved in PlayerPrefs
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
        if (IsClient || IsHost) //Code below is always executed in main scene (WARNING: IT IS EXECUTED AS MANY TIMES AS THERE ARE CLIENTS, but I don't know where to currently put it, as GameManager is not about menu managment)
        {
            resumeGameButton = GameObject.Find("ResumeGameButton").GetComponent<Button>();
            exitServerButton = GameObject.Find("ExitServerButton").GetComponent<Button>();

            HideStartingMenu();

            resumeGameButton.onClick.RemoveAllListeners();
            exitServerButton.onClick.RemoveAllListeners();

            resumeGameButton.onClick.AddListener(() =>
            {
                ResumeGame(true);
            });
            exitServerButton.onClick.AddListener(QuitServer);
            pauseMenu.SetActive(false);//hide pause menu before joining server
        }
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
        SceneManager.LoadScene("MenuScene"); //change if different scene for menu
    }
    //set hidePauseMenu == true, when you want to close pause menu and not other menu
    public void ResumeGame(bool hidePauseMenu) //bool used, because this function is also called along side closing other menus
    {
        amountOfDisplayedMenus--;

        pauseMenu.SetActive(!hidePauseMenu);
        if (hidePauseMenu) //if hidePauseMenu == True, then this function was called to stop pausing and not close other menu, therefore we stop pausing to avoid bugs [Maybe rewrite code if more menus added]
            isGamePaused = false;

        if (amountOfDisplayedMenus <= 0)
        {
            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Locked;
            pauseMenu.SetActive(false);
        }

    }
    public void PauseGame()
    {
        amountOfDisplayedMenus++;
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
        isGamePaused = true;
        pauseMenu.SetActive(true);
    }
}
