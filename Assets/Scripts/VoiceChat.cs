using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Vivox;
using Unity.Netcode;
using System.Threading.Tasks;
using UnityEngine.UI;
using System;

public class VoiceChat : NetworkBehaviour
{
    bool isInChannel = false;
    async Task InitializeAsync()
    {
        await UnityServices.InitializeAsync();
        await AuthenticationService.Instance.SignInAnonymouslyAsync();
        await VivoxService.Instance.InitializeAsync();
    }
    async Task LoginToVivoxAsync()
    {
        LoginOptions options = new LoginOptions();
        options.DisplayName = "Sigma";
        options.EnableTTS = true;
        await VivoxService.Instance.LoginAsync(options);
    }
    async Task JoinPositionalChannelAsync()
    {
        string channelToJoin = "Lobby";
        Channel3DProperties channelProperties = new Channel3DProperties(30, 10, 1.0f, AudioFadeModel.InverseByDistance);
        await VivoxService.Instance.JoinPositionalChannelAsync(channelToJoin, ChatCapability.TextAndAudio, null);
    }


    public async Task LogoutFromVivox()
    {
        isInChannel = false;
        await VivoxService.Instance.LogoutAsync();
        AuthenticationService.Instance.SignOut();
    }

    public void UpdateVivoxPosition()
    {
        if (isInChannel)
            VivoxService.Instance.Set3DPosition(gameObject, "Lobby");
    }

    public async Task StartVivox()
    {
        await InitializeAsync();
        await LoginToVivoxAsync();
        await JoinPositionalChannelAsync();
        isInChannel = true;
        Debug.Log("Successfully Joined Vivox Channel");
    }

}
