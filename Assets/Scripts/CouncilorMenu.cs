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

    PlayerData playerData;
    float topOffset = -100; //used in displaying law queue
    public void Awake()
    {
        playerData = GetComponent<PlayerData>();

        councilorMenu = GameObject.Find("CouncilorMenu");
        chooseLawDropdown = GameObject.Find("LawChooseDropdown").GetComponent<TMP_Dropdown>();
        submitNewLawButton = GameObject.Find("ProposeLawChangeButton").GetComponent<Button>();
        lawList = GameObject.Find("VotingQueueContainer");
    }
    public override void OnNetworkSpawn()
    {
        if(!IsOwner) return;
        SetUpChooseLawDropdown();
        playerData.TownId.OnValueChanged += SetUpButtons;
        councilorMenu.SetActive(false);
    }
    void SetUpButtons(int oldTownId, int newTownId)
    {
        //Clear old listeners
        if (oldTownId >= 0 && oldTownId < GameManager.Instance.TownData.Count)
        {
            GameManager.Instance.TownData[oldTownId].OnLawAddedToQueue -= AddLawToQueueMenu;
            GameManager.Instance.TownData[playerData.TownId.Value].OnVotingStart -= SetUpVoting;
            submitNewLawButton.onClick.RemoveAllListeners();
        }
        //Set up new ones
        if (newTownId >= 0 && newTownId < GameManager.Instance.TownData.Count)
        {
            GameManager.Instance.TownData[playerData.TownId.Value].OnLawAddedToQueue += AddLawToQueueMenu;
            GameManager.Instance.TownData[playerData.TownId.Value].OnVotingStart += SetUpVoting;
            submitNewLawButton.onClick.AddListener(() =>
            {
                GameManager.Law law = Parliament.GetLawFromString(chooseLawDropdown.options[chooseLawDropdown.value].text);
                GameManager.Instance.TownData[playerData.TownId.Value].OnLawAddedToQueue.Invoke(law);
            });
        }
    }
    void SetUpChooseLawDropdown()
    {
        foreach (GameManager.Law law in System.Enum.GetValues(typeof(GameManager.Law)))
        {
            chooseLawDropdown.options.Add(new TMP_Dropdown.OptionData(Parliament.GetTextForLaw(law, true)));
        }
    }

    void AddLawToQueueMenu(GameManager.Law law)
    {
        GameObject lawObject = Instantiate(lawMenuPrefab, lawList.transform);
        lawObject.transform.localPosition = new Vector3(115, topOffset, 0);
        topOffset -= 30;
        //Change if this object become more complex
        lawObject.GetComponent<TMP_Text>().text = Parliament.GetTextForLaw(law, true);
    }
    void SetUpVoting(int votingCooldown)
    {
        _ = votingCooldown;
        RemoveLawFromQueueMenu();
    }
    void RemoveLawFromQueueMenu()
    {
        Destroy(lawList.transform.GetChild(1).gameObject); //removing second child, because first is "Voting queue: "
        for (int i = 1; i < lawList.transform.childCount; i++)
        {
            lawList.transform.GetChild(i).transform.position += new Vector3(0, 30, 0);
        }
    }

    [Rpc(SendTo.Owner)]
    public void InitiateMenuOwnerRpc()
    {
        councilorMenu.SetActive(true);
        Cursor.visible = true; 
        Cursor.lockState = CursorLockMode.None;
    }


}
