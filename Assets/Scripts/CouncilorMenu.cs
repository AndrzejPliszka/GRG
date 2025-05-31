using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public class CouncilorMenu : NetworkBehaviour
{
    [SerializeField] GameObject lawMenuPrefab;
    GameObject councilorMenu;
    TMP_Dropdown chooseLawDropdown;
    Button submitNewLawButton;
    GameObject lawList;
    TMP_Text mainText;
    TMP_Text cooldownText;
    PlayerData playerData;
    Button voteYesButton;
    Button voteNoButton;
    TMP_Text yesVotesText;
    TMP_Text noVotesText;
    float topOffset = -100; //used in displaying law queue
    bool menuJustOpened = false; //used as closing and opening menu is under the same button
    public override void OnNetworkSpawn()
    {
        playerData = GetComponent<PlayerData>();
        if (IsServer)
        {
            playerData.TownId.OnValueChanged += SetUpServerSideListeners;
        }
        if(!IsOwner) return;
        councilorMenu = GameObject.Find("CouncilorMenu");
        chooseLawDropdown = GameObject.Find("LawChooseDropdown").GetComponent<TMP_Dropdown>();
        submitNewLawButton = GameObject.Find("ProposeLawChangeButton").GetComponent<Button>();
        lawList = GameObject.Find("VotingQueueContainer");
        mainText = GameObject.Find("MainLawText").GetComponent<TMP_Text>();
        cooldownText = GameObject.Find("CooldownText").GetComponent<TMP_Text>();
        voteYesButton = GameObject.Find("VoteYesButton").GetComponent<Button>();
        voteNoButton = GameObject.Find("VoteNoButton").GetComponent<Button>();
        yesVotesText = GameObject.Find("YesVotesText").GetComponent<TMP_Text>();
        noVotesText = GameObject.Find("NoVotesText").GetComponent<TMP_Text>();

        SetUpChooseLawDropdown();
        playerData.TownId.OnValueChanged += SetUpButtons;
        councilorMenu.SetActive(false);
    }
    private void Update()
    {
        if (!IsOwner) return;
        //Used to ignore initial E press when menu is opened
        if (menuJustOpened)
        {
            if (!Input.GetKey(KeyCode.E))
                menuJustOpened = false;
            return;
        }


        if (councilorMenu.activeSelf && Input.GetKeyDown(KeyCode.E))
        {
            councilorMenu.SetActive(false);
            GetComponent<Movement>().blockRotation = false;
            GetComponent<ObjectInteraction>().canInteract = true;
            GameObject.Find("Canvas").GetComponent<Menu>().ResumeGame(false);
        }
    }
    private void SetUpServerSideListeners(int oldTownId, int newTownId)
    {
        if (!IsServer) { throw new System.Exception("This wont work on client!"); }
        if (oldTownId >= 0 && oldTownId < GameManager.Instance.TownData.Count)
        {
            GameManager.Instance.TownData[oldTownId].OnLawAddedToQueue -= AddLawToQueueMenuOwnerRpc;
            GameManager.Instance.TownData[oldTownId].OnVotingStateChange -= SetUpVotingOwnerRpc;
            GameManager.Instance.TownData[oldTownId].OnVoteCountChange -= DisplayNewVoteOwnerRpc;
        }
        if (newTownId >= 0 && newTownId < GameManager.Instance.TownData.Count)
        {
            GameManager.Instance.TownData[playerData.TownId.Value].OnLawAddedToQueue += AddLawToQueueMenuOwnerRpc;
            GameManager.Instance.TownData[playerData.TownId.Value].OnVotingStateChange += SetUpVotingOwnerRpc;
            GameManager.Instance.TownData[playerData.TownId.Value].OnVoteCountChange += DisplayNewVoteOwnerRpc;
        }
    }
    void SetUpButtons(int oldTownId, int newTownId)
    {
        //Clear old listeners
        if (oldTownId >= 0 && oldTownId < GameManager.Instance.TownData.Count)
        {
            submitNewLawButton.onClick.RemoveAllListeners();
            voteYesButton.onClick.RemoveAllListeners();
            voteNoButton.onClick.RemoveAllListeners();
        }
        //Set up new ones
        if (newTownId >= 0 && newTownId < GameManager.Instance.TownData.Count)
        {
            voteYesButton.onClick.AddListener(() =>
            {
                SendVoteServerRpc(true);
            });
            voteNoButton.onClick.AddListener(() =>
            {
                SendVoteServerRpc(false);
            });

            submitNewLawButton.onClick.AddListener(() =>
            {
                GameManager.Law law = Parliament.GetLawFromString(chooseLawDropdown.options[chooseLawDropdown.value].text);
                SendLawServerRpc(law);
            });
        }
    }
    [Rpc(SendTo.Server)]
    void SendVoteServerRpc(bool vote)
    {
        if(playerData.Role.Value != PlayerData.PlayerRole.Councilor && playerData.Role.Value != PlayerData.PlayerRole.Leader)
        {
            Debug.LogError("Tried to vote with non-councilor role!");
            return;
        }
        GameManager.Instance.TownData[playerData.TownId.Value].OnPlayerVote.Invoke(GetComponent<NetworkObject>().OwnerClientId, vote);
    }
    [Rpc(SendTo.Server)]
    void SendLawServerRpc(GameManager.Law law)
    {
        if (playerData.Role.Value != PlayerData.PlayerRole.Councilor && playerData.Role.Value != PlayerData.PlayerRole.Leader)
        {
            Debug.LogError("Tried to vote with non-councilor role!");
            return;
        }
        GameManager.Instance.TownData[playerData.TownId.Value].OnLawAddedToQueue.Invoke(law);
    }
    void SetUpChooseLawDropdown()
    {
        foreach (GameManager.Law law in System.Enum.GetValues(typeof(GameManager.Law)))
        {
            chooseLawDropdown.options.Add(new TMP_Dropdown.OptionData(Parliament.GetTextForLaw(law, true)));
        }
    }
    [Rpc(SendTo.Owner)]
    void DisplayNewVoteOwnerRpc(int noVotes, int yesVotes) //use playerId if you want to display who voted, but currently it is not used
    {
        yesVotesText.text = $"{yesVotes} people voted\nYES";
        noVotesText.text = $"{noVotes} people voted\nNO";
    }
    [Rpc(SendTo.Owner)]
    void AddLawToQueueMenuOwnerRpc(GameManager.Law law)
    {
        GameObject lawObject = Instantiate(lawMenuPrefab, lawList.transform);
        lawObject.transform.localPosition = new Vector3(115, topOffset, 0);
        topOffset -= 30;
        //Change if this object become more complex
        lawObject.GetComponent<TMP_Text>().text = Parliament.GetTextForLaw(law, true);
    }
    [Rpc(SendTo.Owner)]
    void SetUpVotingOwnerRpc(int votingCooldown, bool didVoteStarted, GameManager.Law law)
    {
        StartCoroutine(ChangeCooldownTimer(votingCooldown));
        if (didVoteStarted)
        {
            ChangeMainText(true, law);
            RemoveLawFromQueueMenu();
        }
        else
        {
            ChangeMainText(false, law);
        }
        
    }

    IEnumerator ChangeCooldownTimer(int cooldownTime)
    {
        while(true)
        {
            cooldownText.text = $"Cooldown: {cooldownTime} seconds";
            yield return new WaitForSeconds(1);
            cooldownTime--;
            if (cooldownTime <= 0) 
            {
                break;
            }
        }
    }

    void ChangeMainText(bool isVotingInProgress, GameManager.Law law) //law doesn't matter if isVotingInProgress is false
    {
        if (isVotingInProgress)
        {
            mainText.text = $"Currently voting on law\n{Parliament.GetTextForLaw(law, true)}";
        }
        else
        {
            mainText.text = $"Currently not voting on any law.";
        }
    }

    void RemoveLawFromQueueMenu()
    {
        Destroy(lawList.transform.GetChild(1).gameObject); //removing second child, because first is "Voting queue: "
        topOffset += 30; //because we removed one element, we need to adjust the offset
        for (int i = 1; i < lawList.transform.childCount; i++)
        {
            lawList.transform.GetChild(i).transform.position += new Vector3(0, 30, 0);
        }
    }

    [Rpc(SendTo.Owner)]
    public void InitiateMenuOwnerRpc()
    {
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
        GetComponent<Movement>().blockRotation = true;
        GetComponent<ObjectInteraction>().canInteract = false;
        GameObject.Find("Canvas").GetComponent<Menu>().amountOfDisplayedMenus++;
        councilorMenu.SetActive(true);
        menuJustOpened = true;
    }


}
