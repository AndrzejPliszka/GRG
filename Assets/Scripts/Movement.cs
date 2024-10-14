using System;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.EventSystems;

public class Movement : NetworkBehaviour
{
    [SerializeField] float speed;
    [SerializeField] float sensitivity = 5f;
    [SerializeField] float reconciliationThreshold;

    GameObject playerCamera;
    CharacterController characterController;

    float currentCameraXRotation = 0;
    float currentVelocity = 0f;
    float gravity = 9.8f;
    [SerializeField] float jumpHeight = 2f;
    bool isGrounded = false;
    [SerializeField] GameObject localModel; // Client-side predicted model
    CharacterController localCharacterController; //and his character_Controller

    //Manager components (scripts)
    Menu menuManager;
    VoiceChat voiceChat;

    //only server side
    Vector3 finalMovementVector = Vector3.zero; //only used in reconciliation

    //set up references
    private void Awake()
    {
        playerCamera = GameObject.Find("Camera");
        voiceChat = GameObject.Find("VoiceChatManager").GetComponent<VoiceChat>();
        characterController = gameObject.GetComponent<CharacterController>();
        menuManager = GameObject.Find("Canvas").GetComponent<Menu>();
    }
    private void Start()
    {
        if (!IsOwner) return;
        playerCamera.transform.position = transform.position;
        // Disable server side model
        //gameObject.GetComponent<Renderer>().enabled = false; //disable if you want to see server side model
        if(!IsHost)
            gameObject.GetComponent<CharacterController>().enabled = false; //char contr is disabled to disable its collision (if enabled there would be two collisions in one place)
        else
            gameObject.GetComponent<CharacterController>().excludeLayers = LayerMask.GetMask("LocalObject");  //on host i cannot disable entire component coz it is referenced in script

        // Instantiate local model for client-side prediction
        localModel = Instantiate(localModel, transform.position, transform.rotation);
        localCharacterController = localModel.GetComponent<CharacterController>();

        menuManager.resumeGame(); //lock cursor in place
    }

    //all things that need to contact server regulary
    private void FixedUpdate()
    {
        if (!IsOwner) return;
        HandleMovement();
        HandleRotation();
        HandleGravityAndJumping(false);
        voiceChat.UpdateVivoxPosition();
        ReconciliateServerRpc();
    }

    //things that are done on keybord input (fixed update doesn't always register them)
    private void Update()
    {
        if (!IsOwner) return;
        //pausing the game (move to other component?)
        if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.V)) //v to test pause menu in unity editor
        {
            if (menuManager.isGamePaused)
                menuManager.resumeGame();
            else
                menuManager.pauseGame();
        }
        if (Input.GetKeyDown(KeyCode.Space))
        {
            HandleGravityAndJumping(true); //may cause errors coa theoretically player mashing space can invoke more physics updates
        }
    }

    //Function moving local player and sending keyboard inputs to server
    private void HandleMovement()
    {
        if(menuManager.isGamePaused) { return; }
        // get input
        float moveX = Input.GetAxis("Vertical");
        float moveZ = Input.GetAxis("Horizontal");

        MovePlayerServerRpc(new Vector3(moveX, 0, moveZ)); //and send it to the server

        // Client-side predicted movement
        Vector3 moveDirection = CalculateMovement(new Vector3(moveX, 0, moveZ), localModel.transform);
        localCharacterController.Move(moveDirection);
    }

    //Function dealing with camera and rotating both player and camera and sending mouse input to server
    private void HandleRotation() {
        if (menuManager.isGamePaused) { return; }
        float mouseY = Input.GetAxis("Mouse Y");
        float mouseX = Input.GetAxis("Mouse X");

        localModel.transform.Rotate(0, mouseX * sensitivity, 0); //rotate local model "horizontally"
        playerCamera.transform.position = localModel.transform.position; //set up camera in correct position
        //rotate camera "vertically"
        currentCameraXRotation += -mouseY * sensitivity;
        currentCameraXRotation = Mathf.Clamp(currentCameraXRotation, -90f, 90f); //clamp rotation so you cannot do 360 "vertically"
        Vector3 playerCameraRotation = playerCamera.transform.eulerAngles;
        playerCameraRotation.z = 0;
        playerCameraRotation.y = localModel.transform.eulerAngles.y; //make camera correctly rotated "horizontally"
        playerCameraRotation.x = currentCameraXRotation;
        playerCamera.transform.eulerAngles = playerCameraRotation;

        RotatePlayerServerRpc(mouseX);
    }

    //handle all physics and if pressedSpace is true also jumping (of course if touching ground)
    private void HandleGravityAndJumping(bool pressedSpace)
    {
        Vector3 jumpVector;
        jumpVector = CalculateGravity(pressedSpace, localModel.transform);
        localCharacterController.Move(jumpVector * Time.fixedDeltaTime);
        if(!IsHost) isGrounded = localCharacterController.isGrounded; //setting isGrounded after moving fixes undefined behaviour of characterController.isGrounded
        HandleGravityAndJumpingServerRpc(pressedSpace);
    }


    //function outputs vector which specifies where should object be moved based on inputVector
    //transform.forward and transform.right need to be based on some frame of refrence, and rotationTransform is precisly this reference
    private Vector3 CalculateMovement(Vector3 inputVector, Transform rotationTransform)
    {
        Vector3 clampedInput = new Vector3(Mathf.Clamp(inputVector.x, -1, 1), 0, Mathf.Clamp(inputVector.z, -1, 1)); //clamp because we don't trust client
        Vector3 moveVector = (clampedInput.x * rotationTransform.forward + clampedInput.z * rotationTransform.right) * speed * Time.fixedDeltaTime;
        return moveVector;
    }

    //calculates downward vector, dependent on gravity, didTryJump should be true if you want object to jump, but it will jump only if touching ground
    //transformOfObject is only used to make Host not modify velocity when this function is called second time with localModel *[SO THIS ARGUMENT IS PROBABLY REDUNDANT]
    private Vector3 CalculateGravity(bool didTryJump, Transform transformOfObject)
    {
        if (!IsHost || transformOfObject == localModel.transform || !IsOwner) //if host modify this only one time on fixedUpdate and on localModel, because it is called first and host has authority anyways (!isOwner so host updates velocity for other clients)
            currentVelocity -= gravity * Time.fixedDeltaTime;
        if (isGrounded)
        {
            currentVelocity = -2f; //small negative value so it is attracted to ground
        }
        if (didTryJump && isGrounded)
        {
            currentVelocity = Mathf.Sqrt(jumpHeight * gravity);
        }
        // Zastosowanie grawitacji
        Vector3 moveVector = new Vector3(0, currentVelocity, 0);
        return moveVector;
    }
    //Moves player on the server
    [Rpc(SendTo.Server)]
    private void MovePlayerServerRpc(Vector3 inputVector)
    {
        // Server-side authoritative movement
        Vector3 finalMove = CalculateMovement(inputVector, transform);
        characterController.Move(finalMove);
        finalMovementVector += finalMove; //finalMovementVector used for reconsiliation

    }
    //Rotates player on server (only "horizontally", because "vertical" movement is only registered by camera")
    [Rpc(SendTo.Server)]
    private void RotatePlayerServerRpc(float mouseX)
    {
        transform.Rotate(0, mouseX * sensitivity, 0);
    }

    //Same as HandleGravityAndJumping() but works on server and therefore modifies "real" objects and not localModel
    [Rpc(SendTo.Server)]
    private void HandleGravityAndJumpingServerRpc(bool pressedSpace)
    {
        Vector3 moveVector;
        moveVector = CalculateGravity(pressedSpace, transform);
        characterController.Move(moveVector  * Time.fixedDeltaTime);
        isGrounded = characterController.isGrounded;
        finalMovementVector += moveVector; //finalMovementVector used for reconsiliation
    }
    //Theoretically this function is redundant, but without it I would have to call reconciliation from HandleGravityAndJumpingServerRpc, and that's retarded
    [Rpc(SendTo.Server)]
    private void ReconciliateServerRpc()
    {
        ReconciliatePositionRpc(finalMovementVector);
    }

    //This function corrects position of localModel, so it alligns with real position sent from server (note that normal desync during walking is ~ 1 unit)
    //If currentMovementVector is anything than (0, 0, 0), do not reconciliate (that would tamper with player movement) unless desync is extreme
    //It is Rpc, so I can give "up to date" movement vector from server, and move character without stuttering
    //[PROBLEM: if desync is extreme this will not move localModel through walls, which makes that this method doesn't remove desync on some conditions]
    [Rpc(SendTo.Owner)]
    private void ReconciliatePositionRpc(Vector3 currentMovementVector)
    {
        Vector3 serverPosition = transform.position;
        float distance = Vector3.Distance(localModel.transform.position, serverPosition);
        //if player moves do not fix position (unless desync is extreme)
        if (currentMovementVector != Vector3.zero && distance < 3)
        {
            return;
        }

        if (distance > reconciliationThreshold)
        {
            // Smoothly move the client model towards the server's position
            localCharacterController.Move(Vector3.Lerp(localModel.transform.position, serverPosition, Time.fixedDeltaTime*10) - localModel.transform.position);
        }
    }
    
}