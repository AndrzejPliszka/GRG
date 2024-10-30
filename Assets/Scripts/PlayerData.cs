using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

public class PlayerData : NetworkBehaviour
{
    NetworkVariable<FixedString32Bytes> nickname = new();
    Menu menuScript;
    private void Start()
    {
        if(!IsOwner)
        ChangeNicknameServerRpc(GameObject.Find("Canvas").GetComponent<Menu>().Nickname);
    }
    //[WARNING !] This is unsafe, because it makes that nickname is de facto Owner controlled and can be changed any time by client by calling this method
    //It is that way, because otherwise nickname would need to be send to server on player join and server would need to assign it only one time and I don't really know how to do this and this will be assigned by client anyways, so I don't care
    [Rpc(SendTo.Server)]
    private void ChangeNicknameServerRpc(FixedString32Bytes setNickname)
    {
        nickname.Value = setNickname;
    }
}
