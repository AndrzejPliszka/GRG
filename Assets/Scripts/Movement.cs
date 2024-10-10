using System;
using Unity.Netcode;
using UnityEngine;

public class PlayerMovement : NetworkBehaviour
{
    [SerializeField] float speed;
    [SerializeField] float sensitivity = 5f;
    //[SerializeField] float reconciliationThreshold;
    //[SerializeField] float reconciliationSpeed = 1f;

    Vector3 lastServerPosition;
    GameObject playerCamera;
    CharacterController characterController;

    float currentCameraXRotation = 0;
    float currentVelocity = 0f;
    float gravity = 9.8f;
    float jumpHeight = 2f;
    bool isGrounded = false;

    [SerializeField] GameObject localModel; // Client-side predicted model
    CharacterController localCharacterController;

    //Manager components
    Menu menuManager;
    VoiceChat voiceChat;

    private void Awake()
    {
        playerCamera = GameObject.Find("Camera");
        voiceChat = GameObject.Find("NetworkManager").GetComponent<VoiceChat>();
        characterController = gameObject.GetComponent<CharacterController>();
        menuManager = GameObject.Find("Canvas").GetComponent<Menu>();
    }
    private void Start()
    {
        if (!IsOwner) return;
        playerCamera.transform.position = transform.position;
        // Disable server side model
        //gameObject.GetComponent<Renderer>().enabled = false; //disable if you want to see server side model
        characterController.detectCollisions = false;

        // Instantiate local model for client-side prediction
        localModel = Instantiate(localModel, transform.position, transform.rotation);
        localCharacterController = localModel.GetComponent<CharacterController>();

        lastServerPosition = transform.position; // Start with the current position
        menuManager.resumeGame(); //lock cursor in place
    }

    private void FixedUpdate()
    {
        if (!IsOwner) return;
        HandleMovement();
        HandleRotation();
        HandleGravityAndJumping(false);
        voiceChat.UpdateVivoxPosition();
        //SendPositionsToClientServerRpc();
    }

    private void Update()
    {
        if (!IsOwner) return;
        //pausing the game (move to other component?)
        if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.V))
        {
            if (menuManager.isGamePaused) { menuManager.resumeGame(); }
            else { menuManager.pauseGame(); }
        }
        if (Input.GetKeyDown(KeyCode.Space))
        {
            HandleGravityAndJumping(true);
        }
    }

    private void HandleMovement()
    {
        if(menuManager.isGamePaused) { return; }
        // Get player inputs
        float moveX = Input.GetAxis("Vertical");
        float moveZ = Input.GetAxis("Horizontal");

        // Client-side predicted movement
        Vector3 moveDirection = CalculateMovement(new Vector3(moveX, 0, moveZ), localModel.transform);
        localCharacterController.Move(moveDirection);

        // Send input to the server
        MovePlayerServerRpc(new Vector3(moveX, 0, moveZ));
    }

    private void HandleRotation() {
        if (menuManager.isGamePaused) { return; }
        float mouseY = Input.GetAxis("Mouse Y");
        float mouseX = Input.GetAxis("Mouse X");

        //Set up which only happens locally
        Vector3 playerCameraRotation = playerCamera.transform.eulerAngles;
        currentCameraXRotation += -mouseY * sensitivity;
        currentCameraXRotation = Mathf.Clamp(currentCameraXRotation, -90f, 90f);

        localModel.transform.Rotate(0, mouseX * sensitivity, 0);
        playerCamera.transform.position = localModel.transform.position;
        //Set up camera rotation
        playerCameraRotation.z = 0;
        playerCameraRotation.y = localModel.transform.eulerAngles.y;
        playerCameraRotation.x = currentCameraXRotation;
        playerCamera.transform.eulerAngles = playerCameraRotation;

        RotatePlayerServerRpc(mouseX);
    }

    private Vector3 CalculateMovement(Vector3 inputVector, Transform rotationTransform)
    {
        Vector3 clampedInput = new Vector3(Mathf.Clamp(inputVector.x, -1, 1), 0, Mathf.Clamp(inputVector.z, -1, 1));
        Vector3 moveVector = (clampedInput.x * rotationTransform.forward + clampedInput.z * rotationTransform.right) * speed * Time.fixedDeltaTime;
        return moveVector;
    }

    private void HandleGravityAndJumping(bool pressedSpace)
    {
        Vector3 jumpVector;
        Debug.Log("Client");
        jumpVector = CalculateVelocity(pressedSpace, localModel.transform);
        localCharacterController.Move(jumpVector * Time.fixedDeltaTime);

        HandleGravityAndJumpingServerRpc(pressedSpace);
    }

    private Vector3 CalculateVelocity(bool didTryJump, Transform transformOfObject)
    {
        isGrounded = transformOfObject.GetComponent<CharacterController>().isGrounded;
        Debug.Log("Before change: " + currentVelocity);
        if (isGrounded && currentVelocity < 0)
        {
            currentVelocity = -2f; // Ma³a wartoœæ zamiast 0, aby zapewniæ ci¹g³y kontakt
        }
        if (didTryJump && isGrounded)
        {
            currentVelocity = Mathf.Sqrt(jumpHeight * gravity);
            //return new Vector3(0, 0, 0); // I do it because it will be calculated anyway when this function gets didTryJump = false, which happenes on fixedUpdate
        }
        // Zastosowanie grawitacji
        currentVelocity -= gravity * Time.fixedDeltaTime;
        Debug.Log("After change: " + currentVelocity);
        Vector3 moveVector = new Vector3(0, currentVelocity, 0);
        return moveVector;
    }

    [Rpc(SendTo.Server)]
    private void HandleGravityAndJumpingServerRpc(bool pressedSpace)
    {
        Vector3 moveVector;
        Debug.Log("Server");
        moveVector = CalculateVelocity(pressedSpace, transform);
        characterController.Move(moveVector * Time.fixedDeltaTime);
    }
    [Rpc(SendTo.Server)]
    private void RotatePlayerServerRpc(float mouseX)
    {
        transform.Rotate(0, mouseX * sensitivity, 0);
    }
    [Rpc(SendTo.Server)]
    private void MovePlayerServerRpc(Vector3 inputVector)
    {
        // Server-side authoritative movement
        Vector3 finalMove = CalculateMovement(inputVector, transform);
        characterController.Move(finalMove); // Move the authoritative server object
    }





    //Code to reconciliate position but it is not needed (for now)
    /*
    private void ReconciliatePosition()
    {
        // Ask the server to send the authoritative position to the client
        SendPositionsToClientServerRpc();
    }

    [Rpc(SendTo.Server)]
    private void SendPositionsToClientServerRpc()
    {
        // Server sends back the authoritative position to the client
        Debug.Log(transform.position);
        SendClientPositionRpc(transform.position);
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void SendClientPositionRpc(Vector3 serverPosition)
    {
        Debug.Log(serverPosition.ToString() +": "+ localModel.transform.position + ": " + Time.deltaTime * reconciliationSpeed);
        // Reconcile client-side prediction with the authoritative server position
        float distance = Vector3.Distance(localModel.transform.position, serverPosition);

        if (distance > reconciliationThreshold)
        {
            // Smoothly move the client model towards the server's position
            localModel.transform.position = Vector3.Lerp(localModel.transform.position, serverPosition, Time.deltaTime * reconciliationSpeed);
        }

        // Update last known server position
        lastServerPosition = serverPosition;
    }
    */
}