using System;
using System.Linq;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UIElements;
using static UnityEngine.Rendering.DebugUI;

public class BuildModeController : NetworkBehaviour
{
    Movement playerMovement;
    ObjectInteraction objectInteraction;
    [SerializeField] BuildingData buildingData;
    [SerializeField] Material correctGhostObjectMaterial;
    [SerializeField] Material wrongPlacementGhostObjectMaterial;
    GameObject ghostObject;
    //These could be network variables changed on owner, but instead I just give them to ServerRpc
    BuildingData.BuildingType _currentBuildingType = BuildingData.BuildingType.Storage;
    public BuildingData.BuildingType CurrentBuildingType //Getter setter so I can activate event when this changes
    {
        get => _currentBuildingType;
        private set
        {
            if (_currentBuildingType != value)
            {
                _currentBuildingType = value;
                subtypeStructureLength = buildingData.GetDataOfBuildingType(value).baseObjects.Count();
                CurrentBuildingSubtype = Mathf.Clamp(CurrentBuildingSubtype, 0, subtypeStructureLength-1);
                OnSelectedBuildingChanged.Invoke(_currentBuildingType, buildingData.GetDataOfBuildingType(value).subtypeNames[CurrentBuildingSubtype]);
            }
        }
    }
    Vector3 objectPosition;
    Quaternion objectRotation;
    public int CurrentBuildingSubtype { get; private set; } = 0;
    int subtypeStructureLength;
    readonly float buildingDistance = 10f;
    readonly float rotateSpeed = 2f;
    bool isPlacedCorrectly;
    public NetworkVariable<bool> IsBuildModeActive { get; private set; } = new(false);

    public event Action<BuildingData.BuildingType, string> OnSelectedBuildingChanged;
    void Start()
    {
        playerMovement = GetComponent<Movement>();
        objectInteraction = GetComponent<ObjectInteraction>();
    }

    private void Update()
    {
        if(!IsOwner) return;

        if (Input.GetKeyDown(KeyCode.B))
        {
            ToggleBuildModeServerRpc();
        }

        if(IsBuildModeActive.Value && Input.GetKey(KeyCode.R))
            RotateGhostObject(true);


        if (IsBuildModeActive.Value && Input.GetAxis("Mouse ScrollWheel") > 0)
        {
            ChangeBuildingType(true);
            SpawnGhostObject();
        }
        else if (IsBuildModeActive.Value && Input.GetAxis("Mouse ScrollWheel") < 0)
        {
            ChangeBuildingType(false);
            SpawnGhostObject();
        }

        else if (IsBuildModeActive.Value && Input.GetKeyUp(KeyCode.E))
        {
            ChangeBuildingSubtype(true);
            SpawnGhostObject();
        }

        if (IsBuildModeActive.Value && Input.GetMouseButtonDown(0) && isPlacedCorrectly)
            PlaceObjectServerRpc(NetworkManager.Singleton.LocalClientId, objectPosition, objectRotation, CurrentBuildingType, CurrentBuildingSubtype);

        if (IsBuildModeActive.Value)
        {
            MoveGhostObjectToCursor();
            CheckValidityOfGhostObject();
        }
        else if(!IsBuildModeActive.Value && ghostObject != null)
        {
            Destroy(ghostObject);
            ghostObject = null;
        }
    }

    [Rpc(SendTo.Server)]
    void PlaceObjectServerRpc(ulong playerId, Vector3 objectPosition, Quaternion objectRotation, BuildingData.BuildingType building, int currentBuildingSubtype)
    {
        //TO DO: Make validation so player cannot cheat by building very far away
        //MAKE VALIDATION SO PLAYER CANNOT BUILD INSIDE OTHER Structures!
        if (!IsBuildModeActive.Value)
            return;

        BuildingData.Building buildingToSpawn = buildingData.GetDataOfBuildingType(building);
        GameObject spawnedObject = Instantiate(buildingToSpawn.baseObjects[currentBuildingSubtype], objectPosition, objectRotation);

        spawnedObject.GetComponent<NetworkObject>().Spawn();
        if (spawnedObject.GetComponent<UnbuiltBuilding>() == null)
            return;
        spawnedObject.GetComponent<UnbuiltBuilding>().OwnerId.Value = playerId;
        spawnedObject.GetComponent<UnbuiltBuilding>().ObjectStringDescription.Value = $"{buildingData.GetDataOfBuildingType(building).subtypeNames[currentBuildingSubtype]} {building}";
    }

    void RotateGhostObject(bool rotateRight)
    {
        if (ghostObject == null)
            return;

        ghostObject.transform.Rotate(new Vector3(0, rotateRight ? -rotateSpeed : rotateSpeed, 0));
        objectRotation = ghostObject.transform.rotation;
    }

    static public BuildingData.BuildingType GetAdjacentBuildingType(bool returnNext, BuildingData.BuildingType buildingType)
    {
        if (returnNext)
            if (Enum.IsDefined(typeof(BuildingData.BuildingType), buildingType + 1))
                return buildingType + 1;
            else
                return 0;
        else
            if (Enum.IsDefined(typeof(BuildingData.BuildingType), buildingType - 1))
            return buildingType - 1;
        else
            //We are at the beggining of the enum, so when we decrease we go to the last element
            return (BuildingData.BuildingType)Array.IndexOf(Enum.GetValues(typeof(BuildingData.BuildingType)), Enum.GetValues(typeof(BuildingData.BuildingType)).Cast<BuildingData.BuildingType>().Max());
    }

    void ChangeBuildingType(bool increment)
    {
        BuildingData.BuildingType buildingToChangeInto = GetAdjacentBuildingType(increment, CurrentBuildingType);
        CurrentBuildingType = buildingToChangeInto;
    }

    void ChangeBuildingSubtype(bool increment)
    {
        if (increment)
            if (CurrentBuildingSubtype + 1 < subtypeStructureLength)
                CurrentBuildingSubtype++;
            else
                CurrentBuildingSubtype = 0;
        else
            if (CurrentBuildingSubtype - 1 >= 0)
            CurrentBuildingSubtype--;
        else
            //We are at the beggining of the enum, so when we decrease we go to the last element
            CurrentBuildingSubtype = subtypeStructureLength - 1;
        OnSelectedBuildingChanged.Invoke(CurrentBuildingType, buildingData.GetDataOfBuildingType(CurrentBuildingType).subtypeNames[CurrentBuildingSubtype]);

    }

    void MoveGhostObjectToCursor()
    {
        if (GameObject.Find("Canvas").GetComponent<Menu>() != null && GameObject.Find("Canvas").GetComponent<Menu>().amountOfDisplayedMenus != 0)
            return;
        if (ghostObject == null)
            SpawnGhostObject();

        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hit, buildingDistance, ~LayerMask.GetMask("Ignore Raycast")))
        {
            GameObject hitObject = hit.collider.gameObject;
            if (!hitObject.CompareTag("Untagged"))
            {
                if (ghostObject != null)
                {
                    Destroy(ghostObject);
                    ghostObject = null;
                }
                return;
            }

            Vector3 targetPos = hit.point;
            ghostObject.transform.position = targetPos;
            objectPosition = ghostObject.transform.position;
        }
        else
        {
            Destroy(ghostObject);
            ghostObject = null;
        }
    }

    void SpawnGhostObject()
    {
        if(ghostObject != null)
        {
            Destroy(ghostObject);
        }
        BuildingData.Building buildingToSpawn = buildingData.GetDataOfBuildingType(CurrentBuildingType);
        ghostObject = Instantiate(buildingToSpawn.buildingModeObject, objectPosition, objectRotation);
    }

    //This function modifies isPlacedCorrectly and changes material
    void CheckValidityOfGhostObject()
    {
        if (ghostObject == null)
        {
            isPlacedCorrectly = false;
            return;
        }

        isPlacedCorrectly = true;
        Collider ghostCollider = ghostObject.GetComponent<Collider>();
        if (ghostCollider == null)
        {
            ghostCollider = ghostObject.GetComponentInChildren<Collider>();
        }
        Vector3 center = ghostCollider.bounds.center;
        Vector3 halfExtents = ghostCollider.bounds.extents;

        Collider[] hits = Physics.OverlapBox(center, halfExtents, ghostObject.transform.rotation);
        foreach (var hit in hits)
        {
            if (hit.gameObject != ghostObject && !hit.CompareTag("Untagged"))
            {
                isPlacedCorrectly = false;
                break;
            }
        }

        Renderer[] objectRenderers = ghostObject.GetComponentsInChildren<Renderer>();
        if (!isPlacedCorrectly)
        {
            foreach (Renderer renderer in objectRenderers)
            {
                renderer.material = wrongPlacementGhostObjectMaterial;
            }
        }
        else
        {
            foreach (Renderer renderer in objectRenderers)
            {
                renderer.material = correctGhostObjectMaterial;
            }
        }
    }

    [Rpc(SendTo.Server)]
    void ToggleBuildModeServerRpc()
    {
        IsBuildModeActive.Value = !IsBuildModeActive.Value;

        if (IsBuildModeActive.Value)
        {
            if (objectInteraction != null)
                objectInteraction.ToggleCanInteractOwnerRpc(false);
        }
        else
        {
            if (objectInteraction != null)
                objectInteraction.ToggleCanInteractOwnerRpc(true);
        }
    }
}
