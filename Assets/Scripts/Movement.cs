using System;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.EventSystems;

public class Movement : NetworkBehaviour
{
    [SerializeField] float speed = 5f;
    [SerializeField] float runningModifier = 1.5f;
    [SerializeField] float sensitivity = 5f;
    [SerializeField] float reconciliationThreshold;

    bool isTesting = false;

    GameObject playerCamera;
    CharacterController characterController;

    [field: SerializeField] 
    public Vector3 CameraOffset { get; private set; } = Vector3.zero; //getter setter, because objectInteraction needs this
    [SerializeField] Vector3 startingPosition = new(0, 1, 0);  
    float currentCameraXRotation = 0;
    float currentVelocity = 0f;
    const float yVelocityOnGround = -2f; //y velocity that is applied when standing on the ground (cannot be zero, because characterController.OnGround doesn't work properly without it)
    const float gravity = 9.8f;
    [SerializeField] float jumpHeight = 2f;
    [SerializeField] bool renderOrginalModel = false; //Set true while debugging
    public bool IsGrounded {get; private set;} //getter-setter, because animator needs this to be public, but we want this readonly beyond this script
    public bool IsRunning { get; private set; }
    [field: SerializeField] public GameObject LocalPlayerModel { get; private set; } // Client-side predicted player model
    CharacterController localCharacterController; //and his character_Controller

    //Manager components (scripts)
    Menu menuManager;
    VoiceChat voiceChat;

    bool shouldReconciliate = true;

    //set up references
    private void Awake()
    {
        playerCamera = GameObject.Find("Camera");
        voiceChat = GameObject.Find("VoiceChatManager").GetComponent<VoiceChat>();
        characterController = gameObject.GetComponent<CharacterController>();
        menuManager = GameObject.Find("Canvas").GetComponent<Menu>();
    }


    //All things that need to be done on player connection, which are not component assigments; [MAYBE USE FUNCTIONS TO ENCAPSULATE THIS CODE]
    private void Start()
    {
        if (IsServer) //here will be movement set done on server, because only it can manage positions
        {
            characterController.enabled = false; //temporary disabling characterController because otherwise it will move from (0, 0, 0) on .Move()
            transform.position = startingPosition;
            characterController.enabled = true;
        }

        if (!IsOwner) return;
        // Creating local player model
        //It needs to be first things that Start does, because localPlayerModel is referenced by Animations.cs by name [MAYBE BAD PRACTISE]
        LocalPlayerModel = Instantiate(LocalPlayerModel, transform.position, transform.rotation);
        LocalPlayerModel.name = "LocalPlayerModel";
        localCharacterController = LocalPlayerModel.GetComponent<CharacterController>();
        localCharacterController.enabled = false;  // move character controller into starting position (without disabling character controller, it may warp character to previous position on update)
        LocalPlayerModel.transform.position = startingPosition;
        localCharacterController.enabled = true;

        //Disable rendering of server-side model
        if (!renderOrginalModel) { 
            if (gameObject.GetComponent<Renderer>())  //if PlayerPrefab has component renderer than disable it
                gameObject.GetComponent<Renderer>().enabled = false;
            //if not - one of child objects has (probably) renderer of entire object (if even that is not the case then change model or this code)
            else
                foreach (Renderer playerRenderers in gameObject.GetComponentsInChildren<Renderer>())
                    playerRenderers.enabled = false;
        }
        //***Disable collision locally for server-side model
        if (!IsHost) //character controller is disabled to disable its collision (if enabled there would be two collisions in one place, because of localPlayerModel character controller)
            gameObject.GetComponent<CharacterController>().enabled = false;
        else
            gameObject.GetComponent<CharacterController>().excludeLayers = LayerMask.GetMask("LocalObject");  //on host i cannot disable entire component cos it is referenced in ("client side") script

        //Position camera
        playerCamera.transform.position = LocalPlayerModel.transform.position + CameraOffset;
        playerCamera.transform.parent = LocalPlayerModel.transform;

        menuManager.ResumeGame(); //lock cursor in place
    }

    //all things that need to contact server regulary
    private void FixedUpdate()
    {
        if (!IsOwner) return;
        HandleMovement();
        HandleRotation();
        HandleGravityAndJumping(false);
        if(voiceChat) voiceChat.UpdateVivoxPosition(gameObject);
        if (shouldReconciliate)
        {
            ReconciliateServerRpc(LocalPlayerModel.transform.position);
            shouldReconciliate = false;
        }
    }

    //things that are done on keybord input (fixed update doesn't always register them)
    private void Update()
    {
        if (!IsOwner) return;
        //pausing the game (move to other component?)
        if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.V)) //v to test pause menu in unity editor
        {
            if (menuManager.isGamePaused)
                menuManager.ResumeGame();
            else
                menuManager.PauseGame();
        }
        if (Input.GetKeyDown(KeyCode.Space))
        {
            HandleGravityAndJumping(true); //may cause errors cos theoretically player mashing space can invoke more physics updates
        }

        if (Input.GetKeyDown(KeyCode.LeftShift))
        {
            IsRunning = true;
            SetIsRunningServerRpc(true);
        }
        else if (Input.GetKeyUp(KeyCode.LeftShift))
        {
            IsRunning = false;
            SetIsRunningServerRpc(false);
        }

        if (Input.GetKeyDown(KeyCode.Z))
        {
            isTesting = true;
        }
    }

    //Function moving local player and sending keyboard inputs to server
    private void HandleMovement()
    {
        if(menuManager.isGamePaused) { return; }
        // get input
        float moveX = Input.GetAxis("Vertical");
        float moveZ = Input.GetAxis("Horizontal");
        if (isTesting)
            moveX = 1;
        MovePlayerServerRpc(new Vector3(moveX, 0, moveZ)); //and send it to the server

        // Client side movement
        Vector3 moveDirection = CalculateMovement(new Vector3(moveX, 0, moveZ), LocalPlayerModel.transform);
        localCharacterController.Move(moveDirection);
    }

    //Function dealing with camera, rotation of both player and camera and sending mouse input to server
    //Note: for some reason rotation is significantly faster on application then in unity editor
    private void HandleRotation() {
        if (menuManager.isGamePaused) { return; }
        float mouseY = Input.GetAxis("Mouse Y");
        float mouseX = Input.GetAxis("Mouse X");

        LocalPlayerModel.transform.Rotate(0, mouseX * sensitivity, 0);
        currentCameraXRotation += -mouseY * sensitivity;
        currentCameraXRotation = Mathf.Clamp(currentCameraXRotation, -90f, 90f); //clamp rotation so you cannot do 360 "vertically"
        Vector3 playerCameraRotation = playerCamera.transform.eulerAngles;
        playerCameraRotation.z = 0;
        playerCameraRotation.y = LocalPlayerModel.transform.eulerAngles.y;  //make camera correctly rotated "horizontally"
        playerCameraRotation.x = currentCameraXRotation;
        playerCamera.transform.eulerAngles = playerCameraRotation;

        RotatePlayerServerRpc(mouseX);
    }

    //handle all physics and if pressedSpace is true also jumping (of course if touching ground)
    private void HandleGravityAndJumping(bool pressedSpace)
    {
        Vector3 jumpVector;
        jumpVector = CalculateGravity(pressedSpace, LocalPlayerModel.transform);
        localCharacterController.Move(jumpVector * Time.fixedDeltaTime);
        if(!IsHost) IsGrounded = localCharacterController.isGrounded; //setting isGrounded after moving fixes undefined behaviour of characterController.isGrounded
        HandleGravityAndJumpingServerRpc(pressedSpace);
    }


    //function outputs vector which specifies where should object be moved based on inputVector
    //transform.forward and transform.right need to be based on some frame of refrence, and rotationTransform is precisly this reference
    private Vector3 CalculateMovement(Vector3 inputVector, Transform rotationTransform)
    {
        Vector3 clampedInput = new(Mathf.Clamp(inputVector.x, -1, 1), 0, Mathf.Clamp(inputVector.z, -1, 1)); //clamp because we don't trust client
        if (clampedInput.magnitude > 1) clampedInput = clampedInput.normalized;   //make walking diagonally not faster than walking normally
        Vector3 moveVector = speed* Time.fixedDeltaTime * (clampedInput.x * rotationTransform.forward + clampedInput.z * rotationTransform.right);
        if (IsRunning)
            moveVector *= runningModifier;
        return moveVector;
    }

    //calculates downward vector, dependent on gravity, didTryJump should be true if you want object to jump, but it will jump only if touching ground
    //transformOfObject is only used to make Host not modify velocity when this function is called second time with localPlayerModel *[SO THIS ARGUMENT IS PROBABLY REDUNDANT]
    private Vector3 CalculateGravity(bool didTryJump, Transform transformOfObject)
    {
        if (!IsHost || transformOfObject == LocalPlayerModel.transform || !IsOwner) //if host modify this only one time on fixedUpdate and on localPlayerModel, because it is called first and host has authority anyways (!isOwner so host updates velocity for other clients)
            currentVelocity -= gravity * Time.fixedDeltaTime;
        if (IsGrounded)
        {
            currentVelocity = yVelocityOnGround; //small negative value so it is always attracted to ground (and ensures that characterController.isOnGround works fine)
        }
        if (didTryJump && IsGrounded)
        {
            currentVelocity = Mathf.Sqrt(jumpHeight * gravity);
        }
        // Zastosowanie grawitacji
        Vector3 moveVector = new(0, currentVelocity, 0);
        return moveVector;
    }
    //Moves player on the server
    [Rpc(SendTo.Server)]
    private void MovePlayerServerRpc(Vector3 inputVector)
    {
        // Server-side authoritative movement
        Vector3 finalMove = CalculateMovement(inputVector, transform);
        characterController.Move(finalMove);

    }
    //Rotates player on server (only "horizontaly", because "vertical" movement is only registered by camera)
    [Rpc(SendTo.Server)]
    private void RotatePlayerServerRpc(float mouseX)
    {
        transform.Rotate(0, mouseX * sensitivity, 0);
    }

    //Handle all physics and also jumping but works on server and therefore modifies "real" objects and not localPlayerModel
    [Rpc(SendTo.Server)]
    private void HandleGravityAndJumpingServerRpc(bool pressedSpace)
    {
        Vector3 moveVector;
        moveVector = CalculateGravity(pressedSpace, transform);
        characterController.Move(moveVector  * Time.fixedDeltaTime);
        IsGrounded = characterController.isGrounded;
    }

    [Rpc(SendTo.Server)]
    private void SetIsRunningServerRpc(bool setIsRunning)
    {
        IsRunning = setIsRunning;
    }
    //This function calls ReconciliatePositionRpc on owner and resets finalMovementVector
    [Rpc(SendTo.Server)]
    private void ReconciliateServerRpc(Vector3 localPosition)
    {
        float distance = Vector3.Distance(localPosition, transform.position);
        //if player moves and desync is small do not fix position
        Debug.Log(distance);
        if (distance == 0f)
        {
            HandleWhenNoDescyncOwnerRpc();
        }

        ReconciliatePositionRpc(transform.position - localPosition);
    }

    Vector3 lastOffset;

    //This function corrects position of localPlayerModel, so it alligns with real position sent from server (note that normal desync during walking is ~ 1 unit)
    //It is Rpc, so I can give "up to date" movement vector from server and position, and move character without stuttering
    [Rpc(SendTo.Owner)]
    private void ReconciliatePositionRpc(Vector3 offsetToMove)
    {
        lastOffset = offsetToMove;
        localCharacterController.detectCollisions = false;
        localCharacterController.Move(offsetToMove);
        localCharacterController.detectCollisions = true;
        shouldReconciliate = true;
    }
    [Rpc(SendTo.Owner)]
    private void HandleWhenNoDescyncOwnerRpc()
    {
        shouldReconciliate = true;
    }

}