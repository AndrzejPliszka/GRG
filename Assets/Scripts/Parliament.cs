using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using TMPro;
using Unity.Netcode;
using UnityEngine;

public class Parliament : NetworkBehaviour
{
    [field: SerializeField] public NetworkVariable<int> TownId { get; private set; } = new();
    [SerializeField] TMP_Text lawListText;
    const int cooldownBetweenVotes = 5;
    const int votingTime = 5;
    bool isVotingInProgress = false;
    int currentCooldown;
    //Server only
    GameManager.Law currentlyVotedLaw;
    readonly List<GameManager.Law> lawQueue = new();
    readonly Dictionary<bool, List<ulong>> playerVotes = new()
    {
        { true, new List<ulong>() },  // Votes for "Yes"
        { false, new List<ulong>() }  // Votes for "No"
    };
    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            currentCooldown = cooldownBetweenVotes;
            StartCoroutine(VotingCoroutine());
            GameManager.Instance.TownData[TownId.Value].OnLawAddedToQueue += AddLawToQueue;
            GameManager.Instance.TownData[TownId.Value].OnPlayerVote += CountPlayerVote;
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

    void CountPlayerVote(ulong playerId, bool votedYes)
    {
        if (!IsServer) throw new System.Exception("Voting is only accessible on server");

        if (!isVotingInProgress) //There is no voting currently
            return;
        // Remove vote if player already voted in opposite way
        if (playerVotes[!votedYes].Contains(playerId))
        {
            playerVotes[!votedYes].Remove(playerId);
        }
        //Add vote
        if (!playerVotes[votedYes].Contains(playerId))
        {
            playerVotes[votedYes].Add(playerId);
            GameManager.Instance.TownData[TownId.Value].OnVoteCountChange.Invoke(playerVotes[false].Count, playerVotes[true].Count);
        }
    }
    void ResetVotes()
    {
        if (!IsServer) throw new System.Exception("Resetting votes is only accessible on server");
        playerVotes[true].Clear();
        playerVotes[false].Clear();
        GameManager.Instance.TownData[TownId.Value].OnVoteCountChange.Invoke(0, 0);
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
                        GameManager.Instance.TownData[TownId.Value].OnVotingStateChange.Invoke(votingTime, false, currentlyVotedLaw);
                        continue;
                    }
                    Debug.Log("Voting started!");
                    currentCooldown = votingTime;
                    isVotingInProgress = true;
                    currentlyVotedLaw = lawQueue[0];
                    lawQueue.RemoveAt(0);
                    GameManager.Instance.TownData[TownId.Value].OnVotingStateChange.Invoke(votingTime, true, currentlyVotedLaw);
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
                    if(playerVotes[true].Count > playerVotes[false].Count)
                        GameManager.Instance.TownData[TownId.Value].townLaw[currentlyVotedLaw] = true;
                    else if (playerVotes[false].Count > playerVotes[true].Count)
                        GameManager.Instance.TownData[TownId.Value].townLaw[currentlyVotedLaw] = false;

                    GameManager.Instance.TownData[TownId.Value].OnVotingStateChange.Invoke(cooldownBetweenVotes, false, currentlyVotedLaw); //Setting law if it is needed
                    ResetVotes();
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
