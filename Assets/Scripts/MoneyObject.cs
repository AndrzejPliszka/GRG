using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class MoneyObject : NetworkBehaviour
{
    [SerializeField] float startingMoneyAmount = 0f;
    [HideInInspector] public NetworkVariable<float> moneyAmount;

    public override void OnNetworkSpawn()
    {
        if(!IsServer) { return; }
        moneyAmount.Value = Mathf.Abs(Mathf.Round(startingMoneyAmount * 100f) / 100f);
    }
}
