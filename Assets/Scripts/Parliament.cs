using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.Netcode;
using UnityEngine;

public class Parliament : NetworkBehaviour
{
    [field: SerializeField] public NetworkVariable<int> TownId { get; private set; } = new();
    [SerializeField] TMP_Text lawListText;
    [SerializeField] int cooldownBetweenVotes = 5;
    [SerializeField] int votingTime = 5;
    bool isVotingInProgress = false;
    int currentCooldown;
    //Server only
    GameManager.Law currentlyVotedLaw;
    readonly List<GameManager.Law> lawQueue = new();
    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            currentCooldown = cooldownBetweenVotes;
            StartCoroutine(VotingCoroutine());
            GameManager.Instance.TownData[TownId.Value].OnLawAddedToQueue += AddLawToQueue;
            foreach (var law in GameManager.Instance.TownData[TownId.Value].townLaw)
            {
                UpdateLawTextOwnerRpc(law.Key, law.Value);
            }
        }
    }

    [Rpc(SendTo.Owner)]
    void UpdateLawTextOwnerRpc(GameManager.Law law, bool isActive)
    {
        string[] lawLines = lawListText.text.Split('\n');
        int lawNumber = (int)law + 1; // +1 because first line is "Current laws: "
        if (lawLines.Length >= lawNumber)
            lawLines[lawNumber] = GetTextForLaw(law, isActive);
        lawListText.text = string.Join("\n", lawLines);
    }

    public void AddLawToQueue(GameManager.Law law)
    {
        if(!IsServer) throw new System.Exception("Queue is only accessible on server");
        lawQueue.Add(law);
    }

    IEnumerator VotingCoroutine()
    {
        while (true) {
            if(isVotingInProgress == false)
            {
                if(currentCooldown == 0)
                {
                    if(lawQueue.Count == 0)
                    {
                        currentCooldown = cooldownBetweenVotes;
                        continue;
                    }
                    Debug.Log("Voting started!");
                    currentCooldown = votingTime;
                    isVotingInProgress = true;
                    currentlyVotedLaw = lawQueue[0];
                    lawQueue.RemoveAt(0);
                    GameManager.Instance.TownData[TownId.Value].OnVotingStart.Invoke(votingTime);
                    //More logic to handling start of voting
                }
                else
                {
                    currentCooldown--;
                    yield return new WaitForSeconds(1f);
                }
            }
            else
            {
                if (currentCooldown == 0)
                {
                    Debug.Log("Voting ended!");
                    currentCooldown = cooldownBetweenVotes;
                    isVotingInProgress = false;
                    //More logic to handling end of voting
                }
                else
                {
                    currentCooldown--;
                    yield return new WaitForSeconds(1f);
                }
            }
        }
    }

    public static string GetTextForLaw(GameManager.Law law, bool isInEffect)
    {
        return law switch
        {
            GameManager.Law.AllowViolence => "Violence is " + (isInEffect ? "allowed." : "not allowed.") + "\n",
            GameManager.Law.AllowPeasants => "Peasants are " + (isInEffect ? "allowed." : "not allowed.") + "\n",
            _ => "Text for this law wasn't programmed yet.\n",
        };
    }

    public static GameManager.Law GetLawFromString(string law)
    {
        law = law.Trim();
        return law switch
        {
            "Violence is allowed." => GameManager.Law.AllowViolence,
            "Violence is not allowed." => GameManager.Law.AllowViolence,
            "Peasants are allowed." => GameManager.Law.AllowPeasants,
            "Peasants are not allowed." => GameManager.Law.AllowPeasants,
            _ => throw new System.Exception("Law not found: " + law)
        };
    }
}
