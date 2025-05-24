using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.Netcode;
using UnityEngine;

public class Parliament : NetworkBehaviour
{
    [field: SerializeField] public NetworkVariable<int> TownId { get; private set; } = new();
    [SerializeField] TMP_Text lawListText;

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            foreach (var law in GameManager.Instance.TownData[TownId.Value].townLaw)
            {
                Debug.Log("sigm");
                UpdateLawTextOwnerRpc(law.Key, law.Value);
            }
        }
    }

    [Rpc(SendTo.Owner)]
    void UpdateLawTextOwnerRpc(GameManager.Laws law, bool isActive)
    {
        string[] lawLines = lawListText.text.Split('\n');
        int lawNumber = (int)law + 1; // +1 because first line is "Current laws: "
        if (lawLines.Length >= lawNumber)
            lawLines[lawNumber] = GetTextForLaw(law, isActive);
        lawListText.text = string.Join("\n", lawLines);
    }

    string GetTextForLaw(GameManager.Laws law, bool isInEffect)
    {
        Debug.Log("tekscik");
        return law switch
        {
            GameManager.Laws.AllowViolence => "Violence is " + (isInEffect ? "allowed." : "not allowed.") + "\n",
            GameManager.Laws.AllowPeasants => "Peasants are " + (isInEffect ? "allowed." : "not allowed.") + "\n",
            _ => "Text for this law wasn't programmed yet.\n",
        };
    }

}
