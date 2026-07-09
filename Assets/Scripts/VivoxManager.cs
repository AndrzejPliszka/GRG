using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Vivox;
using UnityEngine;
using UnityEngine.InputSystem;

public class VivoxManager : MonoBehaviour
{
    public static VivoxManager Instance { get; private set; }
    public bool IsLoggedIn { get; private set; } = false;
    public bool IsInChannel { get; private set; } = false;

    public event Action OnLoggedIn;

    public string CurrentChannelName { get; private set; } = "";

    private void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        OnLoggedIn += async () =>
        {
            if (NetworkManager.Singleton.IsConnectedClient)
            {
                await JoinChannelForCurrentServer();
            }
        };
    }

    private async void Start()
    {
        if (!IsLoggedIn)
        {
            await InitializeAsync();
            await LoginToVivoxAsync();
            IsLoggedIn = true;
            OnLoggedIn.Invoke();
            StartCoroutine(WaitForNetworkManagerAndSubscribe());
            Debug.Log("[Vivox]: Successfully logged in!");
        }
    }

    private async void OnApplicationQuit()
    {
        await LogoutFromVivox();
        Debug.Log("[Vivox]: Logged out.");
    }
    /// <summary>
    /// Waits until player joins the server (NetworkManager is initialized) and subscribes to its connection and disconnection callbacks, to manage voice chat.
    /// </summary>
    /// <returns>An enumerator for coroutine execution.</returns>
    private IEnumerator WaitForNetworkManagerAndSubscribe()
    {
        while (NetworkManager.Singleton == null)
            yield return null;

        NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
        NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
    }
    /// <summary>
    /// Method for managing voice chat when player joins the server, calls JoinChannelForCurrentServer() (note that player may not be logged in during this time!)
    /// </summary>
    /// <param name="clientId">Used by callback; This ID needs to be the same as NetworkManager.Singleton.LocalClientId for function to run.</param>
    private async void OnClientConnected(ulong clientId)
    {
        if (clientId != NetworkManager.Singleton.LocalClientId) return;

        if (IsInChannel)
            await LeaveChannel();

        await JoinChannelForCurrentServer();
    }
    /// <summary>
    /// Method for managing voice chat when player leaves the server
    /// </summary>
    /// <param name="clientId">Used by callback; This ID needs to be the same as NetworkManager.Singleton.LocalClientId for function to run.</param>
    private async void OnClientDisconnected(ulong clientId)
    {
        if (clientId != NetworkManager.Singleton.LocalClientId) return;
        await LeaveChannel();
    }

    /// <summary>
    /// Joins the positional channel for the current server if the user is logged in and not already in a channel.
    /// </summary>
    /// <returns>A Task representing the asynchronous operation.</returns>
    async Task JoinChannelForCurrentServer()
    {
        if (!IsLoggedIn)
        {
            Debug.LogError("Vivox is not logged in yet!");
            return;
        }

        if (IsInChannel)
            return;

        CurrentChannelName = NetworkManager.Singleton.GetComponent<UnityTransport>()
            .ConnectionData.Address.ToString().Replace(':', '_'); 

        Channel3DProperties props = new(30, 10, 1.0f, AudioFadeModel.InverseByDistance);
        await VivoxService.Instance.JoinPositionalChannelAsync(CurrentChannelName, ChatCapability.TextAndAudio, props);
        IsInChannel = true;

        Debug.Log($"[Vivox]: Joined channel {CurrentChannelName}!");
    }

    /// <summary>
    /// Leave current channel in which there is user.
    /// </summary>
    /// <returns>A Task representing the asynchronous operation.</returns>
    async Task LeaveChannel()
    {
        if (IsInChannel)
        {
            IsInChannel = false;
            await VivoxService.Instance.LeaveChannelAsync(CurrentChannelName);
            Debug.Log($"[Vivox]: Left channel {CurrentChannelName}!");
            CurrentChannelName = "";
        }
    }
    /// <summary>
    /// Initialize all services needed for Vivox to work.
    /// </summary>
    /// <returns>A Task representing the asynchronous operation.</returns>
    private async Task InitializeAsync()
    {
        await UnityServices.InitializeAsync();
        if (AuthenticationService.Instance.IsSignedIn)
            AuthenticationService.Instance.SignOut();
        AuthenticationService.Instance.ClearSessionToken();
        if (!AuthenticationService.Instance.IsSignedIn)
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
        await VivoxService.Instance.InitializeAsync();
    }
    /// <summary>
    /// Login to vivox with selected options.
    /// </summary>
    /// <returns>A Task representing the asynchronous operation.</returns>
    private async Task LoginToVivoxAsync()
    {
        LoginOptions options = new()
        {
            EnableTTS = true
        };
        await VivoxService.Instance.LoginAsync(options);
    }
    /// <summary>
    /// Logout from Vivox and other services needed for it to work.
    /// </summary>
    /// <returns>A Task representing the asynchronous operation.</returns>
    private async Task LogoutFromVivox()
    {
        if (IsInChannel)
        {
            await VivoxService.Instance.LeaveChannelAsync(CurrentChannelName);
            IsInChannel = false;
        }

        if (VivoxService.Instance.IsLoggedIn)
            await VivoxService.Instance.LogoutAsync();

        if (AuthenticationService.Instance.IsSignedIn)
            AuthenticationService.Instance.SignOut();

        IsLoggedIn = false;
    }
}
