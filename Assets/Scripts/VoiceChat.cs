using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Vivox;
using UnityEngine;
using UnityEngine.InputSystem;

public class VoiceChat : NetworkBehaviour
{
    public bool IsMuted { get; private set; } = true;

    InputAction voiceChatInput;

    private void Start()
    {
        if(!IsOwner) return;
        voiceChatInput = InputSystem.actions.FindAction("VoiceChat", true);
    }


    private void Update()
    {
        if(!IsOwner || !VivoxManager.Instance.IsInChannel) return;

        if (voiceChatInput.IsPressed())
            UnmutePlayer();
        else
            MutePlayer();
    }

    public void UpdateVivoxPosition()
    {
        if (!IsClient) { return; };

        if (VivoxManager.Instance.IsInChannel)
            VivoxService.Instance.Set3DPosition(gameObject, VivoxManager.Instance.CurrentChannelName);
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
