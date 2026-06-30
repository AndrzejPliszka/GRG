using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine.SceneManagement;


public class MenuManager : NetworkBehaviour
{
    [HideInInspector] public bool isGamePaused = false;

    [SerializeField] GameObject pauseMenu;


    GameObject currentPauseMenuObject;
    [HideInInspector] public int amountOfDisplayedMenus = 1; //=1, as we always call ResumeGame() function when entering game which decreases this variable, and we dont wanna have it negative

    private void Awake()
    {
        amountOfDisplayedMenus = 1;
    }

    void SetUpPauseMenu()
    {
        currentPauseMenuObject = Instantiate(pauseMenu, GameManager.Instance.Canvas.transform);
        PauseMenuReferences pauseMenuReferences = currentPauseMenuObject.GetComponent<PauseMenuReferences>();

        if (IsClient || IsHost) //Code below is always executed in main scene (WARNING: IT IS EXECUTED AS MANY TIMES AS THERE ARE CLIENTS, but I don't know where to currently put it, as GameManager is not about menu managment)
        {

            pauseMenuReferences.resumeGameButton.onClick.RemoveAllListeners();
            pauseMenuReferences.exitServerButton.onClick.RemoveAllListeners();

            pauseMenuReferences.resumeGameButton.onClick.AddListener(() =>
            {
                ResumeGame(true);
            });
            pauseMenuReferences.exitServerButton.onClick.AddListener(QuitServer);
        }
        base.OnNetworkSpawn();
    }



    public void QuitServer()
    {
        NetworkManager.Singleton.Shutdown();
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        SceneManager.LoadScene("MenuScene", LoadSceneMode.Single); //change if different scene for menu
    }

    //set hidePauseMenu == true, when you want to close pause menu and not other menu
    public void ResumeGame(bool hidePauseMenu) //bool used, because this function is also called along side closing other menus
    {
        amountOfDisplayedMenus--;

        if (hidePauseMenu) //if hidePauseMenu == True, then this function was called to stop pausing and not close other menu, therefore we stop pausing to avoid bugs [Maybe rewrite code if more menus added]
        {
            Destroy(currentPauseMenuObject);
            isGamePaused = false;
        }

        if (amountOfDisplayedMenus <= 0)
        {
            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Locked;
            Destroy(currentPauseMenuObject);
        }

    }
    public void PauseGame()
    {
        amountOfDisplayedMenus++;
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
        isGamePaused = true;
        SetUpPauseMenu();
    }
}
