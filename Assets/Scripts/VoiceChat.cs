using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Vivox;
using UnityEngine;

public class VoiceChat : NetworkBehaviour
{
    bool isInChannel = false;
    public bool IsMuted { get; private set; } = true;

    private async void Start()
    {
        if(!IsOwner) return;
        await StartVivox();
    }
    public async override void OnNetworkDespawn()
    {
        if (!IsOwner) return;
        await LogoutFromVivox();
    }
    private void Update()
    {
        if(!IsOwner || !isInChannel) return;
        if (Input.GetKeyUp(KeyCode.Q))
            MutePlayer();
        else if(Input.GetKeyDown(KeyCode.Q))
            UnmutePlayer();
    }
    async Task InitializeAsync()
    {
        await UnityServices.InitializeAsync();
        await AuthenticationService.Instance.SignInAnonymouslyAsync();
        await VivoxService.Instance.InitializeAsync();
    }
    async Task LoginToVivoxAsync()
    {
        LoginOptions options = new()
        {
            EnableTTS = true
        };
        await VivoxService.Instance.LoginAsync(options);
    }
    async Task JoinPositionalChannelAsync()
    {
        string channelToJoin = NetworkManager.Singleton.GetComponent<UnityTransport>().ConnectionData.ToString(); //setting this, so there are different voice chat channels for different servers
        Channel3DProperties channelProperties = new(30, 10, 1.0f, AudioFadeModel.InverseByDistance);
        await VivoxService.Instance.JoinPositionalChannelAsync(channelToJoin, ChatCapability.TextAndAudio, channelProperties);
    }


    public async Task LogoutFromVivox()
    {
        if (isInChannel)
        {
            isInChannel = false;

            await VivoxService.Instance.LogoutAsync();
            AuthenticationService.Instance.SignOut();
        }

        Debug.Log("Successfully logout from Vivox");
    }

    public void UpdateVivoxPosition()
    {
        if (!IsClient) { return; };
        if (isInChannel)
        {
            VivoxService.Instance.Set3DPosition(gameObject, NetworkManager.Singleton.GetComponent<UnityTransport>().ConnectionData.ToString());
        }
    }

    public async Task StartVivox()
    {
        if (!IsClient) { return; }; 
        if (!isInChannel)
        {
            await InitializeAsync();
            await LoginToVivoxAsync();
            await JoinPositionalChannelAsync();
            isInChannel = true;
            IsMuted = false;
            MutePlayer();
            Debug.Log("Successfully Joined Vivox Channel");
        }
    }

    public void MutePlayer()
    {
        IsMuted = true;
        VivoxService.Instance.MuteInputDevice();
    }

    public void UnmutePlayer()
    {
        IsMuted = false;
        VivoxService.Instance.UnmuteInputDevice();
    }
}
